using System;
using System.Collections.Generic;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public class AssetTypeHandler : ITypeHandler
    {
        private Object _selectedAsset;
        private AssetRelationsViewerWindow _viewerWindow;

        private PrefValueBool _explorerSyncModePref = new PrefValueBool("DirtyOnChange", false);

        public string GetHandledType()
        {
            return AssetNodeType.Name;
        }

        public string GetSortingKey(string name)
        {
            return $"Asset {name}";
        }

        public VisualizationNodeData CreateNodeCachedData(string id)
        {
            return new AssetVisualizationNodeData(id, GetHandledType());
        }

        public void SelectInEditor(string id)
        {
            string guid = NodeDependencyLookupUtility.GetGuidFromAssetId(id);
            long fileId = long.Parse(NodeDependencyLookupUtility.GetFileIdFromAssetId(id));

            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object[] allAssets = NodeDependencyLookupUtility.LoadAllAssetsAtPath(path);

            foreach (Object asset in allAssets)
            {
                if (asset == null)
                {
                    return;
                }
                
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string aguid, out long afileId);

                if (afileId == fileId)
                {
                    Selection.activeObject = asset;
                    return;
                }
            }
        }

        public void OnGui()
        {
            DisplayFilterOptions();
        }

        public void OnSelectAsset(string id, string type)
        {
            if (type == GetHandledType())
            {
                _selectedAsset = NodeDependencyLookupUtility.GetAssetById(id);
            }
            else
            {
                _selectedAsset = null;
            }
        }

        public void InitContext(NodeDependencyLookupContext nodeDependencyLookupContext,
            AssetRelationsViewerWindow window, INodeHandler nodeHandler)
        {
            _viewerWindow = window;
            Selection.selectionChanged += HandleSyncToExplorer;
        }

        public bool HandlesCurrentNode()
        {
            return _selectedAsset != null;
        }
        
        private void HandleSyncToExplorer()
        {
            if (_explorerSyncModePref.GetValue())
            {
                _viewerWindow.OnAssetSelectionChanged();
            }
        }

        private void DisplayFilterOptions()
        {
            EditorGUILayout.BeginVertical();

            Object newSelectedAsset = EditorGUILayout.ObjectField(_selectedAsset, typeof(Object), false);

            if (newSelectedAsset != _selectedAsset)
            {
                string fileId = NodeDependencyLookupUtility.GetAssetIdForAsset(newSelectedAsset);
                _viewerWindow.ChangeSelection(fileId, GetHandledType());

                _selectedAsset = newSelectedAsset;
            }

            EditorPrefUtilities.TogglePref(_explorerSyncModePref, "Sync to explorer:");
            EditorGUILayout.EndVertical();
        }

        private HashSet<string> CreateFilter(string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return null;

            return new HashSet<string>(AssetDatabase.FindAssets(filter));
        }
    }
}