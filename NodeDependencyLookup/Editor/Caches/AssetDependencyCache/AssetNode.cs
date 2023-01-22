using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
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

		public List<ResolverTimeStamp> ResolverTimeStamps = new List<ResolverTimeStamp>(4);


		public AssetNode GetAssetNode(string id)
		{
			foreach (AssetNode assetNode in AssetNodes)
			{
				if (assetNode.Id == id)
				{
					return assetNode;
				}
			}

			AssetNode newAssetNode = new AssetNode(id);
			AssetNodes.Add(newAssetNode);
			return newAssetNode;
		}
		
		public ResolverTimeStamp GetResolverTimeStamp(string id)
		{
			foreach (ResolverTimeStamp resolverTimeStamp in ResolverTimeStamps)
			{
				if (resolverTimeStamp.ResolverId == id)
				{
					return resolverTimeStamp;
				}
			}

			ResolverTimeStamp newTimestamp = new ResolverTimeStamp{ResolverId = id};
			ResolverTimeStamps.Add(newTimestamp);

			return newTimestamp;
		}
	}
	
	/**
	 * Stores a relation and contains a list of dependency nodes and a list of referencer nodes
	 */
	public class AssetNode : IDependencyMappingNode
	{
		public class ResolverData
		{
			public string ResolverId;
			public List<Dependency> Dependencies;
		}

		private string AssetId;
		private string KeyId;
		
		public string Id => AssetId;
		public string Type => AssetNodeType.Name;
		public string Key => KeyId;

		public List<ResolverData> ResolverDatas = new List<ResolverData>();

		public AssetNode(string assetId)
		{
			AssetId = assetId;
			KeyId = NodeDependencyLookupUtility.GetNodeKey(AssetId, Type);
		}

		public ResolverData GetResolverData(string id)
		{
			foreach (ResolverData resolverData in ResolverDatas)
			{
				if (resolverData.ResolverId == id)
				{
					return resolverData;
				}
			}

			ResolverData newResolver = new ResolverData();
			newResolver.ResolverId = id;

			ResolverDatas.Add(newResolver);

			return newResolver;
		}

		public List<Dependency> GetDependenciesForResolverUsages(Dictionary<string, CreatedResolver> resolverUsages)
		{
			List<Dependency> result = new List<Dependency>();

			foreach (ResolverData data in ResolverDatas)
			{
				if (!resolverUsages.ContainsKey(data.ResolverId))
				{
					continue;
				}

				CreatedResolver dependencyCache = resolverUsages[data.ResolverId];

				foreach (Dependency dependency in data.Dependencies)
				{
					if (dependencyCache.DependencyTypes.Contains(dependency.DependencyType))
					{
						result.Add(dependency);
					}
				}
			}

			return result;
		}
	}
}