using System.Collections.Generic;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public class InSceneVisualizationNodeData : VisualizationNodeData
    {
        public override Texture2D AssetPreviewTexture => null;
        public override Texture2D ThumbNailTexture => null;
    }

    public class InSceneTypeHandler : ITypeHandler
    {
        private const string SyncPrefKey = "InSceneSync";
        private AssetRelationsViewerWindow _viewerWindow;
        private InSceneDependencyNodeHandler _nodeHandler;

        private HashSet<string> _nodes = new HashSet<string>();

        private GameObject _currentNode = null;

        public string GetHandledType()
        {
            return InSceneNodeType.Name;
        }

        public string GetSortingKey(string name)
        {
            return name;
        }

        public void ApplyFilterString(string filterString)
        {
            
        }

        public VisualizationNodeData CreateNodeCachedData(string id)
        {
            return new InSceneVisualizationNodeData();
        }

        public void SelectInEditor(string id)
        {
            foreach (GameObject rootGameObject in OpenSceneDependencyCache.GetRootGameObjects())
            {
                SelectGameObject(rootGameObject, id);
            }
        }

        private void SelectGameObject(GameObject go, string id)
        {
            if (go.GetHashCode().ToString() == id)
            {
                Selection.activeObject = go;
                return;
            }

            for (int i = 0; i < go.transform.childCount; ++i)
            {
                SelectGameObject(go.transform.GetChild(i).gameObject, id);
            }
        }

        public void OnGui()
        {
            EditorPrefs.SetBool(SyncPrefKey, 
                EditorGUILayout.ToggleLeft("Sync to Hierarchy:", EditorPrefs.GetBool(SyncPrefKey, false)));

            GameObject newSelection = EditorGUILayout.ObjectField(_currentNode, typeof(GameObject), true) as GameObject;

            bool selectionChanged = newSelection != null && newSelection != _currentNode;
            _currentNode = newSelection;
            
            if (selectionChanged || (_currentNode != null && GUILayout.Button("Select")))
            {
                _viewerWindow.ChangeSelection(_currentNode.GetHashCode().ToString(), InSceneNodeType.Name);
            }
        }

        public void OnSelectAsset(string id, string type)
        {
            GameObject node = _nodeHandler.GetGameObjectById(id);

            if (type == InSceneNodeType.Name && node != null)
            {
                _currentNode = node; 
                Selection.activeObject = _currentNode;
            }
        }

        public void InitContext(NodeDependencyLookupContext context, AssetRelationsViewerWindow viewerWindow, INodeHandler nodeHandler)
        {
            _viewerWindow = viewerWindow;
            _nodeHandler = nodeHandler as InSceneDependencyNodeHandler;

            HashSet<string> nodes = new HashSet<string>();

            foreach (KeyValuePair<string, CreatedDependencyCache> pair in context.CreatedCaches)
            {
                List<IDependencyMappingNode> resolvedNodes = new List<IDependencyMappingNode>();
                pair.Value.Cache.AddExistingNodes(resolvedNodes);

                foreach (IDependencyMappingNode node in resolvedNodes)
                {
                    if (node.Type == InSceneNodeType.Name)
                        nodes.Add(node.Id);
                }
            }

            _nodes = new HashSet<string>(nodes);
            _nodeHandler.BuildHashToGameObjectMapping();

            Selection.selectionChanged += SelectionChanged;
        }

        private void SelectionChanged()
        {
            _currentNode = null;
            Object activeObject = Selection.activeObject;

            if (activeObject == null)
            {
                return;
            }
            
            string hashCode = activeObject.GetHashCode().ToString();

            if (_nodes.Contains(hashCode))
            {
                _currentNode = _nodeHandler.GetGameObjectById(hashCode);

                if (EditorPrefs.GetBool(SyncPrefKey))
                {
                    _viewerWindow.ChangeSelection(hashCode, InSceneNodeType.Name);
                }

                _viewerWindow.Repaint();
            }
        }

        public bool HandlesCurrentNode()
        {
            return _currentNode != null;
        }
    }
}