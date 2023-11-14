using System;
using System.Collections.Generic;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	[UsedImplicitly]
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

		public VisualizationNodeData CreateNodeCachedData(Node node)
		{
			return new AssetVisualizationNodeData(node);
		}

		public string GetNodeDisplayName(Node node)
		{
			return node.Name;
		}

		public void SelectInEditor(string id)
		{
			var guid = NodeDependencyLookupUtility.GetGuidFromAssetId(id);
			var fileId = long.Parse(NodeDependencyLookupUtility.GetFileIdFromAssetId(id));

			var path = AssetDatabase.GUIDToAssetPath(guid);
			var allAssets = NodeDependencyLookupUtility.LoadAllAssetsAtPath(path);

			foreach (var asset in allAssets)
			{
				if (asset == null)
				{
					return;
				}

				AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var aguid, out long afileId);

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
			AssetRelationsViewerWindow window)
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

			var newSelectedAsset = EditorGUILayout.ObjectField(_selectedAsset, typeof(Object), false);

			if (newSelectedAsset != _selectedAsset)
			{
				var fileId = NodeDependencyLookupUtility.GetAssetIdForAsset(newSelectedAsset);
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