using System;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/**
	 * Serializer for the AssetDependencyCache
	 * Since all json solutions where to slow and the structure is quite simple it was fastest to manually write it into a byte array
	 */
	public class AssetDependencyCacheSerializer
	{
		public const string EOF = "EndOfSerializedAssetDependencyCache";
		public const int ARRAY_SIZE_OFFSET = 0xFFFF; // 64 kb
		
		public static byte[] Serialize(AssetNode[] assetNodes)
		{
			byte[] bytes = new byte[ARRAY_SIZE_OFFSET];
			int offset = 0;

			int length = assetNodes.Length;
			bytes[offset++] = (byte) length;
			bytes[offset++] = (byte) (length >> 8);
			
			foreach (AssetNode assetNode in assetNodes)
			{
				EncodeString(assetNode.Guid, ref bytes, ref offset);

				bytes[offset++] = (byte)assetNode.Res.Count;
				bytes[offset++] = (byte)(assetNode.Res.Count >> 8);
				
				for (var i = 0; i < assetNode.Res.Count; i++)
				{
					AssetNode.ResolverData data = assetNode.Res[i];
					long timeStamp = data.TimeStamp;

					for (int k = 0; k < 8; ++k)
					{
						bytes[offset++] = (byte) (timeStamp >> (8 * k));
					}
					
					EncodeString(data.Id, ref bytes, ref offset);

					Dependency[] dependencies = data.Dep;
					
					bytes[offset++] = (byte) dependencies.Length;
					bytes[offset++] = (byte) (dependencies.Length >> 8);
					
					for (var k = 0; k < dependencies.Length; k++)
					{
						Dependency dependency = dependencies[k];
						EncodeString(dependency.Id, ref bytes, ref offset);
						EncodeString(dependency.ConnectionType, ref bytes, ref offset);
						EncodeString(dependency.NodeType, ref bytes, ref offset);

						int pathLength = dependency.PathSegments.Length;
						bytes[offset++] = (byte) pathLength;
						bytes[offset++] = (byte) (pathLength >> 8);
						
						for (var p = 0; p < pathLength; p++)
						{
							PathSegment pathSegment = dependency.PathSegments[p];
							
							EncodeString(pathSegment.Name, ref bytes, ref offset);
							bytes[offset++] = (byte) pathSegment.Type;
						}
						
						bytes = EnsureSize(bytes, offset);
					}

					bytes = EnsureSize(bytes, offset);
				}
			}
			
			EncodeString(EOF, ref bytes, ref offset);
			
			return bytes;
		}
		
		public static AssetNode[] Deserialize(byte[] bytes)
		{
			int offset = 0;
			int nodeLength = bytes[offset++] + (bytes[offset++] << 8);
			
			AssetNode[] assetsNodes = new AssetNode[nodeLength];

			for (int n = 0; n < nodeLength; ++n)
			{
				string guid = DecodeString(ref bytes, ref offset);
				AssetNode assetNode = new AssetNode(guid);
				int resLength = bytes[offset++] + (bytes[offset++] << 8);
				
				for (var i = 0; i < resLength; i++)
				{
					AssetNode.ResolverData data = new AssetNode.ResolverData();
					long timeStamp = 0;

					for (int k = 0; k < 8; ++k)
					{
						timeStamp += (long)bytes[offset++] << (8 * k);
					}

					data.TimeStamp = timeStamp;
					data.Id = DecodeString(ref bytes, ref offset);

					int dependencyLength = bytes[offset++] + (bytes[offset++] << 8);
					Dependency[] dependencies = new Dependency[dependencyLength];
					
					for (var k = 0; k < dependencyLength; k++)
					{
						string id = DecodeString(ref bytes, ref offset);
						string connectionType = DecodeString(ref bytes, ref offset);
						string nodeType = DecodeString(ref bytes, ref offset);

						int pathLength = bytes[offset++] + (bytes[offset++] << 8);
						PathSegment[] pathSegments = new PathSegment[pathLength];
						
						for (var p = 0; p < pathLength; p++)
						{
							PathSegment pathSegment = new PathSegment();
							
							pathSegment.Name = DecodeString(ref bytes, ref offset);
							pathSegment.Type = (PathSegmentType)bytes[offset++];

							pathSegments[p] = pathSegment;
						}
						
						Dependency dependency = new Dependency(id, connectionType, nodeType, pathSegments);

						dependencies[k] = dependency;
					}

					data.Dep = dependencies;
					assetNode.Res.Add(data);
				}
				
				assetsNodes[n] = assetNode;
			}
			
			string eof = DecodeString(ref bytes, ref offset);
			if (!eof.Equals(EOF))
			{
				Debug.LogError("AssetDependencyCache cache file to be corrupted. Rebuilding cache required");
				return new AssetNode[0];
			}

			return assetsNodes;
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