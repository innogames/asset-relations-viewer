﻿using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class RelationLookup
	{
		public class RelationsLookup
		{
			private Dictionary<string, Node> _lookup = new Dictionary<string, Node>();

			public void Build(List<CreatedDependencyCache> caches)
			{
				_lookup = RelationLookupBuilder.CreateRelationMapping(caches);
			}

			public Node GetNode(string id, string type)
			{
				string key = NodeDependencyLookupUtility.GetNodeKey(id, type);

				if (_lookup.ContainsKey(key))
				{
					return _lookup[key];
				}

				return null;
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

			public static Dictionary<string, Node> CreateRelationMapping(List<CreatedDependencyCache> dependencyCaches)
			{
				List<IResolvedNode> resolvedNodes = new List<IResolvedNode>();
				Dictionary<string, List<IDependencyCache>> typeToCaches = new Dictionary<string, List<IDependencyCache>>();
				Dictionary<string, Node> nodeDictionary = new Dictionary<string, Node>();

				foreach (CreatedDependencyCache dependencyCache in dependencyCaches)
				{
					IDependencyCache cache = dependencyCache.Cache;
					string handledNodeType = cache.GetHandledNodeType();

					if (!typeToCaches.ContainsKey(handledNodeType))
					{
						typeToCaches.Add(handledNodeType, new List<IDependencyCache>());
					}
					
					typeToCaches[handledNodeType].Add(cache);

					cache.AddExistingNodes(resolvedNodes);
					cache.InitLookup();
				}
				
				// create dependency structure here
				foreach (var resolvedNode in resolvedNodes)
				{
					Node referencerNode = GetOrCreateNode(resolvedNode.Id, resolvedNode.Type, nodeDictionary);
					
					foreach (IDependencyCache dependencyCache in typeToCaches[referencerNode.Type])
					{
						List<Dependency> dependenciesForId = dependencyCache.GetDependenciesForId(referencerNode.Id);

						foreach (Dependency dependency in dependenciesForId)
						{
							Node dependencyNode = GetOrCreateNode(dependency.Id, dependency.NodeType, nodeDictionary);
							referencerNode.Dependencies.Add(
								new Connection(dependencyNode, dependency.ConnectionType, dependency.PathSegments));
						}
					}
				}

				// create reference structure here
				foreach (var pair in nodeDictionary)
				{
					Node referencerNode = pair.Value;

					foreach (Connection connection in referencerNode.Dependencies)
					{
						connection.Node.Referencers.Add(new Connection(referencerNode, connection.Type, connection.PathSegments));
					}
				}

				EditorUtility.ClearProgressBar();

				return nodeDictionary;
			}

			private static Node GetOrCreateNode(string id, string type, Dictionary<string, Node> nodeDictionary)
			{
				string key = NodeDependencyLookupUtility.GetNodeKey(id, type);

				if (!nodeDictionary.ContainsKey(key))
				{
					nodeDictionary.Add(key, new Node {Id = id, Type = type});
				}

				return nodeDictionary[key];
			}
		}
	}
}