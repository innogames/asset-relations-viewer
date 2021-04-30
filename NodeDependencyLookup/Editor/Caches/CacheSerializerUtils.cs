using System;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    public static class CacheSerializerUtils
    {
        public const int ARRAY_SIZE_OFFSET = 0xFFFF; // 64 kb
        
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
            bytes[offset++] = (byte)charArray.Length;
				
            for (var c = 0; c < charArray.Length; c++)
            {
                bytes[offset++] = (byte) charArray[c];
            }
        }
		
        public static string DecodeString(ref byte[] bytes, ref int offset)
        {
            int length = bytes[offset++];
            char[] charArray = new char[length];
			
            for (var c = 0; c < length; c++)
            {
                charArray[c] = (char)bytes[offset++];
            }
			
            return new string(charArray);
        }
    }
}