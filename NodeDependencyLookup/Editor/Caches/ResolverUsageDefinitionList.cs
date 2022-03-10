using System;
using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	///<summary>
	///List to store which resolvers will be used when creating the dependency structure.
	///Only the given resolvers will be executed instead of executing all available ones
	///</summary>
	public class ResolverUsageDefinitionList
	{
		internal class Entry
		{
			public readonly Type CacheType;
			public readonly Type ResolverType;
			public List<string> ConnectionTypes;
			public bool Load;
			public bool Update;
			public bool Save;

			// Will add all resolved types to be active
			internal Entry(Type cacheType, Type resolverType, List<string> connectionTypes, bool load, bool update, bool save)
			{
				CacheType = cacheType;
				ResolverType = resolverType;
				ConnectionTypes = connectionTypes;
				Load = load;
				Update = update;
				Save = save;
			}
		}
		
		internal List<Entry> CacheUsages = new List<Entry>();
		internal Dictionary<string, Entry> ResolverUsagesLookup = new Dictionary<string, Entry>();

		public void GetUpdateStateForCache(Type cacheType, out bool load, out bool update, out bool save)
		{
			load = false;
			update = false;
			save = false;

			foreach (Entry cacheUsage in CacheUsages)
			{
				if (cacheUsage.CacheType == cacheType)
				{
					load |= cacheUsage.Load;
					update |= cacheUsage.Update;
					save |= cacheUsage.Save;
				}
			}
		}
		
		public void GetUpdateStateForResolver(Type resolverType, out bool load, out bool update, out bool save, out bool unload)
		{
			load = false;
			update = false;
			save = false;
			unload = false;
			
			foreach (Entry cacheUsage in CacheUsages)
			{
				if (cacheUsage.ResolverType == resolverType)
				{
					load |= cacheUsage.Load;
					update |= cacheUsage.Update;
					save |= cacheUsage.Save;
				}
			}
		}

		public void Add<C, R>(bool load = true, bool update = true, bool save = true, List<string> connectionTypes = null) where C : IDependencyCache where R : IDependencyResolver
		{
			Add(typeof(C), typeof(R), load, update, save, connectionTypes);
		}

		public void Add(Type cacheType, Type resolverType, bool load = true, bool update = true, bool save = true, List<string> connectionTypes = null)
		{
			Entry entry = new Entry(cacheType, resolverType, connectionTypes, load, update, save);
			ResolverUsagesLookup[resolverType.FullName] = entry;
			CacheUsages.Add(entry);
		}
	}
}