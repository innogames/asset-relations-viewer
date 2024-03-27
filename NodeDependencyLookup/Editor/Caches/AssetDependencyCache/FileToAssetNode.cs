using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Stores dependencies from a File to all Assets it contains
	/// </summary>
	public class FileToAssetNode : IIdentifyable
	{
		public class ResolverTimeStamp
		{
			public string ResolverId;
			public long TimeStamp;
		}

		public string FileId;
		public List<AssetNode> AssetNodes;

		public string Id => FileId;

		public readonly List<ResolverTimeStamp> ResolverTimeStamps = new List<ResolverTimeStamp>(4);

		public AssetNode GetAssetNode(string id)
		{
			foreach (var assetNode in AssetNodes)
			{
				if (assetNode.Id == id)
				{
					return assetNode;
				}
			}

			var newAssetNode = new AssetNode(id);
			AssetNodes.Add(newAssetNode);
			return newAssetNode;
		}

		public ResolverTimeStamp GetResolverTimeStamp(string id)
		{
			foreach (var resolverTimeStamp in ResolverTimeStamps)
			{
				if (resolverTimeStamp.ResolverId == id)
				{
					return resolverTimeStamp;
				}
			}

			var newTimestamp = new ResolverTimeStamp {ResolverId = id};
			ResolverTimeStamps.Add(newTimestamp);

			return newTimestamp;
		}
	}
}