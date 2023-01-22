using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	public class FileTypeHandler : ITypeHandler
	{
		private Object _selectedAsset;

		public string GetHandledType()
		{
			return FileNodeType.Name;
		}

		public string GetSortingKey(string name)
		{
			return $"File {name}";
		}

		public VisualizationNodeData CreateNodeCachedData(Node node)
		{
			return new FileVisualizationNodeData(node);
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
		}

		public bool HandlesCurrentNode()
		{
			return _selectedAsset != null;
		}
	}
}
