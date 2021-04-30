using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/**
	 * Stores a relation and contains a list of dependency nodes and a list of referencer nodes
	 */
	public class AssetNode : IResolvedNode
	{
		public class ResolverData
		{
			public string Id;
			public long TimeStamp;
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
				if (resolverData.Id == id)
				{
					return resolverData;
				}
			}

			ResolverData newResolver = new ResolverData();
			newResolver.Id = id;

			ResolverDatas.Add(newResolver);

			return newResolver;
		}

		public List<Dependency> GetDependenciesForResolverUsages(Dictionary<string, CreatedResolver> resolverUsages)
		{
			List<Dependency> result = new List<Dependency>();

			foreach (ResolverData data in ResolverDatas)
			{
				if (!resolverUsages.ContainsKey(data.Id))
				{
					continue;
				}

				CreatedResolver dependencyCache = resolverUsages[data.Id];

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