using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
#if UNITY_2021_3_OR_NEWER
using UnityEditor.SceneManagement;
#else
using UnityEditor.Experimental.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Cache to find gameobject -> gameobject dependencies within the currently opened Scene or Prefab.
	/// This also works inside the playmode to analyze the currently loaded scenes in the running game.
	/// </summary>
	public class OpenSceneDependencyCache : IDependencyCache
	{
		private struct TraverseValues
		{
			public HashSet<UnityEngine.Object> SceneObjects;
		}

		private CreatedDependencyCache _createdDependencyCache;

		private readonly Dictionary<string, GenericDependencyMappingNode> _lookup =
			new Dictionary<string, GenericDependencyMappingNode>();

		private IDependencyMappingNode[] _nodes = Array.Empty<IDependencyMappingNode>();

		public void Initialize(CreatedDependencyCache createdDependencyCache)
		{
			_createdDependencyCache = createdDependencyCache;
		}

		public bool CanUpdate()
		{
			return true;
		}

		public static GameObject[] GetRootGameObjects()
		{
			var currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();

			if (currentPrefabStage != null)
			{
				return new[] {currentPrefabStage.prefabContentsRoot};
			}

			var rootGameObjects = new List<GameObject>();

			for (var i = 0; i < SceneManager.sceneCount; ++i)
			{
				rootGameObjects.AddRange(SceneManager.GetSceneAt(i).GetRootGameObjects());
			}

			return rootGameObjects.ToArray();
		}

		public IEnumerator Update(CacheUpdateSettings cacheUpdateSettings, ResolverUsageDefinitionList resolverUsages,
			bool shouldUpdate)
		{
			_lookup.Clear();

			var stack = new Stack<PathSegment>();
			var traverseValues = new TraverseValues();
			traverseValues.SceneObjects = new HashSet<UnityEngine.Object>();

			var rootGameObjects = GetRootGameObjects();

			foreach (var gameObject in rootGameObjects)
			{
				FindAllGameObjects(gameObject, traverseValues);
			}

			foreach (var gameObject in rootGameObjects)
			{
				TraverseGameObject(gameObject, stack, traverseValues);
			}

			_nodes = new IDependencyMappingNode[_lookup.Count];
			var count = 0;

			foreach (var pair in _lookup)
			{
				_nodes[count++] = pair.Value;
			}

			yield return null;
		}

		public void AddExistingNodes(List<IDependencyMappingNode> nodes)
		{
			foreach (var node in _nodes)
			{
				nodes.Add(node);
			}
		}

		public List<Dependency> GetDependenciesForId(string id)
		{
			if (NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, InSceneDependencyResolver.Id,
				    InSceneConnectionType.Name))
			{
				return _lookup[id].Dependencies;
			}

			return new List<Dependency>();
		}

		public void Load(string directory)
		{
			// nothing to do
		}

		public void Save(string directory)
		{
			// nothing to do
		}

		public void InitLookup()
		{
			// nothing to do
		}

		public Type GetResolverType()
		{
			return typeof(IInSceneDependencyResolver);
		}

		private void FindAllGameObjects(GameObject go, TraverseValues traverseValues)
		{
			var childCount = go.transform.childCount;

			traverseValues.SceneObjects.Add(go);

			for (var i = 0; i < childCount; ++i)
			{
				FindAllGameObjects(go.transform.GetChild(i).gameObject, traverseValues);
			}
		}

		private void TraverseGameObject(GameObject go, Stack<PathSegment> stack, TraverseValues traverseValues)
		{
			GetNode(go.GetHashCode().ToString());

			var components = go.GetComponents<Component>();

			foreach (var component in components)
			{
				TraverseComponent(go, component, stack, traverseValues);
			}

			var childCount = go.transform.childCount;

			for (var i = 0; i < childCount; ++i)
			{
				var child = go.transform.GetChild(i);
				TraverseGameObject(child.gameObject, stack, traverseValues);
			}
		}

		private void TraverseComponent(GameObject go, Component component, Stack<PathSegment> stack,
			TraverseValues traverseValues)
		{
			stack.Push(new PathSegment(component.GetType().Name, PathSegmentType.Component));
			var serializedObject = new SerializedObject(component);
			var serializedProperty = serializedObject.GetIterator();

			while (serializedProperty.Next(true))
			{
				if (serializedProperty.propertyType == SerializedPropertyType.ObjectReference)
				{
					var value = serializedProperty.objectReferenceValue;

					if (value == null)
					{
						continue;
					}

					Component componentValue = null;

					if (value is Component)
					{
						var propertyPath = serializedProperty.propertyPath;
						var exclude = propertyPath.StartsWith("m_Children.") || propertyPath == "m_Father";

						if (!exclude)
						{
							componentValue = value as Component;
							value = componentValue.gameObject;
						}
					}

					if (value == go)
					{
						continue;
					}

					if (traverseValues.SceneObjects.Contains(value))
					{
						var goHash = go.GetHashCode().ToString();
						var valueHash = value.GetHashCode().ToString();

						var node = GetNode(goHash);

						stack.Push(new PathSegment(serializedProperty.propertyPath, PathSegmentType.Property));

						if (componentValue)
						{
							stack.Push(new PathSegment(componentValue.GetType().Name, PathSegmentType.Unknown));
							node.Dependencies.Add(new Dependency(valueHash, InSceneConnectionType.Name,
								InSceneNodeType.Name,
								stack.ToArray()));
							stack.Pop();
						}
						else
						{
							node.Dependencies.Add(new Dependency(valueHash, InSceneConnectionType.Name,
								InSceneNodeType.Name,
								stack.ToArray()));
						}

						stack.Pop();
					}
				}
			}

			stack.Pop();
		}

		private GenericDependencyMappingNode GetNode(string id)
		{
			if (!_lookup.ContainsKey(id))
			{
				var node = new GenericDependencyMappingNode(id, InSceneNodeType.Name);
				_lookup.Add(id, node);
			}

			return _lookup[id];
		}
	}

	public interface IInSceneDependencyResolver : IDependencyResolver
	{
	}

	public class InSceneDependencyResolver : IInSceneDependencyResolver
	{
		private const string ConnectionTypeDescription =
			"Dependencies between GameObjects in the currently opened scene/prefab";

		private static DependencyType InSceneType = new DependencyType("Scene GameObject->GameObject",
			new Color(0.8f, 0.9f, 0.6f), false, true, ConnectionTypeDescription);

		public const string Id = "InSceneDependencyResolver";

		public string[] GetDependencyTypes()
		{
			return new[] {InSceneConnectionType.Name};
		}

		public string GetId()
		{
			return Id;
		}

		public DependencyType GetDependencyTypeForId(string typeId)
		{
			return InSceneType;
		}
	}

	public class InSceneNodeType
	{
		public const string Name = "InSceneGameObject";
	}

	public class InSceneConnectionType
	{
		public const string Name = "GTOG_InScene";
	}

	public class InSceneDependencyNodeHandler : INodeHandler
	{
		private Dictionary<string, GameObject> _hashToGameObject = new Dictionary<string, GameObject>();

		public string GetHandledNodeType()
		{
			return InSceneNodeType.Name;
		}

		public void InitializeOwnFileSize(Node node, NodeDependencyLookupContext stateContext, bool updateNodeData)
		{
			node.OwnSize = new Node.NodeSize {Size = 0, ContributesToTreeSize = false};
		}

		public void CalculateOwnFileSize(Node node, NodeDependencyLookupContext stateContext, bool updateNodeData)
		{
			// nothing to do
		}

		public void CalculateOwnFileDependencies(Node node, NodeDependencyLookupContext context,
			HashSet<Node> calculatedNodes)
		{
			// nothing to do
		}

		public bool IsNodePackedToApp(Node node, bool alwaysExcluded)
		{
			return false;
		}

		public bool IsNodeEditorOnly(string id, string type)
		{
			return false;
		}

		public void BuildHashToGameObjectMapping()
		{
			_hashToGameObject.Clear();

			foreach (var rootGameObject in OpenSceneDependencyCache.GetRootGameObjects())
			{
				BuildHashToGameObjectMapping(rootGameObject);
			}
		}

		private void BuildHashToGameObjectMapping(GameObject go)
		{
			_hashToGameObject.Add(go.GetHashCode().ToString(), go);

			for (var i = 0; i < go.transform.childCount; ++i)
			{
				BuildHashToGameObjectMapping(go.transform.GetChild(i).gameObject);
			}
		}

		public Node CreateNode(string id, string type, bool update, out bool wasCached)
		{
			var concreteType = "GameObject";
			var name = _hashToGameObject.ContainsKey(id) ? _hashToGameObject[id].name : id;

			wasCached = false;
			return new Node(id, type, name, concreteType);
		}

		public void InitNodeCreation()
		{
			BuildHashToGameObjectMapping();
		}

		public void SaveCaches()
		{
		}

		public GameObject GetGameObjectById(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return null;
			}

			if (_hashToGameObject.TryGetValue(id, out var go))
			{
				return go;
			}

			return null;
		}
	}
}