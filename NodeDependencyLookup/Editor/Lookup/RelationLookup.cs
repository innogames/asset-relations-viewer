using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Static class containing Helper methods for the internal <see cref="RelationsLookup"/> class
	/// </summary>
	public static class RelationLookup
	{
		/// <summary>
		/// Contains the created RelationsLookup created by the <see cref="NodeDependencyLookupContext"/>
		/// Contains methods to either get all found nodes <see cref="GetAllNodes"/>
		/// or explicitly search for one specific node <see cref="GetNode"/>
		/// </summary>
		public class RelationsLookup
		{
			private Dictionary<string, Node> _lookup = new Dictionary<string, Node>();

			public IEnumerator Build(NodeDependencyLookupContext stateContext, List<CreatedDependencyCache> caches,
				Dictionary<string, Node> nodeDictionary, bool fastUpdate, bool updateData)
			{
				yield return CreateRelationMapping(stateContext, caches, nodeDictionary, fastUpdate, updateData);
				_lookup = nodeDictionary;
			}

			public List<Node> GetAllNodes() => _lookup.Values.ToList();

			public Node GetNode(string id, string type) => GetNode(NodeDependencyLookupUtility.GetNodeKey(id, type));

			private Node GetNode(string key)
			{
				if (_lookup.TryGetValue(key, out var node))
				{
					return node;
				}

				return null;
			}
		}

		public static IEnumerator GetAssetToFileLookup(CacheUpdateSettings cacheUpdateSettings,
			CacheUpdateInfo updateInfo, RelationsLookup relationsLookup)
		{
			var context = new NodeDependencyLookupContext(cacheUpdateSettings);
			context.RelationsLookup = relationsLookup;
			var resolverList = new ResolverUsageDefinitionList();
			resolverList.Add<AssetToFileDependencyCache, AssetToFileDependencyResolver>(true, updateInfo.Update,
				updateInfo.Save);

			yield return NodeDependencyLookupUtility.LoadDependencyLookupForCachesAsync(context, resolverList);
		}

		public static Dictionary<string, T> ConvertToDictionary<T>(T[] entries) where T : IIdentifyable
		{
			var list = new Dictionary<string, T>();

			foreach (var entry in entries)
			{
				list[entry.Id] = entry;
			}

			return list;
		}

		private static IEnumerator CreateRelationMapping(NodeDependencyLookupContext stateContext,
			List<CreatedDependencyCache> dependencyCaches, Dictionary<string, Node> nodeDictionary, bool isFastUpdate,
			bool updateNodeData)
		{
			var resolvedNodes = new List<IDependencyMappingNode>(16 * 1024);
			var cacheUpdateSettings = stateContext.CacheUpdateSettings;

			foreach (var dependencyCache in dependencyCaches)
			{
				var cache = dependencyCache.Cache;
				resolvedNodes.Clear();

				cache.AddExistingNodes(resolvedNodes);
				cache.InitLookup();

				var k = 0;
				var c = 0;

				var cacheUpdateResourcesCleaner = new CacheUpdateResourcesCleaner();

				// create dependency structure here
				foreach (var resolvedNode in resolvedNodes)
				{
					var percentageDone = (float)k / resolvedNodes.Count;
					var node = GetOrCreateNode(resolvedNode.Id, resolvedNode.Type, resolvedNode.Key, nodeDictionary,
						stateContext, updateNodeData, out var nodeCached);

					var dependenciesForId = dependencyCache.Cache.GetDependenciesForId(node.Id);

					foreach (var dependency in dependenciesForId)
					{
						var dependencyNode = GetOrCreateNode(dependency.Id, dependency.NodeType, dependency.Key,
							nodeDictionary, stateContext, updateNodeData, out var dependencyCached);
						var isHardConnection = stateContext.DependencyTypeLookup
							.GetDependencyType(dependency.DependencyType)
							.IsHardConnection(node, dependencyNode);
						var connection = new Connection(dependencyNode, dependency.DependencyType,
							dependency.PathSegments, isHardConnection);
						node.Dependencies.Add(connection);

						if (!dependencyCached)
						{
							DisplayNodeCreationProgress(dependencyNode, percentageDone);
						}
					}

					if (!nodeCached)
					{
						c++;
						DisplayNodeCreationProgress(node, percentageDone);
					}

					cacheUpdateResourcesCleaner.Clean(cacheUpdateSettings, c);

					k++;

					if (k % 500 == 0)
					{
						yield return null;
					}
				}
			}

			var j = 0;

			// create reference structure here
			foreach (var pair in nodeDictionary)
			{
				if (j % 2000 == 0)
				{
					EditorUtility.DisplayProgressBar("RelationLookup", "Building reference structure",
						(float)j / nodeDictionary.Count);
				}

				var referencerNode = pair.Value;

				foreach (var connection in referencerNode.Dependencies)
				{
					connection.Node.Referencers.Add(new Connection(referencerNode, connection.DependencyType,
						connection.PathSegments, connection.IsHardDependency));
				}

				j++;

				if (j % 5000 == 0)
				{
					yield return null;
				}
			}
		}

		private static void DisplayNodeCreationProgress(Node node, float percentage)
		{
			var canceled = EditorUtility.DisplayCancelableProgressBar("Creating node data",
				$"[{node.ConcreteType}]{node.Name}", percentage);

			if (canceled)
			{
				throw new DependencyUpdateAbortedException();
			}
		}

		public static Node GetOrCreateNode(string id, string type, string key, Dictionary<string, Node> nodeDictionary,
			NodeDependencyLookupContext context, bool updateData, out bool wasCached)
		{
			if (nodeDictionary.TryGetValue(key, out var outNode))
			{
				wasCached = true;
				return outNode;
			}

			var nodeHandler = context.NodeHandlerLookup[type];
			var node = nodeHandler.CreateNode(id, type, updateData, out wasCached);

			nodeDictionary.Add(key, node);

			return node;
		}
	}
}
