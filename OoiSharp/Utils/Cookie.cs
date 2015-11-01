using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Web;
using OoiSharp.Utils.ArrayExtensions;

namespace OoiSharp.Utils
{
    public static class Cookie
    {
        public static bool IsHmacKeyTemporary { get; private set; }
        
        private static byte[] hmacKey;
        private static int initStatus = 0;
        private static readonly ManualResetEventSlim initialized = new ManualResetEventSlim();
        private static readonly System.Text.UTF8Encoding cookieEncoding = new System.Text.UTF8Encoding(false, true);
        private static readonly ThreadLocal<HMACSHA256> hmac = new ThreadLocal<HMACSHA256>(() => new HMACSHA256(hmacKey));

        private const int SigLength = 32;
        private const int TsLength = 8;
        private const int ValidityLength = 4;
        private const int IpAddrLength = 16;

        private const int IpAddrOffset = SigLength;
        private const int TsOffset = IpAddrOffset + IpAddrLength;
        private const int ValidityOffset = TsOffset + TsLength;
        private const int DataOffset = ValidityOffset + ValidityLength;

        public static void Init()
        {
            if(Interlocked.CompareExchange(ref initStatus, 1, 0) != 0) {
                initialized.Wait();
                return;
            }

            hmacKey = System.Text.Encoding.UTF8.GetBytes(ConfigurationManager.AppSettings["hmacKey"] ?? "");
            if(hmacKey.Length > 64) {
                using(SHA512 sha = SHA512.Create()) {
                    hmacKey = sha.ComputeHash(hmacKey);
                }
            } else if(hmacKey.Length < 8) {
                IsHmacKeyTemporary = true;
                hmacKey = new byte[24];
                using(var rng = RandomNumberGenerator.Create()) {
                    rng.GetBytes(hmacKey);
                }
            }

            initialized.Set();
            initStatus = 2;
        }

        public static string SignCookie(string input, long timestamp = long.MinValue, IPAddress remoteIp = null, uint validTime = uint.MaxValue)
        {
#if !DEBUG
            if(initStatus != 2) {
                Init();
            }
#endif
            var dataLen = cookieEncoding.GetByteCount(input) + DataOffset;
            var data = new byte[dataLen];
            var encodedLength = cookieEncoding.GetBytes(input, 0, input.Length, data, DataOffset);
            System.Diagnostics.Debug.Assert(encodedLength == (dataLen - DataOffset));
            
            data.PutInt64LE(TsOffset, timestamp == long.MinValue ? UnixTimestamp.CurrentMillisecondTimestamp : timestamp);
            data.PutUInt32LE(ValidityOffset, validTime);

            if(remoteIp == null) { //which means don't care
                remoteIp = IPAddress.IPv6Any; //all zeroes
            } else if(remoteIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                remoteIp = remoteIp.MapToIPv6();
            }
            Array.Copy(remoteIp.GetAddressBytes(), 0, data, IpAddrOffset, IpAddrLength);

            Array.Copy(hmac.Value.ComputeHash(data, SigLength, data.Length - SigLength), data, SigLength);
            return Convert.ToBase64String(data).Replace('+', '.').Replace('/', '_').Replace('=', '-');
        }

        public static string VerifyCookie(string input, IPAddress remoteIp = null, uint validTime = uint.MaxValue)
        {
            long scratch;
            return VerifyCookie(input, out scratch, remoteIp, validTime);
        }

        public static string VerifyCookie(string input, out long signTime, IPAddress remoteIp = null, uint validTime = uint.MaxValue)
        {
            signTime = long.MinValue;
#if !DEBUG
            if(initStatus != 2) {
                Init();
            }
#endif
            if(input == null) {
                return null;
            }
            if(input.Length < 80) { //Minimum length (60 byte sig+metadata)
                return null;
            }
            if((input.Length & 3) != 0) { //Length of a base64 string must be of multiples of 4
                return null;
            }
            
            try {
                var data = Convert.FromBase64String(input.Replace('.', '+').Replace('_', '/').Replace('-', '=')); //throws FormatException

                var computed = hmac.Value.ComputeHash(data, SigLength, data.Length - SigLength); //Compute signature
                int trash = 0;
                for(int i = 0; i < SigLength; i++) {    //Compare the sig. (Avoiding timing attack)
                    trash |= data[i] ^ computed[i];
                }
                if(trash != 0) {    //sig mismatch, reject.
                    return null;
                }

                signTime = data.GetInt64LE(TsOffset);
                var currentTs = UnixTimestamp.CurrentMillisecondTimestamp;
                if(signTime > currentTs) {    //Reject anything with a timestamp from the future
                    return null;
                }
                
                //Compute the validity period for this verification.
                //Subtract 1 to treat value 0 (defined as infinite) as greatest.
                validTime = Math.Min(data.GetUInt32LE(ValidityOffset) - 1, validTime - 1);
                if(validTime != uint.MaxValue) {
                    if((signTime + validTime) < currentTs) { //Should be <=, but we've subtracted 1 from the original value.
                        return null;
                    }
                }

                //all zeroes denotes don't care.
                if(!data.TrueForRange(IpAddrOffset, IpAddrLength, x => x == 0)) {
                    if(remoteIp == null) { //No remote ip provided while a verification is required, reject.
                        return null;
                    }
                    if(remoteIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                        remoteIp = remoteIp.MapToIPv6();
                    }
                    var ip = remoteIp.GetAddressBytes();
                    if(!ArrayEx.RangeEquals(data, IpAddrOffset, ip, 0, IpAddrLength)) {
                        return null;
                    }
                }

                return cookieEncoding.GetString(data, DataOffset, data.Length - DataOffset); //throws ArgumentException
            } catch(FormatException) {
                return null;
            } catch(ArgumentException) {
                return null;
            }
        }
    }
}