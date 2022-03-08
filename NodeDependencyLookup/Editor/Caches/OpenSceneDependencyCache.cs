using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    /// <summary>
    /// Resolver to find gameobject -> gameobject dependencies within a scene
    /// </summary>
    public class OpenSceneDependencyCache : IDependencyCache
    {
        private const string ConnectionType = "InScene";
        private CreatedDependencyCache _createdDependencyCache;

        private Dictionary<string, GenericDependencyMappingNode> Lookup =
            new Dictionary<string, GenericDependencyMappingNode>();

        private IDependencyMappingNode[] Nodes = new IDependencyMappingNode[0];

        public void ClearFile(string directory)
        {
            // nothing to do
        }

        public void Initialize(CreatedDependencyCache createdDependencyCache)
        {
            _createdDependencyCache = createdDependencyCache;
        }

        public bool NeedsUpdate()
        {
            return true;
        }

        public bool CanUpdate()
        {
            return true;
        }

        public static GameObject[] GetRootGameObjects()
        {
#if UNITY_2018_3_OR_NEWER
            PrefabStage currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();

            if (currentPrefabStage != null)
            {
                return new[] {currentPrefabStage.prefabContentsRoot};
            }
#endif

            List<GameObject> rootGameObjects = new List<GameObject>();

            for (int i = 0; i < SceneManager.sceneCount; ++i)
            {
                rootGameObjects.AddRange(SceneManager.GetSceneAt(i).GetRootGameObjects());
            }
            
            return rootGameObjects.ToArray();
        }

        public void Update()
        {
            Lookup.Clear();

            Stack<PathSegment> stack = new Stack<PathSegment>();
            TraverseValues traverseValues = new TraverseValues();
            traverseValues.SceneObjects = new HashSet<UnityEngine.Object>();

            GameObject[] rootGameObjects = GetRootGameObjects();

            foreach (GameObject gameObject in rootGameObjects)
            {
                FindAllGameObjects(gameObject, traverseValues);
            }

            foreach (GameObject gameObject in rootGameObjects)
            {
                TraverseGameObject(gameObject, stack, traverseValues);
            }

            Nodes = new IDependencyMappingNode[Lookup.Count];
            int count = 0;

            foreach (KeyValuePair<string, GenericDependencyMappingNode> pair in Lookup)
            {
                Nodes[count++] = pair.Value;
            }
        }

        public struct TraverseValues
        {
            public HashSet<UnityEngine.Object> SceneObjects;
        }

        public void AddExistingNodes(List<IDependencyMappingNode> nodes)
        {
            foreach (IDependencyMappingNode node in Nodes)
            {
                if (node.Existing)
                {
                    nodes.Add(node);
                }
            }
        }

        public List<Dependency> GetDependenciesForId(string id)
        {
            if (NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, InSceneDependencyResolver.Id,
                ConnectionType))
            {
                return Lookup[id].Dependencies;
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
            int childCount = go.transform.childCount;

            traverseValues.SceneObjects.Add(go);

            for (int i = 0; i < childCount; ++i)
            {
                FindAllGameObjects(go.transform.GetChild(i).gameObject, traverseValues);
            }
        }

        private void TraverseGameObject(GameObject go, Stack<PathSegment> stack, TraverseValues traverseValues)
        {
            GetNode(go.GetHashCode().ToString());
            
            Component[] components = go.GetComponents<Component>();
            
            foreach (Component component in components)
            {
                TraverseComponent(go, component, stack, traverseValues);
            }

            int childCount = go.transform.childCount;

            for (int i = 0; i < childCount; ++i)
            {
                Transform child = go.transform.GetChild(i);
                TraverseGameObject(child.gameObject, stack, traverseValues);
            }
        }

        private void TraverseComponent(GameObject go, Component component, Stack<PathSegment> stack,
            TraverseValues traverseValues)
        {
            stack.Push(new PathSegment(component.GetType().Name, PathSegmentType.Component));
            SerializedObject serializedObject = new SerializedObject(component);
            SerializedProperty serializedProperty = serializedObject.GetIterator();

            while (serializedProperty.Next(true))
            {
                if (serializedProperty.propertyType == SerializedPropertyType.ObjectReference)
                {
                    UnityEngine.Object value = serializedProperty.objectReferenceValue;

                    if (value == null)
                    {
                        continue;
                    }

                    Component componentValue = null;

                    if (value is Component)
                    {
                        string propertyPath = serializedProperty.propertyPath;
                        bool exclude = propertyPath.StartsWith("m_Children.") || propertyPath == "m_Father";

                        if (!exclude)
                        {
                            componentValue = (value as Component);
                            value = componentValue.gameObject;
                        }
                    }

                    if (value == go)
                    {
                        continue;
                    }

                    if (traverseValues.SceneObjects.Contains(value))
                    {
                        string goHash = go.GetHashCode().ToString();
                        string valueHash = value.GetHashCode().ToString();

                        GenericDependencyMappingNode node = GetNode(goHash);

                        stack.Push(new PathSegment(serializedProperty.propertyPath, PathSegmentType.Property));

                        if (componentValue)
                        {
                            stack.Push(new PathSegment(componentValue.GetType().Name, PathSegmentType.Unknown));
                            node.Dependencies.Add(new Dependency(valueHash, ConnectionType, InSceneNodeType.Name,
                                stack.ToArray()));
                            stack.Pop();
                        }
                        else
                        {
                            node.Dependencies.Add(new Dependency(valueHash, ConnectionType, InSceneNodeType.Name,
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
            if (!Lookup.ContainsKey(id))
            {
                GenericDependencyMappingNode node = new GenericDependencyMappingNode();
                node.NodeId = id;
                node.NodeType = InSceneNodeType.Name;
                Lookup.Add(id, node);
            }

            return Lookup[id];
        }
    }

    public interface IInSceneDependencyResolver : IDependencyResolver
    {
    }

    public class InSceneDependencyResolver : IInSceneDependencyResolver
    {
        private const string ConnectionTypeDescription = "Dependencies between GameObjects in the currently opened scene/prefab";
        private static DependencyType InSceneType = new DependencyType("Scene GameObject->GameObject", new Color(0.8f, 0.9f, 0.6f), false, true, ConnectionTypeDescription);
        public const string Id = "InSceneDependencyResolver";

        public string[] GetConnectionTypes()
        {
            return new[] {"InScene"};
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

    public class InSceneDependencyNodeHandler : INodeHandler
    {
        private Dictionary<string, GameObject> _hashToGameObject = new Dictionary<string, GameObject>();
        
        public string GetId()
        {
            return "InSceneDependencyNodeHandler";
        }

        public string GetHandledNodeType()
        {
            return InSceneNodeType.Name;
        }

        public int GetOwnFileSize(string type, string id, string key,
            NodeDependencyLookupContext stateContext,
            Dictionary<string, NodeDependencyLookupUtility.NodeSize> ownSizeCache)
        {
            return 0;
        }

        public bool IsNodePackedToApp(string id, string type, bool alwaysExcluded)
        {
            return false;
        }

        public bool IsNodeEditorOnly(string id, string type)
        {
            return false;
        }

        public bool ContributesToTreeSize()
        {
            return false;
        }
        
        public void BuildHashToGameObjectMapping()
        {
            _hashToGameObject.Clear();

            foreach (GameObject rootGameObject in OpenSceneDependencyCache.GetRootGameObjects())
            {
                BuildHashToGameObjectMapping(rootGameObject);
            }
        }

        private void BuildHashToGameObjectMapping(GameObject go)
        {
            _hashToGameObject.Add(go.GetHashCode().ToString(), go);

            for (int i = 0; i < go.transform.childCount; ++i)
            {
                BuildHashToGameObjectMapping(go.transform.GetChild(i).gameObject);
            }
        }

        public void GetNameAndType(string id, out string name, out string type)
        {
            type = "GameObject";
            
            if (!_hashToGameObject.ContainsKey(id))
            {
                name = id;
                return;
            }

            name = _hashToGameObject[id].name;
        }

        public long GetChangedTimeStamp(string id)
        {
            return -1;
        }

        public GameObject GetGameObjectById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }
            
            if (_hashToGameObject.TryGetValue(id, out GameObject go))
            {
                return go;
            }

            return null;
        }
    }
}