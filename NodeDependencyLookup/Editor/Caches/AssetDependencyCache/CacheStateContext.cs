using System.Collections.Generic;
using System.Linq;
using Assets.Package.Editor.DependencyResolvers;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/**
	 * The CacheStateContext holds all information after the Caches updates
	 * This is to reduce the amount of parameters needing to be passed to the functions because most of the time they need all member values
	 */
	public class CacheStateContext
	{
		private static Dictionary<string, CacheStateContext> m_stateContexts = new Dictionary<string, CacheStateContext>();
		
		public RelationLookup.RelationsLookup RelationsLookup = new RelationLookup.RelationsLookup();
		public Dictionary<string, INodeHandler> NodeHandlerLookup = new Dictionary<string, INodeHandler>();
		public ConnectionTypeLookup ConnectionTypeLookup;
		public Dictionary<string, CreatedDependencyCache> CreatedCaches = new Dictionary<string, CreatedDependencyCache>();
		
		public static CacheStateContext GetStateContextForName(string name)
		{
			if(!m_stateContexts.ContainsKey(name))
				m_stateContexts.Add(name, new CacheStateContext());

			return m_stateContexts[name];
		}

		public static void ResetContexts()
		{
			m_stateContexts.Clear();
		}
		
		public void Reset()
		{
			RelationsLookup = new RelationLookup.RelationsLookup();
			NodeHandlerLookup.Clear();
			ConnectionTypeLookup = null;
		}
		
		public void UpdateFromDefinition(CacheUsageDefinitionList definitionList)
		{
			ResetCacheUsages();
			
			foreach (CacheUsageDefinitionList.Entry entry in definitionList.CacheUsages)
			{
				string cacheTypeFullName = entry.CacheType.FullName;
				
				if (!CreatedCaches.ContainsKey(cacheTypeFullName))
				{
					IDependencyCache cache = NodeDependencyLookupUtility.InstantiateClass<IDependencyCache>(entry.CacheType);
					CreatedDependencyCache createdCache = new CreatedDependencyCache(cache);

					CreatedCaches.Add(cacheTypeFullName, createdCache);
				}

				CreatedCaches[cacheTypeFullName].AddResolver(entry.ResolverType, entry.ConnectionTypes);
			}
			
			ConnectionTypeLookup = new ConnectionTypeLookup(GetCaches());
			NodeHandlerLookup = NodeDependencyLookupUtility.BuildNodeHandlerLookup();
		}

		public void ResetCacheUsages()
		{
			foreach (KeyValuePair<string,CreatedDependencyCache> pair in CreatedCaches)
			{
				pair.Value.ResetLookups();
			}
		}

		public List<CreatedDependencyCache> GetCaches()
		{
			return CreatedCaches.Values.ToList();
		}
	}
}