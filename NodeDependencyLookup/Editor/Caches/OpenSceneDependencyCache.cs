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

        private Dictionary<string, InSceneDependencyMappingNode> Lookup =
            new Dictionary<string, InSceneDependencyMappingNode>();

        private IResolvedNode[] Nodes = new IResolvedNode[0];

        public void ClearFile(string directory)
        {
            // nothing to do
        }

        public void Initialize(CreatedDependencyCache createdDependencyCache)
        {
            _createdDependencyCache = createdDependencyCache;
        }

        public bool NeedsUpdate(ProgressBase progress)
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

        public void Update(ProgressBase progress)
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

            Nodes = new IResolvedNode[Lookup.Count];
            int count = 0;

            foreach (KeyValuePair<string, InSceneDependencyMappingNode> pair in Lookup)
            {
                Nodes[count++] = pair.Value;
            }
        }

        public struct TraverseValues
        {
            public HashSet<UnityEngine.Object> SceneObjects;
        }

        public void AddExistingNodes(List<IResolvedNode> nodes)
        {
            foreach (IResolvedNode node in Nodes)
            {
                if (node.Existing)
                {
                    nodes.Add(node);
                }
            }
        }

        public string GetHandledNodeType()
        {
            return "InScene";
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
            Component[] components = go.GetComponents<Component>();

            string goHash = go.GetHashCode().ToString();
            InSceneDependencyMappingNode node = GetNode(goHash);

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

                        InSceneDependencyMappingNode node = GetNode(goHash);

                        stack.Push(new PathSegment(serializedProperty.propertyPath, PathSegmentType.Property));

                        if (componentValue)
                        {
                            stack.Push(new PathSegment(componentValue.GetType().Name, PathSegmentType.Unknown));
                            node.Dependencies.Add(new Dependency(valueHash, "InScene", GetHandledNodeType(),
                                stack.ToArray()));
                            stack.Pop();
                        }
                        else
                        {
                            node.Dependencies.Add(new Dependency(valueHash, "InScene", GetHandledNodeType(),
                                stack.ToArray()));
                        }

                        stack.Pop();
                    }
                }
            }

            stack.Pop();
        }

        private InSceneDependencyMappingNode GetNode(string id)
        {
            if (!Lookup.ContainsKey(id))
            {
                InSceneDependencyMappingNode node = new InSceneDependencyMappingNode();
                node.NodeId = id;
                Lookup.Add(id, node);
            }

            return Lookup[id];
        }
    }

    public interface IInSceneDependencyResolver : IDependencyResolver
    {
    }

    public class InSceneDependencyMappingNode : IResolvedNode
    {
        public string NodeId;
        public string Id => NodeId;
        public string Type => "InScene";
        public bool Existing => true;

        public List<Dependency> Dependencies = new List<Dependency>();
    }

    public class InSceneDependencyResolver : IInSceneDependencyResolver
    {
        private static ConnectionType InSceneType = new ConnectionType(new Color(0.8f, 0.9f, 0.6f), false, true);

        public const string ResolvedType = "InScene";
        public const string Id = "InSceneDependencyResolver";

        public string[] GetConnectionTypes()
        {
            return new[] {"InScene"};
        }

        public string GetId()
        {
            return Id;
        }

        public ConnectionType GetDependencyTypeForId(string typeId)
        {
            return InSceneType;
        }
    }

    public class InSceneDependencyNodeHandler : INodeHandler
    {
        private string[] HandledTypes = {"InScene"};

        public string GetId()
        {
            return "InSceneDependencyNodeHandler";
        }

        public string[] GetHandledNodeTypes()
        {
            return HandledTypes;
        }

        public int GetOwnFileSize(string id, string type, NodeDependencyLookupContext stateContext)
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
    }
}