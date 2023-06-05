using System;
using System.Collections.Generic;
using System.Linq;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class CreatedDependencyCache
	{
		public CreatedDependencyCache(IDependencyCache cache)
		{
			Cache = cache;
			cache.Initialize(this);
		}

		public bool IsLoaded = false;
		public IDependencyCache Cache;
		public List<CreatedResolver> ResolverUsages = new List<CreatedResolver>();
		public Dictionary<string, CreatedResolver> ResolverUsagesLookup = new Dictionary<string, CreatedResolver>();
		public Dictionary<string, CreatedResolver> CreatedResolvers = new Dictionary<string, CreatedResolver>();

		public void AddResolver(Type resolverType, List<string> dependencyTypes)
		{
			string resolverTypeFullName = resolverType.FullName;

			if (!CreatedResolvers.ContainsKey(resolverTypeFullName))
			{
				IDependencyResolver dependencyResolver = NodeDependencyLookupUtility.InstantiateClass<IDependencyResolver>(resolverType);
				CreatedResolver resolver = new CreatedResolver(dependencyResolver);
				CreatedResolvers.Add(resolverTypeFullName, resolver);
			}

			CreatedResolver createdResolver = CreatedResolvers[resolverTypeFullName];
			string resolverId = createdResolver.Resolver.GetId();

			if (!ResolverUsagesLookup.ContainsKey(resolverId))
			{
				ResolverUsages.Add(createdResolver);
				ResolverUsagesLookup.Add(resolverId, createdResolver);
			}

			string[] collection = dependencyTypes != null ? dependencyTypes.ToArray() : createdResolver.Resolver.GetDependencyTypes();
			createdResolver.DependencyTypes = new List<string>(collection);
			createdResolver.IsActive = true;
		}

		public void ResetLookups()
		{
			ResolverUsages.Clear();
			ResolverUsagesLookup.Clear();

			foreach (KeyValuePair<string,CreatedResolver> pair in CreatedResolvers)
			{
				pair.Value.IsActive = false;
			}
		}
	}

	public class CreatedResolver
	{
		public CreatedResolver(IDependencyResolver resolver)
		{
			Resolver = resolver;
		}

		public bool IsActive;
		public List<string> DependencyTypes = new List<string>();
		public IDependencyResolver Resolver;
	}
}