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
		public List<AssetNode> AssetNodes = new List<AssetNode>();

		public string Id => FileId;

		public List<ResolverTimeStamp> ResolverTimeStamps = new List<ResolverTimeStamp>();


		public AssetNode GetAssetNode(string id)
		{
			foreach (AssetNode assetNode in AssetNodes)
			{
				if (assetNode.Id == id)
				{
					return assetNode;
				}
			}

			AssetNode newAssetNode = new AssetNode(id){Existing = true};
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
	public class AssetNode : IResolvedNode
	{
		public class ResolverData
		{
			public string ResolverId;
			public Dependency[] Dependencies = new Dependency[0];
		}

		public string AssetId;
		
		public string Id{get { return AssetId; }}
		public string Type{get { return "Asset"; }}
		public bool Existing { get; set; }
		
		public List<ResolverData> ResolverDatas = new List<ResolverData>();

		public AssetNode(string assetId)
		{
			AssetId = assetId;
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
					if (dependencyCache.ConnectionTypes.Contains(dependency.ConnectionType))
					{
						result.Add(dependency);
					}
				}
			}

			return result;
		}
	}
}