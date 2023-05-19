using System.Collections.Generic;
using System.Linq;
using Assets.Package.Editor.DependencyResolvers;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/**
	 * The CacheStateContext holds all information after the Caches updates
	 * This is to reduce the amount of parameters needing to be passed to the functions because most of the time they need all member values
	 */
	public class NodeDependencyLookupContext
	{
		public Dictionary<string, Node> nodeDictionary = new Dictionary<string, Node>();

		private static Dictionary<string, NodeDependencyLookupContext> m_stateContexts = new Dictionary<string, NodeDependencyLookupContext>();

		public RelationLookup.RelationsLookup RelationsLookup = new RelationLookup.RelationsLookup();
		public Dictionary<string, INodeHandler> NodeHandlerLookup = new Dictionary<string, INodeHandler>();
		public DependencyTypeLookup DependencyTypeLookup;
		public Dictionary<string, CreatedDependencyCache> CreatedCaches = new Dictionary<string, CreatedDependencyCache>();

		public static NodeDependencyLookupContext GetStateContextForName(string name)
		{
			if(!m_stateContexts.ContainsKey(name))
				m_stateContexts.Add(name, new NodeDependencyLookupContext());

			return m_stateContexts[name];
		}

		public NodeDependencyLookupContext()
		{
			NodeHandlerLookup = NodeDependencyLookupUtility.BuildNodeHandlerLookup();
			//Reset();
		}

		public static void ResetContexts()
		{
			m_stateContexts.Clear();
		}

		public void Reset()
		{
			RelationsLookup = new RelationLookup.RelationsLookup();
			//NodeHandlerLookup = NodeDependencyLookupUtility.BuildNodeHandlerLookup();
			DependencyTypeLookup = null;
		}

		public void UpdateFromDefinition(ResolverUsageDefinitionList definitionList)
		{
			foreach (ResolverUsageDefinitionList.Entry entry in definitionList.CacheUsages)
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

			DependencyTypeLookup = new DependencyTypeLookup(GetCaches());
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