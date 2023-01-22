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

			internal bool HasActiveConnectionTypes()
			{
				return ConnectionTypes == null || ConnectionTypes.Count > 0;
			}
		}
		
		internal List<Entry> CacheUsages = new List<Entry>();
		internal Dictionary<string, Entry> ResolverUsagesLookup = new Dictionary<string, Entry>();

		public bool IsCacheActive(Type cacheType)
		{
			foreach (Entry cacheUsage in CacheUsages)
			{
				if (cacheUsage.CacheType == cacheType && cacheUsage.HasActiveConnectionTypes())
				{
					return true;
				}
			}

			return false;
		}

		public CacheUpdateInfo GetUpdateStateForCache(Type cacheType)
		{
			bool load = false;
			bool update = false;
			bool save = false;

			foreach (Entry cacheUsage in CacheUsages)
			{
				if (cacheUsage.CacheType == cacheType && cacheUsage.HasActiveConnectionTypes())
				{
					load |= cacheUsage.Load;
					update |= cacheUsage.Update;
					save |= cacheUsage.Save;
				}
			}

			return new CacheUpdateInfo {Load = load, Update = update, Save = save};
		}

		public CacheUpdateInfo GetUpdateStateForResolver(Type resolverType)
		{
			foreach (Entry cacheUsage in CacheUsages)
			{
				if (cacheUsage.ResolverType == resolverType && cacheUsage.HasActiveConnectionTypes())
				{
					return new CacheUpdateInfo {Load = cacheUsage.Load, Update = cacheUsage.Update, Save = cacheUsage.Save};
				}
			}

			return new CacheUpdateInfo {Load = false, Update = false, Save = false};
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