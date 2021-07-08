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

		public static byte[] Serialize(FileToAssetNode[] fileToAssetNodes)
		{
			byte[] bytes = new byte[CacheSerializerUtils.ARRAY_SIZE_OFFSET];
			int offset = 0;
			
			CacheSerializerUtils.EncodeShort((short)fileToAssetNodes.Length, ref bytes, ref offset);

			foreach (FileToAssetNode fileToAssetNode in fileToAssetNodes)
			{
				CacheSerializerUtils.EncodeString(fileToAssetNode.FileId, ref bytes, ref offset);
				CacheSerializerUtils.EncodeShort((short)fileToAssetNode.ResolverTimeStamps.Count, ref bytes, ref offset);

				for (int i = 0; i < fileToAssetNode.ResolverTimeStamps.Count; ++i)
				{
					FileToAssetNode.ResolverTimeStamp resolverTimeStamp = fileToAssetNode.ResolverTimeStamps[i];
					CacheSerializerUtils.EncodeString(resolverTimeStamp.ResolverId, ref bytes, ref offset);
					CacheSerializerUtils.EncodeLong(resolverTimeStamp.TimeStamp, ref bytes, ref offset);
				}
				
				CacheSerializerUtils.EncodeShort((short)fileToAssetNode.AssetNodes.Count, ref bytes, ref offset);

				for (int j = 0; j < fileToAssetNode.AssetNodes.Count; ++j)
				{
					AssetNode assetNode = fileToAssetNode.AssetNodes[j];
					CacheSerializerUtils.EncodeString(assetNode.Id, ref bytes, ref offset);
					
					CacheSerializerUtils.EncodeShort((short)assetNode.ResolverDatas.Count, ref bytes, ref offset);

					for (var i = 0; i < assetNode.ResolverDatas.Count; i++)
					{
						AssetNode.ResolverData resolverData = assetNode.ResolverDatas[i];

						CacheSerializerUtils.EncodeString(resolverData.ResolverId, ref bytes, ref offset);
						CacheSerializerUtils.EncodeDependencies(resolverData.Dependencies, ref bytes, ref offset);

						bytes = CacheSerializerUtils.EnsureSize(bytes, offset);
					}
				}
			}
			
			CacheSerializerUtils.EncodeString(EOF, ref bytes, ref offset);

			Deserialize(bytes);
			
			return bytes;
		}
		
		public static FileToAssetNode[] Deserialize(byte[] bytes)
		{
			int offset = 0;
			int numFileToAssetNodes = CacheSerializerUtils.DecodeShort(ref bytes, ref offset);
			
			FileToAssetNode[] fileToAssetNodes = new FileToAssetNode[numFileToAssetNodes];

			for (int n = 0; n < numFileToAssetNodes; ++n)
			{
				string fileId = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
				FileToAssetNode fileAssetNode = new FileToAssetNode{FileId = fileId};
				int resolverTimeStampLength = CacheSerializerUtils.DecodeShort(ref bytes, ref offset);

				for (var i = 0; i < resolverTimeStampLength; i++)
				{
					FileToAssetNode.ResolverTimeStamp resolverTimeStamp = new FileToAssetNode.ResolverTimeStamp();
					resolverTimeStamp.ResolverId = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
					resolverTimeStamp.TimeStamp = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);
					fileAssetNode.ResolverTimeStamps.Add(resolverTimeStamp);
				}

				int numAssetNodes = CacheSerializerUtils.DecodeShort(ref bytes, ref offset);

				for (var i = 0; i < numAssetNodes; i++)
				{
					string assetId = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
					AssetNode assetNode = new AssetNode(assetId);

					int numResolverDatas = CacheSerializerUtils.DecodeShort(ref bytes, ref offset);

					for (int j = 0; j < numResolverDatas; ++j)
					{
						AssetNode.ResolverData data = new AssetNode.ResolverData();
						
						data.ResolverId = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
						data.Dependencies = CacheSerializerUtils.DecodeDependencies(ref bytes, ref offset);
						
						assetNode.ResolverDatas.Add(data);
					}

					fileAssetNode.AssetNodes.Add(assetNode);
				}
				
				fileToAssetNodes[n] = fileAssetNode;
			}
			
			string eof = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
			if (!eof.Equals(EOF))
			{
				Debug.LogError("AssetDependencyCache cache file to be corrupted. Rebuilding cache required");
				return new FileToAssetNode[0];
			}

			return fileToAssetNodes;
		}
	}
}