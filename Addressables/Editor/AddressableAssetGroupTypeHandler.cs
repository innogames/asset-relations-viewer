using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Com.Innogames.Core.Frontend.AssetRelationsViewer;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup.Addressables
{
	public class AddressableGroupVisualizationNodeData : VisualizationNodeData
	{
		public override Texture2D AssetPreviewTexture => null;

		public override Texture2D ThumbNailTexture => null;
	}

	/// <summary>
	/// TypeHandler for AddressableAssetGroups for the Unity Addressables package
	/// </summary>
	[UsedImplicitly]
	public class AddressableAssetGroupTypeHandler : ITypeHandler
	{
		private string[] _nodes = new string[0];
		private string[] _filteredNodes = new string[0];

		private string _selectedGroupId = string.Empty;
		private string _filter = string.Empty;

		private AddressableGroupNodeHandler _nodeHandler;

		private int m_dropDownIndex;

		private AssetRelationsViewerWindow _viewerWindow;

		public string GetHandledType() => AddressableAssetGroupNodeType.Name;

		public string GetSortingKey(string name) => $"AddressableAssetGroup {name}";

		public VisualizationNodeData CreateNodeCachedData(Node node) => new AddressableGroupVisualizationNodeData();

		public string GetNodeDisplayName(Node node) => node.Name;

		public void SelectInEditor(string id)
		{
		}

		public void InitContext(NodeDependencyLookupContext context, AssetRelationsViewerWindow viewerWindow)
		{
			_viewerWindow = viewerWindow;

			var bundleNodes = new HashSet<string>();
			var allNodes = context.RelationsLookup.GetAllNodes();

			foreach (var node in allNodes)
			{
				if (node.Type == AddressableAssetGroupNodeType.Name)
				{
					bundleNodes.Add(node.Id);
				}
			}

			_nodes = bundleNodes.ToArray();
			_filteredNodes = bundleNodes.ToArray();
		}

		public bool HandlesCurrentNode() => !string.IsNullOrEmpty(_selectedGroupId);

		public void OnGui()
		{
			if (_nodes.Length == 0)
			{
				EditorGUILayout.LabelField("AddressableGroupTempCache not activated");
				EditorGUILayout.LabelField("or no addressable groups found");
				return;
			}

			EditorGUILayout.LabelField("Selected Group:");
			EditorGUILayout.LabelField(_selectedGroupId);
			EditorGUILayout.Space();

			var newFilter = EditorGUILayout.TextField("Filter:", _filter);

			if (newFilter != _filter)
			{
				_filter = newFilter;
				var filteredNodes = new HashSet<string>();

				foreach (var node in _nodes)
				{
					if (node.Contains(_filter))
						filteredNodes.Add(node);
				}

				_filteredNodes = filteredNodes.ToArray();
			}

			m_dropDownIndex = EditorGUILayout.Popup("Groups: ", m_dropDownIndex, _filteredNodes);

			if (GUILayout.Button("Select"))
			{
				_selectedGroupId = _filteredNodes[m_dropDownIndex];
				_viewerWindow.ChangeSelection(_selectedGroupId, AddressableAssetGroupNodeType.Name);
			}
		}

		public void OnSelectAsset(string id, string type)
		{
			_selectedGroupId = type == AddressableAssetGroupNodeType.Name ? id : string.Empty;
		}
	}

	[UsedImplicitly]
	public class AddressableGroupNodeHandler : INodeHandler
	{
		public string GetHandledNodeType() => AddressableAssetGroupNodeType.Name;

		public void InitializeOwnFileSize(Node node, NodeDependencyLookupContext stateContext, bool updateNodeData)
		{
			// nothing to do
		}

		public void CalculateOwnFileSizeParallel(Node node, NodeDependencyLookupContext stateContext,
			bool updateNodeData)
		{
			// nothing to do
		}

		public void CalculateOwnFileDependencies(Node node, NodeDependencyLookupContext context,
			HashSet<Node> calculatedNodes)
		{
			var addedNodes = new HashSet<Node>();
			var addedFiles = new HashSet<Node>();

			GetTreeNodes(node, node, context, addedNodes, addedFiles, 0);

			var size = 0;

			foreach (var addedNode in addedFiles)
			{
				NodeDependencyLookupUtility.UpdateOwnFileSizeDependenciesForNode(addedNode, context, calculatedNodes);
				var ownNodeSize = addedNode.OwnSize;

				if (ownNodeSize.ContributesToTreeSize && addedNode != node)
				{
					size += ownNodeSize.Size;
				}
			}

			node.OwnSize = new Node.NodeSize { Size = size, ContributesToTreeSize = false };
		}

		private void GetTreeNodes(Node node, Node rootGroupNode, NodeDependencyLookupContext stateContext,
			HashSet<Node> addedNodes, HashSet<Node> addedFiles, int depth)
		{
			if (addedNodes.Contains(node))
			{
				return;
			}

			addedNodes.Add(node);

			if (depth > 1)
			{
				foreach (var referencerConnection in node.Referencers)
				{
					if (referencerConnection.Node.Type == AddressableAssetGroupNodeType.Name &&
					    referencerConnection.Node != rootGroupNode)
					{
						return;
					}
				}
			}

			if (node.Type == FileNodeType.Name)
			{
				addedFiles.Add(node);
				return;
			}

			foreach (var dependency in node.Dependencies)
			{
				if (!stateContext.DependencyTypeLookup.GetDependencyType(dependency.DependencyType).IsHard)
				{
					continue;
				}

				var dependencyNodeType = dependency.Node.Type;

				if (dependencyNodeType == AssetNodeType.Name || dependencyNodeType == FileNodeType.Name)
				{
					GetTreeNodes(dependency.Node, rootGroupNode, stateContext, addedNodes, addedFiles, depth + 1);
				}
			}
		}

		public bool IsNodePackedToApp(Node node, bool alwaysExclude) => true;

		public bool IsNodeEditorOnly(string id, string type) => false;

		public Node CreateNode(string id, string type, bool update, out bool wasCached)
		{
			var name = id;
			var concreteType = "AddressableAssetGroupBundle";

			wasCached = false;
			return new Node(id, type, name, concreteType);
		}

		public void CalculatePrecalculatableAsyncDataWhileCacheExecution(Node node, List<Task> taskList)
		{
			// Nothing to do
		}

		public void InitNodeCreation()
		{
			// nothing to do
		}

		public void SaveCaches()
		{
			// nothing to do
		}

		public void InitContext(NodeDependencyLookupContext nodeDependencyLookupContext)
		{
			// nothing to do
		}
	}
}
