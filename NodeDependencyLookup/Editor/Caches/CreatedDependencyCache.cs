using System;
using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// During the dependency cache update this stores the current state of the <see cref="IDependencyCache"/>
	/// </summary>
	public class CreatedDependencyCache
	{
		public CreatedDependencyCache(IDependencyCache cache)
		{
			Cache = cache;
			cache.Initialize(this);
		}

		public bool IsLoaded = false;
		public readonly IDependencyCache Cache;
		public readonly List<CreatedResolver> ResolverUsages = new List<CreatedResolver>();

		public readonly Dictionary<string, CreatedResolver> ResolverUsagesLookup =
			new Dictionary<string, CreatedResolver>();

		public readonly Dictionary<string, CreatedResolver>
			CreatedResolvers = new Dictionary<string, CreatedResolver>();

		public void AddResolver(Type resolverType, List<string> dependencyTypes)
		{
			var resolverTypeFullName = resolverType.FullName;

			if (!CreatedResolvers.ContainsKey(resolverTypeFullName))
			{
				var dependencyResolver =
					NodeDependencyLookupUtility.InstantiateClass<IDependencyResolver>(resolverType);

				var resolver = new CreatedResolver(dependencyResolver);
				CreatedResolvers.Add(resolverTypeFullName, resolver);
			}

			var createdResolver = CreatedResolvers[resolverTypeFullName];
			var resolverId = createdResolver.Resolver.GetId();

			if (!ResolverUsagesLookup.ContainsKey(resolverId))
			{
				ResolverUsages.Add(createdResolver);
				ResolverUsagesLookup.Add(resolverId, createdResolver);
			}

			var collection = dependencyTypes != null
				? dependencyTypes.ToArray()
				: createdResolver.Resolver.GetDependencyTypes();

			createdResolver.DependencyTypes = new List<string>(collection);
			createdResolver.IsActive = true;
		}

		public void ResetUsages()
		{
			ResolverUsages.Clear();
			ResolverUsagesLookup.Clear();

			foreach (var pair in CreatedResolvers)
			{
				pair.Value.IsActive = false;
			}
		}
	}
}