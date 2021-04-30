using System;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    public static class CacheSerializerUtils
    {
        public const int ARRAY_SIZE_OFFSET = 0xFFFF; // 64 kb

        public static void EncodeShort(short value, ref byte[] bytes, ref int offset)
        {
            bytes[offset++] = (byte)value;
            bytes[offset++] = (byte)(value >> 8);
        }

        public static short DecodeShort(ref byte[] bytes, ref int offset)
        {
            return (short)(bytes[offset++] + (bytes[offset++] << 8));
        }

        public static void EncodeLong(long value, ref byte[] bytes, ref int offset)
        {
            for (int k = 0; k < 8; ++k)
            {
                bytes[offset++] = (byte) (value >> (8 * k));
            }
        }

        public static long DecodeLong(ref byte[] bytes, ref int offset)
        {
            long result = 0;

            for (int k = 0; k < 8; ++k)
            {
                result += (long)bytes[offset++] << (8 * k);
            }

            return result;
        }
        
        public static byte[] EnsureSize(byte[] array, int offset)
        {
            if (offset + ARRAY_SIZE_OFFSET / 2 > array.Length)
            {
                byte[] newArray = new byte[array.Length * 2];
                Array.Copy(array, newArray, offset);
                return newArray;
            }
			
            return array;
        }

        public static void EncodeString(string value, ref byte[] bytes, ref int offset)
        {
            char[] charArray = value.ToCharArray();
            EncodeShort((short)charArray.Length, ref bytes, ref offset);
				
            for (var c = 0; c < charArray.Length; c++)
            {
                bytes[offset++] = (byte) charArray[c];
            }
        }
		
        public static string DecodeString(ref byte[] bytes, ref int offset)
        {
            int length = DecodeShort(ref bytes, ref offset);
            char[] charArray = new char[length];
			
            for (var c = 0; c < length; c++)
            {
                charArray[c] = (char)bytes[offset++];
            }
			
            return new string(charArray);
        }
    }
}