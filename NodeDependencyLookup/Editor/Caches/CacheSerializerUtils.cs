using System;
using System.Collections.Generic;
using System.IO;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Contains all necessary functions to encode and decode the cache to binary and back
	/// Reason for doing own conversion instead of using json is speed.
	/// </summary>
	public static class CacheSerializerUtils
	{
		public const int ARRAY_SIZE_OFFSET = 0xFFFF; // 64 kb

		public static void EncodeShort(short value, ref byte[] bytes, ref int offset)
		{
			bytes[offset++] = (byte) value;
			bytes[offset++] = (byte) (value >> 8);
		}

		public static short DecodeShort(ref byte[] bytes, ref int offset)
		{
			return (short) (bytes[offset++] + (bytes[offset++] << 8));
		}

		public static void EncodeInt(int value, ref byte[] bytes, ref int offset)
		{
			for (var k = 0; k < 4; ++k)
			{
				bytes[offset++] = (byte) (value >> (4 * k));
			}
		}

		public static int DecodeInt(ref byte[] bytes, ref int offset)
		{
			int result = 0;

			for (var k = 0; k < 4; ++k)
			{
				result += bytes[offset++] << (4 * k);
			}

			return result;
		}

		public static void EncodeLong(long value, ref byte[] bytes, ref int offset)
		{
			for (var k = 0; k < 8; ++k)
			{
				bytes[offset++] = (byte) (value >> (8 * k));
			}
		}

		public static long DecodeLong(ref byte[] bytes, ref int offset)
		{
			long result = 0;

			for (var k = 0; k < 8; ++k)
			{
				result += (long) bytes[offset++] << (8 * k);
			}

			return result;
		}

		public static byte[] EnsureSize(byte[] array, int offset)
		{
			if (offset + ARRAY_SIZE_OFFSET / 2 > array.Length)
			{
				var newArray = new byte[array.Length * 2];
				Array.Copy(array, newArray, offset);
				return newArray;
			}

			return array;
		}

		public static void EncodeString(string value, ref byte[] bytes, ref int offset)
		{
			var charArray = value.ToCharArray();
			EncodeShort((short) charArray.Length, ref bytes, ref offset);

			for (var c = 0; c < charArray.Length; c++)
			{
				bytes[offset++] = (byte) charArray[c];
			}
		}

		public static string DecodeString(ref byte[] bytes, ref int offset)
		{
			int length = DecodeShort(ref bytes, ref offset);

#if UNITY_2021_1_OR_NEWER
            Span<char> charArray = stackalloc char[length];
#else
			var charArray = new char[length];
#endif

			for (var c = 0; c < length; c++)
			{
				charArray[c] = (char) bytes[offset++];
			}

			return new string(charArray);
		}

		private static void EncodePathSegments(PathSegment[] pathSegments, ref byte[] bytes, ref int offset)
		{
			EncodeShort((short) pathSegments.Length, ref bytes, ref offset);

			foreach (var pathSegment in pathSegments)
			{
				EncodeString(pathSegment.Name, ref bytes, ref offset);
				EncodeShort((short) pathSegment.Type, ref bytes, ref offset);
			}
		}

		private static PathSegment[] DecodePathSegments(ref byte[] bytes, ref int offset)
		{
			int pathLength = DecodeShort(ref bytes, ref offset);
			var pathSegments = new PathSegment[pathLength];

			for (var p = 0; p < pathLength; p++)
			{
				pathSegments[p] = new PathSegment(DecodeString(ref bytes, ref offset),
					(PathSegmentType) DecodeShort(ref bytes, ref offset));
			}

			return pathSegments;
		}

		public static void EncodeDependencies(List<Dependency> dependencies, ref byte[] bytes, ref int offset)
		{
			EncodeInt(dependencies.Count, ref bytes, ref offset);

			for (var k = 0; k < dependencies.Count; k++)
			{
				var dependency = dependencies[k];
				EncodeString(dependency.Id, ref bytes, ref offset);
				EncodeString(dependency.DependencyType, ref bytes, ref offset);
				EncodeString(dependency.NodeType, ref bytes, ref offset);

				EncodePathSegments(dependency.PathSegments, ref bytes, ref offset);

				bytes = EnsureSize(bytes, offset);
			}
		}

		public static List<Dependency> DecodeDependencies(ref byte[] bytes, ref int offset)
		{
			var numDependencies = DecodeInt(ref bytes, ref offset);
			var dependencies = new List<Dependency>(numDependencies);

			for (var k = 0; k < numDependencies; k++)
			{
				var id = DecodeString(ref bytes, ref offset);
				var connectionType = DecodeString(ref bytes, ref offset);
				var nodeType = DecodeString(ref bytes, ref offset);
				var pathSegments = DecodePathSegments(ref bytes, ref offset);

				dependencies.Add(new Dependency(id, connectionType, nodeType, pathSegments));
			}

			return dependencies;
		}

		public static Dictionary<string, GenericDependencyMappingNode> GenerateIdLookup(
			GenericDependencyMappingNode[] nodes)
		{
			var lookup = new Dictionary<string, GenericDependencyMappingNode>();

			foreach (var node in nodes)
			{
				lookup[node.Id] = node;
			}

			return lookup;
		}

		public static GenericDependencyMappingNode[] LoadGenericLookup(string path)
		{
			if (!File.Exists(path))
			{
				return new GenericDependencyMappingNode[0];
			}

			var bytes = File.ReadAllBytes(path);
			var offset = 0;

			var count = DecodeLong(ref bytes, ref offset);
			var nodes = new GenericDependencyMappingNode[count];

			for (var i = 0; i < count; ++i)
			{
				var id = DecodeString(ref bytes, ref offset);
				var type = DecodeString(ref bytes, ref offset);
				var dependencies = DecodeDependencies(ref bytes, ref offset);
				var node = new GenericDependencyMappingNode(id, type) {Dependencies = dependencies};
				nodes[i] = node;
			}

			return nodes;
		}

		public static void SaveGenericMapping(string directory, string fileName, GenericDependencyMappingNode[] nodes)
		{
			var bytes = new byte[ARRAY_SIZE_OFFSET];
			var offset = 0;

			EncodeLong(nodes.Length, ref bytes, ref offset);

			foreach (var node in nodes)
			{
				EncodeString(node.Id, ref bytes, ref offset);
				EncodeString(node.Type, ref bytes, ref offset);
				EncodeDependencies(node.Dependencies, ref bytes, ref offset);

				bytes = EnsureSize(bytes, offset);
			}

			var path = Path.Combine(directory, fileName);

			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			File.WriteAllBytes(path, bytes);
		}
	}
}