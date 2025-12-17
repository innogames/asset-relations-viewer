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
		private List<AssetNode> assetNodes;
		private Dictionary<string, AssetNode> assetNodesLookup;

		public string Id => FileId;

		public readonly List<ResolverTimeStamp> ResolverTimeStamps = new(4);

		public void Init(int assetNodeCount)
		{
			assetNodes = new List<AssetNode>(assetNodeCount);
			assetNodesLookup = new Dictionary<string, AssetNode>(assetNodeCount);
		}

		public List<AssetNode> GetAssetNodes()
		{
			return assetNodes;
		}

		public AssetNode GetOrCreateAssetNode(string id)
		{
			if (assetNodesLookup.TryGetValue(id, out var node))
			{
				return node;
			}

			var newAssetNode = new AssetNode(id);
			AddAssetNode(newAssetNode);
			return newAssetNode;
		}

		public void AddAssetNode(AssetNode assetNode)
		{
			assetNodes.Add(assetNode);
			assetNodesLookup.Add(assetNode.Id, assetNode);
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