using System.Collections.Generic;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class AssetToFileDependencyCacheSerializer
	{
		public const string EOF = "EndOfSerializedAssetToFileDependencyCache";

		public static byte[] Serialize(FileToAssetsMapping[] assetToFileMappings)
		{
			var bytes = new byte[CacheSerializerUtils.ARRAY_SIZE_OFFSET];
			var offset = 0;

			CacheSerializerUtils.EncodeLong(assetToFileMappings.Length, ref bytes, ref offset);

			foreach (var fileToAssetsMapping in assetToFileMappings)
			{
				CacheSerializerUtils.EncodeLong(fileToAssetsMapping.Timestamp, ref bytes, ref offset);
				CacheSerializerUtils.EncodeString(fileToAssetsMapping.FileId, ref bytes, ref offset);
				CacheSerializerUtils.EncodeShort((short) fileToAssetsMapping.FileNodes.Count, ref bytes, ref offset);

				foreach (var fileNode in fileToAssetsMapping.FileNodes)
				{
					CacheSerializerUtils.EncodeString(fileNode.NodeId, ref bytes, ref offset);
					CacheSerializerUtils.EncodeString(fileNode.NodeType, ref bytes, ref offset);
					CacheSerializerUtils.EncodeDependencies(fileNode.Dependencies, ref bytes, ref offset);
				}

				bytes = CacheSerializerUtils.EnsureSize(bytes, offset);
			}

			CacheSerializerUtils.EncodeString(EOF, ref bytes, ref offset);

			return bytes;
		}

		public static FileToAssetsMapping[] Deserialize(byte[] bytes)
		{
			var offset = 0;
			var numAssetToFileNodes = (int) CacheSerializerUtils.DecodeLong(ref bytes, ref offset);

			var assetToFileMappings = new FileToAssetsMapping[numAssetToFileNodes];

			for (var n = 0; n < numAssetToFileNodes; ++n)
			{
				var mapping = new FileToAssetsMapping();

				mapping.Timestamp = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);
				mapping.FileId = CacheSerializerUtils.DecodeString(ref bytes, ref offset);

				int numFileNodes = CacheSerializerUtils.DecodeShort(ref bytes, ref offset);

				mapping.FileNodes = new List<GenericDependencyMappingNode>(numFileNodes);

				for (var i = 0; i < numFileNodes; ++i)
				{
					var fileNode = new GenericDependencyMappingNode(
						CacheSerializerUtils.DecodeString(ref bytes, ref offset),
						CacheSerializerUtils.DecodeString(ref bytes, ref offset));
					fileNode.Dependencies = CacheSerializerUtils.DecodeDependencies(ref bytes, ref offset);

					mapping.FileNodes.Add(fileNode);
				}

				assetToFileMappings[n] = mapping;
			}

			var eof = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
			if (!eof.Equals(EOF))
			{
				Debug.LogError("AssetToFileDependencyCache cache file to be corrupted. Rebuilding cache required");
				return new FileToAssetsMapping[0];
			}

			return assetToFileMappings;
		}
	}
}