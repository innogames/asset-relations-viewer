using System.IO;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	public class FileTypeHandler : ITypeHandler
	{
		private Object _selectedAsset;
		private AssetRelationsViewerWindow _viewerWindow;

		public string GetHandledType()
		{
			return FileNodeType.Name;
		}

		public string GetSortingKey(string name)
		{
			return $"File {name}";
		}

		public bool HasFilter()
		{
			return false;
		}

		public bool IsFiltered(string id)
		{
			return true;
		}

		public string GetName(string id)
		{
			return Path.GetFileName(AssetDatabase.GUIDToAssetPath(id));
		}

		public VisualizationNodeData CreateNodeCachedData(string id)
		{
			return new FileVisualizationNodeData(id, GetHandledType());
		}

		public void SelectInEditor(string id)
		{
			Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(id));
		}

		public void OnGui()
		{
			EditorGUILayout.BeginVertical();

			if (_selectedAsset != null)
			{
				EditorGUILayout.ObjectField(_selectedAsset, typeof(Object), false);
			}

			EditorGUILayout.EndVertical();
		}

		public void OnSelectAsset(string id, string type)
		{
			if (type == GetHandledType())
			{
				_selectedAsset = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(id));
			}
			else
			{
				_selectedAsset = null;
			}
		}

		public void InitContext(NodeDependencyLookupContext nodeDependencyLookupContext, AssetRelationsViewerWindow window)
		{
			_viewerWindow = window;
		}

		public bool HandlesCurrentNode()
		{
			return _selectedAsset != null;
		}
	}
}
