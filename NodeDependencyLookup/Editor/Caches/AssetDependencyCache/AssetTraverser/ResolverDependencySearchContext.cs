using System.Collections.Generic;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Search context for the AssetTraverser to avoid passing
	/// </summary>
	public class ResolverDependencySearchContext
	{
		public Object Asset;
		public string AssetId = string.Empty;
		public List<IAssetDependencyResolver> Resolvers;

		public Dictionary<IAssetDependencyResolver, List<Dependency>> ResolverDependencies = new Dictionary<IAssetDependencyResolver, List<Dependency>>();

		public ResolverDependencySearchContext Set(Object asset, string assetId,
			List<IAssetDependencyResolver> resolvers)
		{
			Asset = asset;
			AssetId = assetId;
			Resolvers = resolvers;
			ResolverDependencies.Clear();
			foreach (var resolver in resolvers)
			{
				ResolverDependencies.Add(resolver, new List<Dependency>());
			}

			return this;
		}

		public void AddDependency(IAssetDependencyResolver resolver, Dependency dependency)
		{
			if (dependency.Id == AssetId)
			{
				// Don't add self dependency
				return;
			}

			ResolverDependencies[resolver].Add(dependency);
		}
	}
}
