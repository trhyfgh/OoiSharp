using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OoiSharp.Utils.ArrayExtensions
{
    public static class ArrayEx
    {
        public static long GetInt64(this byte[] b, int index)
        {
#if false
            if(b == null) throw new NullReferenceException();
            if(b.Length - 8 < index) throw new IndexOutOfRangeException();
            if(index < 0) throw new IndexOutOfRangeException();

            unsafe
            {
                fixed(byte* lpValue = &b[index])
                {
                    return *(long*)lpValue;
                }
            }
#else
            return BitConverter.ToInt64(b, index);
#endif
        }

        public static uint GetUInt32(this byte[] b, int index)
        {
#if false
            if(b == null) throw new NullReferenceException();
            if(b.Length - 4 < index) throw new IndexOutOfRangeException();
            if(index < 0) throw new IndexOutOfRangeException();

            unsafe
            {
                fixed (byte* lpValue = &b[index])
                {
                    return *(uint*)lpValue;
                }
            }
#else
            return BitConverter.ToUInt32(b, index);
#endif
        }

        public static void PutUInt32(this byte[] b, int index, uint value)
        {
            if(b == null) throw new NullReferenceException();
            if(b.Length - 4 < index) throw new IndexOutOfRangeException();
            if(index < 0) throw new IndexOutOfRangeException();

#if UNSAFE
            unsafe
            {
                fixed (byte* lpValue = &b[index])
                {
                    *(uint*)lpValue = value;
                }
            }
#else
            if(BitConverter.IsLittleEndian) {
                b[index + 0] = (byte)(value >> 0);
                b[index + 1] = (byte)(value >> 8);
                b[index + 2] = (byte)(value >> 16);
                b[index + 3] = (byte)(value >> 24);
            } else {
                b[index + 3] = (byte)(value >> 0);
                b[index + 2] = (byte)(value >> 8);
                b[index + 1] = (byte)(value >> 16);
                b[index + 0] = (byte)(value >> 24);
            }
#endif
        }

        public static void PutInt64(this byte[] b, int index, long value)
        {
            if(b == null) throw new NullReferenceException();
            if(b.Length - 8 < index) throw new IndexOutOfRangeException();
            if(index < 0) throw new IndexOutOfRangeException();

#if UNSAFE
            unsafe
            {
                fixed (byte* lpValue = &b[index])
                {
                    *(long*)lpValue = value;
                }
            }
#else
            if(BitConverter.IsLittleEndian) {
                b[index + 0] = (byte)(value >> 0);
                b[index + 1] = (byte)(value >> 8);
                b[index + 2] = (byte)(value >> 16);
                b[index + 3] = (byte)(value >> 24);
                b[index + 4] = (byte)(value >> 32);
                b[index + 5] = (byte)(value >> 40);
                b[index + 6] = (byte)(value >> 48);
                b[index + 7] = (byte)(value >> 56);
            }else {
                b[index + 7] = (byte)(value >> 0);
                b[index + 6] = (byte)(value >> 8);
                b[index + 5] = (byte)(value >> 16);
                b[index + 4] = (byte)(value >> 24);
                b[index + 3] = (byte)(value >> 32);
                b[index + 2] = (byte)(value >> 40);
                b[index + 1] = (byte)(value >> 48);
                b[index + 0] = (byte)(value >> 56);
            }
#endif
        }

        public static bool RangeEquals(byte[] a, int aIndex, byte[] b, int bIndex, int length)
        {
            if(a == null) throw new NullReferenceException();
            if(b == null) throw new NullReferenceException();
            if(length < 0) throw new ArgumentException();
            if(aIndex < 0) throw new IndexOutOfRangeException();
            if(bIndex < 0) throw new IndexOutOfRangeException();
            if(b.Length - length < bIndex) throw new IndexOutOfRangeException();
            if(a.Length - length < aIndex) throw new IndexOutOfRangeException();

#if UNSAFE
            unsafe
            {
                fixed(byte* lpByteA = &a[aIndex])
                fixed(byte* lpByteB = &b[bIndex])
                {
                    int* lpA = (int*)lpByteA;
                    int* lpB = (int*)lpByteB;
                    int loop = length >> 2;
                    for(int i = 0; i < loop; i++) {
                        if(lpA[i] != lpB[i]) return false;
                    }
                    for(int i = length & 0x7FFFFFFC; i < length; i++) {
                        if(lpByteA[i] != lpByteB[i]) return false;
                    }
                }
            }
#else
            for(int i = 0; i < length; i++) {
                if(a[aIndex + i] != b[bIndex + i]) return false;
            }
#endif
            return true;
        }

        public static bool TrueForRange<T>(this T[] arr, int index, int length, Func<T, bool> predict)
        {
            if(arr == null) throw new NullReferenceException();
            if(length < 0) throw new ArgumentException();
            if(index < 0) throw new IndexOutOfRangeException();
            if(arr.Length - length < index) throw new IndexOutOfRangeException();

            for(int i = 0; i < length; i++) {
                if(!predict(arr[index + i])) return false;
            }
            return true;
        }
    }
}