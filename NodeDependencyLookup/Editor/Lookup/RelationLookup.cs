using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class RelationLookup
	{
		public class RelationsLookup
		{
			private Dictionary<string, Node> _lookup = new Dictionary<string, Node>();

			public void Build(NodeDependencyLookupContext stateContext, List<CreatedDependencyCache> caches, Dictionary<string, Node> nodeDictionary, bool fastUpdate, bool updateData)
			{
				_lookup = RelationLookupBuilder.CreateRelationMapping(stateContext, caches, nodeDictionary, fastUpdate, updateData);
			}

			public Node GetNode(string id, string type)
			{
				return GetNode(NodeDependencyLookupUtility.GetNodeKey(id, type));
			}

			public Node GetNode(string key)
			{
				if (_lookup.ContainsKey(key))
				{
					return _lookup[key];
				}

				return null;
			}

			public List<Node> GetAllNodes()
			{
				return _lookup.Values.ToList();
			}
		}

		public static RelationsLookup GetAssetToFileLookup(CacheUpdateSettings cacheUpdateSettings, CacheUpdateInfo updateInfo)
		{
			NodeDependencyLookupContext context = new NodeDependencyLookupContext(cacheUpdateSettings);
			ResolverUsageDefinitionList resolverList = new ResolverUsageDefinitionList();
			resolverList.Add<AssetToFileDependencyCache, AssetToFileDependencyResolver>(true, updateInfo.Update, updateInfo.Save);
			NodeDependencyLookupUtility.LoadDependencyLookupForCaches(context, resolverList);

			return context.RelationsLookup;
		}

		// Builds bidirectional relations between nodes based on their dependencies
		public class RelationLookupBuilder
		{
			public static Dictionary<string, T> ConvertToDictionary<T>(T[] entries) where T : IIdentifyable
			{
				Dictionary<string, T> list = new Dictionary<string, T>();

				foreach (T entry in entries)
				{
					list[entry.Id] = entry;
				}

				return list;
			}

			public static Dictionary<string, Node> CreateRelationMapping(NodeDependencyLookupContext stateContext,
				List<CreatedDependencyCache> dependencyCaches,
				Dictionary<string, Node> nodeDictionary, bool isFastUpdate, bool updateNodeData)
			{
				List<IDependencyMappingNode> resolvedNodes = new List<IDependencyMappingNode>(16 * 1024);
				CacheUpdateSettings cacheUpdateSettings = stateContext.CacheUpdateSettings;

				if (isFastUpdate)
				{
					foreach (KeyValuePair<string,Node> pair in nodeDictionary)
					{
						pair.Value.ResetRelationInformation();
					}
				}
				else
				{
					nodeDictionary.Clear();
				}

				// Init Name and Type information for node handlers
				foreach (KeyValuePair<string, INodeHandler> pair in stateContext.NodeHandlerLookup)
				{
					pair.Value.InitNodeCreation();
				}

				foreach (CreatedDependencyCache dependencyCache in dependencyCaches)
				{
					IDependencyCache cache = dependencyCache.Cache;
					resolvedNodes.Clear();

					cache.AddExistingNodes(resolvedNodes);
					cache.InitLookup();

					int k = 0;

					// create dependency structure here
					foreach (IDependencyMappingNode resolvedNode in resolvedNodes)
					{
						float percentageDone = (float)k / resolvedNodes.Count;
						Node node = GetOrCreateNode(resolvedNode.Id, resolvedNode.Type, resolvedNode.Key, nodeDictionary, stateContext, updateNodeData, out bool nodeCached);

						List<Dependency> dependenciesForId = dependencyCache.Cache.GetDependenciesForId(node.Id);

						foreach (Dependency dependency in dependenciesForId)
						{
							Node dependencyNode = GetOrCreateNode(dependency.Id, dependency.NodeType, dependency.Key, nodeDictionary, stateContext, updateNodeData, out bool dependencyCached);
							bool isHardConnection = stateContext.DependencyTypeLookup.GetDependencyType(dependency.DependencyType).IsHardConnection(node, dependencyNode);
							Connection connection = new Connection(dependencyNode, dependency.DependencyType, dependency.PathSegments, isHardConnection);
							node.Dependencies.Add(connection);

							if(!dependencyCached)
							{
								DisplayNodeCreationProgress(dependencyNode, percentageDone);
							}
						}

						if(!nodeCached)
						{
							DisplayNodeCreationProgress(node, percentageDone);
						}

						k++;

						if (cacheUpdateSettings.ShouldUnloadUnusedAssets && k % (cacheUpdateSettings.UnloadUnusedAssetsInterval) == 0)
						{
							EditorUtility.UnloadUnusedAssetsImmediate(true);
							GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, true, false);
						}
					}
				}

				int j = 0;

				// create reference structure here
				foreach (var pair in nodeDictionary)
				{
					if (j % 2000 == 0)
					{
						EditorUtility.DisplayProgressBar("RelationLookup", $"Building reference structure", (float)j / nodeDictionary.Count);
					}

					Node referencerNode = pair.Value;

					foreach (Connection connection in referencerNode.Dependencies)
					{
						connection.Node.Referencers.Add(new Connection(referencerNode, connection.DependencyType, connection.PathSegments, connection.IsHardDependency));
					}

					j++;
				}

				NodeDependencyLookupUtility.CalculateAllNodeSizes(nodeDictionary.Values.ToList(), stateContext);

				foreach (KeyValuePair<string, INodeHandler> pair in stateContext.NodeHandlerLookup)
				{
					EditorUtility.DisplayProgressBar("RelationLookup", $"Saving NodeHandler cache: {pair.Value.GetType().Name}", 0);
					pair.Value.SaveCaches();
				}

				EditorUtility.ClearProgressBar();

				return nodeDictionary;
			}

			private static void DisplayNodeCreationProgress(Node node, float percentage)
			{
				bool canceled = EditorUtility.DisplayCancelableProgressBar("Creating node data", $"[{node.ConcreteType}]{node.Name}", percentage);

				if (canceled)
				{
					throw new DependencyUpdateAbortedException();
				}
			}

			private static Node GetOrCreateNode(string id, string type, string key,
				Dictionary<string, Node> nodeDictionary, NodeDependencyLookupContext context, bool updateData, out bool wasCached)
			{
				if (nodeDictionary.TryGetValue(key, out Node outNode))
				{
					wasCached = true;
					return outNode;
				}

				INodeHandler nodeHandler = context.NodeHandlerLookup[type];
				Node node = nodeHandler.CreateNode(id, type, updateData, out wasCached);

				nodeDictionary.Add(key, node);

				return node;
			}
		}
	}
}