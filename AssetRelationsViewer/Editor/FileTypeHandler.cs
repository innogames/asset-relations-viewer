using System.IO;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	[UsedImplicitly]
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

		public string GetNodeDisplayName(Node node)
		{
			return Path.GetFileName(node.Name);
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
			_selectedAsset = type == GetHandledType() ? AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(id)) : null;
		}

		public void InitContext(NodeDependencyLookupContext nodeDependencyLookupContext,
			AssetRelationsViewerWindow window)
		{
		}

		public bool HandlesCurrentNode()
		{
			return _selectedAsset != null;
		}
	}
}