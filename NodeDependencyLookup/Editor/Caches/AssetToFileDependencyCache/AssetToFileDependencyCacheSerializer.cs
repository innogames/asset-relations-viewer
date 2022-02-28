using System.Collections.Generic;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    public class AssetToFileDependencyCacheSerializer
    {
        public const string EOF = "EndOfSerializedAssetToFileDependencyCache";

		public static byte[] Serialize(FileToAssetsMapping[] assetToFileMappings)
		{
			byte[] bytes = new byte[CacheSerializerUtils.ARRAY_SIZE_OFFSET];
			int offset = 0;
			
			CacheSerializerUtils.EncodeShort((short)assetToFileMappings.Length, ref bytes, ref offset);
			
			foreach (FileToAssetsMapping fileToAssetsMapping in assetToFileMappings)
			{
				CacheSerializerUtils.EncodeLong(fileToAssetsMapping.Timestamp, ref bytes, ref offset);
				CacheSerializerUtils.EncodeString(fileToAssetsMapping.FileId, ref bytes, ref offset);
				CacheSerializerUtils.EncodeShort((short)fileToAssetsMapping.FileNodes.Count, ref bytes, ref offset);
				
				foreach (GenericDependencyMappingNode fileNode in fileToAssetsMapping.FileNodes)
				{
					CacheSerializerUtils.EncodeString(fileNode.NodeId, ref bytes, ref offset);
					CacheSerializerUtils.EncodeDependencies(fileNode.Dependencies, ref bytes, ref offset);
				}
			}

			CacheSerializerUtils.EncodeString(EOF, ref bytes, ref offset);

			Deserialize(bytes);
			
			return bytes;
		}
		
		public static FileToAssetsMapping[] Deserialize(byte[] bytes)
		{
			int offset = 0;
			int numAssetToFileNodes = CacheSerializerUtils.DecodeShort(ref bytes, ref offset);
			
			FileToAssetsMapping[] assetToFileMappings = new FileToAssetsMapping[numAssetToFileNodes];

			for (int n = 0; n < numAssetToFileNodes; ++n)
			{
				FileToAssetsMapping mapping = new FileToAssetsMapping();

				mapping.Timestamp = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);
				mapping.FileId = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
				
				int numFileNodes = CacheSerializerUtils.DecodeShort(ref bytes, ref offset);

				mapping.FileNodes = new List<GenericDependencyMappingNode>();

				for (int i = 0; i < numFileNodes; ++i)
				{
					GenericDependencyMappingNode fileNode = new GenericDependencyMappingNode();

					fileNode.NodeId = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
					fileNode.Dependencies = CacheSerializerUtils.DecodeDependencies(ref bytes, ref offset);

					mapping.FileNodes.Add(fileNode);
				}
				
				assetToFileMappings[n] = mapping;
			}
			
			string eof = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
			if (!eof.Equals(EOF))
			{
				Debug.LogError("AssetToFileDependencyCache cache file to be corrupted. Rebuilding cache required");
				return new FileToAssetsMapping[0];
			}

			return assetToFileMappings;
		}
    }
}