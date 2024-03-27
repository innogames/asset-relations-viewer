using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Stores a relation and contains a list of dependency nodes and a list of referencer nodes
	/// </summary>
	public class AssetNode : IDependencyMappingNode
	{
		public class ResolverData
		{
			public string ResolverId;
			public List<Dependency> Dependencies;
		}

		public string Id { get; }
		public string Key { get; }
		public string Type => AssetNodeType.Name;

		public readonly List<ResolverData> ResolverDatas = new List<ResolverData>(2);

		public AssetNode(string assetId)
		{
			Id = assetId;
			Key = NodeDependencyLookupUtility.GetNodeKey(Id, Type);
		}

		public ResolverData GetResolverData(string id)
		{
			foreach (var resolverData in ResolverDatas)
			{
				if (resolverData.ResolverId == id)
				{
					return resolverData;
				}
			}

			var newResolver = new ResolverData();
			newResolver.ResolverId = id;

			ResolverDatas.Add(newResolver);

			return newResolver;
		}

		public List<Dependency> GetDependenciesForResolverUsages(Dictionary<string, CreatedResolver> resolverUsages)
		{
			var result = new List<Dependency>();

			foreach (var data in ResolverDatas)
			{
				if (!resolverUsages.ContainsKey(data.ResolverId))
				{
					continue;
				}

				var dependencyCache = resolverUsages[data.ResolverId];

				foreach (var dependency in data.Dependencies)
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