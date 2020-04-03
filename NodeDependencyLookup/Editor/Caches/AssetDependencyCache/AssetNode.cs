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
			public Dependency[] Dep = new Dependency[0];
		}

		public string Guid;
		
		public string Id{get { return Guid; }}
		public string Type{get { return "Asset"; }}
		public bool Existing { get; set; }
		
		public List<ResolverData> Res = new List<ResolverData>();

		public AssetNode(string guid)
		{
			Guid = guid;
		}

		public ResolverData GetResolverData(string id)
		{
			foreach (ResolverData resolverData in Res)
			{
				if (resolverData.Id == id)
				{
					return resolverData;
				}
			}

			ResolverData newResolver = new ResolverData();
			newResolver.Id = id;

			Res.Add(newResolver);

			return newResolver;
		}

		public List<Dependency> GetDependenciesForResolverUsages(Dictionary<string, CreatedResolver> resolverUsages)
		{
			List<Dependency> result = new List<Dependency>();

			foreach (ResolverData data in Res)
			{
				if (!resolverUsages.ContainsKey(data.Id))
				{
					continue;
				}

				CreatedResolver dependencyCache = resolverUsages[data.Id];

				foreach (Dependency dependency in data.Dep)
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