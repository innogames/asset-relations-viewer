using System;
using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	///<summary>
	/// List to store which resolvers will be used when creating the dependency structure.
	/// Only the given resolvers will be executed instead of executing all available ones
	///</summary>
	public class ResolverUsageDefinitionList
	{
		internal class Entry
		{
			public readonly Type CacheType;
			public readonly Type ResolverType;
			public readonly List<string> ConnectionTypes;
			public readonly bool Load;
			public readonly bool Update;
			public readonly bool Save;

			/// <summary>
			/// Will add all resolved types to be active
			/// </summary>
			internal Entry(Type cacheType, Type resolverType, List<string> connectionTypes, bool load, bool update,
				bool save)
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

		internal readonly List<Entry> CacheUsages = new List<Entry>();

		public bool IsCacheActive(Type cacheType)
		{
			foreach (var cacheUsage in CacheUsages)
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
			var load = false;
			var update = false;
			var save = false;

			foreach (var cacheUsage in CacheUsages)
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
			foreach (var cacheUsage in CacheUsages)
			{
				if (cacheUsage.ResolverType == resolverType && cacheUsage.HasActiveConnectionTypes())
				{
					return new CacheUpdateInfo
						{Load = cacheUsage.Load, Update = cacheUsage.Update, Save = cacheUsage.Save};
				}
			}

			return new CacheUpdateInfo {Load = false, Update = false, Save = false};
		}

		public void Add<C, R>(bool load = true, bool update = true, bool save = true,
			List<string> connectionTypes = null) where C : IDependencyCache where R : IDependencyResolver
		{
			Add(typeof(C), typeof(R), load, update, save, connectionTypes);
		}

		public void Add(Type cacheType, Type resolverType, bool load = true, bool update = true, bool save = true,
			List<string> connectionTypes = null)
		{
			var entry = new Entry(cacheType, resolverType, connectionTypes, load, update, save);
			CacheUsages.Add(entry);
		}
	}
}