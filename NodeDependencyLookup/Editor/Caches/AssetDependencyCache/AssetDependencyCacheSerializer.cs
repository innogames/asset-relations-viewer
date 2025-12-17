using System;
using System.Collections.Generic;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Serializer for the AssetDependencyCache
	/// Since all json solutions where to slow and the structure is quite simple it was fastest to manually write it into a byte array
	/// </summary>
	public class AssetDependencyCacheSerializer
	{
		private const string EOF = "EndOfSerializedAssetDependencyCache";

		public static byte[] Serialize(FileToAssetNode[] fileToAssetNodes)
		{
			var bytes = new byte[CacheSerializerUtils.ARRAY_SIZE_OFFSET];
			var offset = 0;

			CacheSerializerUtils.EncodeLong(fileToAssetNodes.Length, ref bytes, ref offset);

			foreach (var fileToAssetNode in fileToAssetNodes)
			{
				CacheSerializerUtils.EncodeString(fileToAssetNode.FileId, ref bytes, ref offset);
				CacheSerializerUtils.EncodeShort((short) fileToAssetNode.ResolverTimeStamps.Count, ref bytes,
					ref offset);

				for (var i = 0; i < fileToAssetNode.ResolverTimeStamps.Count; ++i)
				{
					var resolverTimeStamp = fileToAssetNode.ResolverTimeStamps[i];
					CacheSerializerUtils.EncodeString(resolverTimeStamp.ResolverId, ref bytes, ref offset);
					CacheSerializerUtils.EncodeLong(resolverTimeStamp.TimeStamp, ref bytes, ref offset);
				}

				var assetNodes = fileToAssetNode.GetAssetNodes();

				CacheSerializerUtils.EncodeInt(assetNodes.Count, ref bytes, ref offset);

				for (var j = 0; j < assetNodes.Count; ++j)
				{
					var assetNode = assetNodes[j];
					CacheSerializerUtils.EncodeString(assetNode.Id, ref bytes, ref offset);

					CacheSerializerUtils.EncodeShort((short) assetNode.ResolverDatas.Count, ref bytes, ref offset);

					for (var i = 0; i < assetNode.ResolverDatas.Count; i++)
					{
						var resolverData = assetNode.ResolverDatas[i];

						CacheSerializerUtils.EncodeString(resolverData.ResolverId, ref bytes, ref offset);
						CacheSerializerUtils.EncodeDependencies(resolverData.Dependencies, ref bytes, ref offset);

						bytes = CacheSerializerUtils.EnsureSize(bytes, offset);
					}
				}
			}

			CacheSerializerUtils.EncodeString(EOF, ref bytes, ref offset);

			return bytes;
		}

		public static FileToAssetNode[] Deserialize(byte[] bytes)
		{
			var offset = 0;
			var numFileToAssetNodes = (int) CacheSerializerUtils.DecodeLong(ref bytes, ref offset);

			var fileToAssetNodes = new FileToAssetNode[numFileToAssetNodes];

			for (var n = 0; n < numFileToAssetNodes; ++n)
			{
				var fileId = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
				var fileAssetNode = new FileToAssetNode {FileId = fileId};
				int resolverTimeStampLength = CacheSerializerUtils.DecodeShort(ref bytes, ref offset);

				for (var i = 0; i < resolverTimeStampLength; i++)
				{
					var resolverTimeStamp = new FileToAssetNode.ResolverTimeStamp();
					resolverTimeStamp.ResolverId = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
					resolverTimeStamp.TimeStamp = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);
					fileAssetNode.ResolverTimeStamps.Add(resolverTimeStamp);
				}

				var numAssetNodes = CacheSerializerUtils.DecodeInt(ref bytes, ref offset);
				fileAssetNode.Init(numAssetNodes);

				for (var i = 0; i < numAssetNodes; i++)
				{
					var assetId = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
					var assetNode = new AssetNode(assetId);
					int numResolverDatas = CacheSerializerUtils.DecodeShort(ref bytes, ref offset);

					for (var j = 0; j < numResolverDatas; ++j)
					{
						var data = new AssetNode.ResolverData();

						data.ResolverId = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
						data.Dependencies = CacheSerializerUtils.DecodeDependencies(ref bytes, ref offset);
						assetNode.ResolverDatas.Add(data);
					}

					fileAssetNode.AddAssetNode(assetNode);
				}

				fileToAssetNodes[n] = fileAssetNode;
			}

			var eof = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
			if (!eof.Equals(EOF, StringComparison.Ordinal))
			{
				Debug.LogError("AssetDependencyCache cache file to be corrupted. Rebuilding cache required");
				return Array.Empty<FileToAssetNode>();
			}

			return fileToAssetNodes;
		}
	}
}