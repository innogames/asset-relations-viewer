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

			public void Build(List<CreatedDependencyCache> caches, Dictionary<string, Node> nodeDictionary, bool fastUpdate)
			{
				_lookup = RelationLookupBuilder.CreateRelationMapping(caches, nodeDictionary, fastUpdate);
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

			public static Dictionary<string, Node> CreateRelationMapping(List<CreatedDependencyCache> dependencyCaches, 
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

				foreach (CreatedDependencyCache dependencyCache in dependencyCaches)
				{
					IDependencyCache cache = dependencyCache.Cache;
					resolvedNodes.Clear();

					cache.AddExistingNodes(resolvedNodes);
					cache.InitLookup();

					// create dependency structure here
					foreach (var resolvedNode in resolvedNodes)
					{
						Node referencerNode = GetOrCreateNode(resolvedNode.Id, resolvedNode.Type, resolvedNode.Key, nodeDictionary);
						List<Dependency> dependenciesForId = dependencyCache.Cache.GetDependenciesForId(referencerNode.Id);

						foreach (Dependency dependency in dependenciesForId)
						{
							Node dependencyNode = GetOrCreateNode(dependency.Id, dependency.NodeType, dependency.Key, nodeDictionary);
							referencerNode.Dependencies.Add(
								new Connection(dependencyNode, dependency.DependencyType, dependency.PathSegments));
						}
					}
				}

				// create reference structure here
				foreach (var pair in nodeDictionary)
				{
					Node referencerNode = pair.Value;

					foreach (Connection connection in referencerNode.Dependencies)
					{
						connection.Node.Referencers.Add(new Connection(referencerNode, connection.DependencyType, connection.PathSegments));
					}
				}

				EditorUtility.ClearProgressBar();

				return nodeDictionary;
			}

			private static Node GetOrCreateNode(string id, string type, string key,
				Dictionary<string, Node> nodeDictionary)
			{
				if (!nodeDictionary.ContainsKey(key))
				{
					nodeDictionary.Add(key, new Node(id, type));
				}

				return nodeDictionary[key];
			}
		}
	}
}