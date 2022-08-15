using System;
using System.Collections.Generic;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public class InSceneVisualizationNodeData : VisualizationNodeData
    {
        public override Texture2D AssetPreviewTexture => null;
        public override Texture2D ThumbNailTexture => null;
    }

    public class InSceneTypeHandler : ITypeHandler
    {
        private PrefValueBool SyncPref = new PrefValueBool("InSceneTypeHandler_Sync", false);
        private PrefValueBool AutoRefreshPref = new PrefValueBool("InSceneTypeHandler_AutoRefresh", true);
        private AssetRelationsViewerWindow _viewerWindow;
        private InSceneDependencyNodeHandler _nodeHandler;

        private HashSet<string> _nodes = new HashSet<string>();

        private GameObject _currentNode = null;
        private int _currentLoadedSceneKey;
        
        private Type cacheType = typeof(OpenSceneDependencyCache);
        private Type resolverType = typeof(InSceneDependencyResolver);

        public string GetHandledType()
        {
            return InSceneNodeType.Name;
        }

        public string GetSortingKey(string name)
        {
            return name;
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
            if (!IsCacheActive())
            {
                EditorGUILayout.LabelField("Scene GameObject->GameObject");
                EditorGUILayout.LabelField("dependency type not loaded!");
            }
            
            EditorPrefUtilities.TogglePref(SyncPref, "Sync to Hierarchy:");
            EditorPrefUtilities.TogglePref(AutoRefreshPref, "Auto refresh scene switch:");

            GameObject newSelection = EditorGUILayout.ObjectField(_currentNode, typeof(GameObject), true) as GameObject;

            bool selectionChanged = newSelection != null && newSelection != _currentNode;
            _currentNode = newSelection;
            
            if (selectionChanged || (_currentNode != null && GUILayout.Button("Select")))
            {
                _viewerWindow.ChangeSelection(_currentNode.GetHashCode().ToString(), InSceneNodeType.Name);
            }
            
            AutoRefreshSceneAfterChange();
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
        
        public bool HandlesCurrentNode()
        {
            return _currentNode != null;
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

                if (SyncPref.GetValue())
                {
                    _viewerWindow.ChangeSelection(hashCode, InSceneNodeType.Name);
                }

                _viewerWindow.Repaint();
            }
        }

        private bool IsCacheActive()
        {
            return _viewerWindow.IsCacheAndResolverTypeActive(cacheType, resolverType) &&
                   _viewerWindow.IsCacheAndResolverTypeLoaded(cacheType, resolverType);
        }

        private void AutoRefreshSceneAfterChange()
        {
            if (!AutoRefreshPref.GetValue())
            {
                return;
            }

            if (!IsCacheActive())
            {
                return;
            }
            
            int loadedScenesKey = GetLoadedScenesKey();
            
            if (_currentLoadedSceneKey != 0 && loadedScenesKey != _currentLoadedSceneKey)
            {
                _viewerWindow.RefreshContext(cacheType, resolverType, null);
            }
            
            _currentLoadedSceneKey = loadedScenesKey;
        }

        private int GetLoadedScenesKey()
        {
            int result = 0;
            
            for (int i = 0; i < EditorSceneManager.loadedSceneCount; ++i)
            {
                Scene scene = EditorSceneManager.GetSceneAt(i);
                result ^= scene.name.GetHashCode();
            }

            return result;
        }
    }
}