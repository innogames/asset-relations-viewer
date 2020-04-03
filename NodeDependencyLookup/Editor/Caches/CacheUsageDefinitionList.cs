using System;
using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	///<summary>
	///List to store which resolvers will be used when creating the dependency structure.
	///Only the given resolvers will be executed instead of executing all available ones
	///</summary>
	public class CacheUsageDefinitionList
	{
		internal class Entry
		{
			public bool IsActive = true;
			public readonly Type CacheType;
			public readonly Type ResolverType;
			public List<string> ConnectionTypes;

			// Will add all resolved types to be active
			internal Entry(Type cacheType, Type resolverType, List<string> connectionTypes)
			{
				CacheType = cacheType;
				ResolverType = resolverType;
				ConnectionTypes = connectionTypes;
			}
		}
		
		internal List<Entry> CacheUsages = new List<Entry>();
		internal Dictionary<string, Entry> ResolverUsagesLookup = new Dictionary<string, Entry>();

		public void Add<C, R>(List<string> connectionTypes = null) where C : IDependencyCache where R : IDependencyResolver
		{
			Add(typeof(C), typeof(R), connectionTypes);
		}

		public void Add(Type cacheType, Type resolverType, List<string> connectionTypes = null)
		{
			Entry entry = new Entry(cacheType, resolverType, connectionTypes);
			ResolverUsagesLookup[resolverType.FullName] = entry;
			CacheUsages.Add(entry);
		}
	}
}