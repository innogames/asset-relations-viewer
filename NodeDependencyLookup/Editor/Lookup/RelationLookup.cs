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

			public void Build(NodeDependencyLookupContext stateContext, List<CreatedDependencyCache> caches, Dictionary<string, Node> nodeDictionary, bool fastUpdate)
			{
				_lookup = RelationLookupBuilder.CreateRelationMapping(stateContext, caches, nodeDictionary, fastUpdate);
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

		public static RelationsLookup GetAssetToFileLookup(CacheUpdateInfo updateInfo)
		{
			NodeDependencyLookupContext context = new NodeDependencyLookupContext();
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
				Dictionary<string, Node> nodeDictionary, bool isFastUpdate)
			{
				List<IDependencyMappingNode> resolvedNodes = new List<IDependencyMappingNode>(16 * 1024);

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
					pair.Value.InitNameAndTypeInformation();
				}

				foreach (CreatedDependencyCache dependencyCache in dependencyCaches)
				{
					IDependencyCache cache = dependencyCache.Cache;
					resolvedNodes.Clear();

					cache.AddExistingNodes(resolvedNodes);
					cache.InitLookup();

					int k = 0;
					string cacheName = cache.GetType().Name;

					// create dependency structure here
					foreach (var resolvedNode in resolvedNodes)
					{
						Node node = GetOrCreateNode(resolvedNode.Id, resolvedNode.Type, resolvedNode.Key, nodeDictionary, stateContext);

						List<Dependency> dependenciesForId = dependencyCache.Cache.GetDependenciesForId(node.Id);

						foreach (Dependency dependency in dependenciesForId)
						{
							Node dependencyNode = GetOrCreateNode(dependency.Id, dependency.NodeType, dependency.Key, nodeDictionary, stateContext);
							bool isHardConnection = stateContext.DependencyTypeLookup.GetDependencyType(dependency.DependencyType).IsHardConnection(node, dependencyNode);
							Connection connection = new Connection(dependencyNode, dependency.DependencyType, dependency.PathSegments, isHardConnection);
							node.Dependencies.Add(connection);
						}

						if (k % 2000 == 0)
						{
							EditorUtility.DisplayProgressBar("RelationLookup", $"Building relation lookup for {cacheName}", (float)k / resolvedNodes.Count);
						}

						k++;
					}
				}

				// create reference structure here
				foreach (var pair in nodeDictionary)
				{
					Node referencerNode = pair.Value;

					foreach (Connection connection in referencerNode.Dependencies)
					{
						connection.Node.Referencers.Add(new Connection(referencerNode, connection.DependencyType, connection.PathSegments, connection.IsHardDependency));
					}
				}

				// Save nodehandler lookup caches
				foreach (KeyValuePair<string, INodeHandler> pair in stateContext.NodeHandlerLookup)
				{
					pair.Value.SaveCaches();
				}

				EditorUtility.ClearProgressBar();

				return nodeDictionary;
			}

			private static Node GetOrCreateNode(string id, string type, string key,
				Dictionary<string, Node> nodeDictionary, NodeDependencyLookupContext context)
			{
				if (!nodeDictionary.ContainsKey(key))
				{
					Node node = new Node(id, type);

					if (string.IsNullOrEmpty(node.Name))
					{
						INodeHandler nodeHandler = context.NodeHandlerLookup[node.Type];
						nodeHandler.GetNameAndType(node.Id, out node.Name, out node.ConcreteType);
					}

					nodeDictionary.Add(key, node);
				}

				return nodeDictionary[key];
			}
		}
	}
}