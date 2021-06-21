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
        private const string HandledType = "InScene";
        private const string SyncPrefKey = "InSceneSync";
        private AssetRelationsViewerWindow _viewerWindow;

        private HashSet<string> _nodes = new HashSet<string>();
        private Dictionary<string, GameObject> _hashToGameObject = new Dictionary<string, GameObject>();

        private GameObject m_currentNode = null;

        public string GetHandledType()
        {
            return HandledType;
        }

        public string GetSortingKey(string name)
        {
            return name;
        }

        public bool HasFilter()
        {
            return false;
        }

        public bool IsFiltered(string id)
        {
            return false;
        }

        public string GetName(string id)
        {
            if (!_hashToGameObject.ContainsKey(id))
            {
                return id;
            }

            return _hashToGameObject[id].name;
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

            GameObject newSelection = EditorGUILayout.ObjectField(m_currentNode, typeof(GameObject), true) as GameObject;

            bool selectionChanged = newSelection != null && newSelection != m_currentNode;
            m_currentNode = newSelection;
            
            if (selectionChanged || (m_currentNode != null && GUILayout.Button("Select")))
            {
                _viewerWindow.ChangeSelection(m_currentNode.GetHashCode().ToString(), HandledType);
            }
        }

        public void OnSelectAsset(string id, string type)
        {
            if (type == HandledType && _hashToGameObject.ContainsKey(id))
            {
                Selection.activeObject = _hashToGameObject[id];
            }
        }

        public void InitContext(NodeDependencyLookupContext context, AssetRelationsViewerWindow viewerWindow)
        {
            _viewerWindow = viewerWindow;

            HashSet<string> nodes = new HashSet<string>();

            foreach (KeyValuePair<string, CreatedDependencyCache> pair in context.CreatedCaches)
            {
                List<IResolvedNode> resolvedNodes = new List<IResolvedNode>();
                pair.Value.Cache.AddExistingNodes(resolvedNodes);

                foreach (IResolvedNode node in resolvedNodes)
                {
                    if (node.Type == HandledType)
                        nodes.Add(node.Id);
                }
            }

            _nodes = new HashSet<string>(nodes);

            BuildHashToGameObjectMapping();

            Selection.selectionChanged += SelectionChanged;
        }

        private void BuildHashToGameObjectMapping()
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

        private void SelectionChanged()
        {
            m_currentNode = null;
            Object activeObject = Selection.activeObject;

            if (activeObject == null)
            {
                return;
            }
            
            string hashCode = activeObject.GetHashCode().ToString();

            if (_nodes.Contains(hashCode))
            {
                m_currentNode = _hashToGameObject[hashCode];

                if (EditorPrefs.GetBool(SyncPrefKey))
                {
                    _viewerWindow.ChangeSelection(hashCode, "InScene");
                }

                _viewerWindow.Repaint();
            }
        }

        public bool HandlesCurrentNode()
        {
            return m_currentNode != null;
        }
    }
}