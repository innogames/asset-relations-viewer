using System;
using System.Collections.Generic;
using System.Linq;
using Com.Innogames.Core.Frontend.AssetRelationsViewer;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup.Addressables
{
	public class AddressableGroupVisualizationNodeData : VisualizationNodeData
	{
		public override Texture2D AssetPreviewTexture => null;

		public override Texture2D ThumbNailTexture => null;
	}

	public class AddressableAssetGroupTypeHandler : ITypeHandler
	{
		private string[] m_nodes = new string[0];
		private string[] m_filteredNodes = new string[0];

		private string m_selectedGroupId = string.Empty;
		private string m_filter = string.Empty;

		private AddressableGroupNodeHandler _nodeHandler;

		private int m_dropDownIndex = 0;

		private AssetRelationsViewerWindow _viewerWindow;

		public string GetHandledType()
		{
			return AddressableAssetGroupNodeType.Name;
		}

		public string GetSortingKey(string name)
		{
			return $"AddressableAssetGroup {name}";
		}

		public VisualizationNodeData CreateNodeCachedData(Node node)
		{
			return new AddressableGroupVisualizationNodeData();
		}

		public string GetNodeDisplayName(Node node)
		{
			return node.Name;
		}

		public void SelectInEditor(string id)
		{
		}

		public void InitContext(NodeDependencyLookupContext context, AssetRelationsViewerWindow viewerWindow)
		{
			_viewerWindow = viewerWindow;

			var nodes = new HashSet<string>();

			foreach (var pair in context.CreatedCaches)
			{
				var resolvedNodes = new List<IDependencyMappingNode>();
				pair.Value.Cache.AddExistingNodes(resolvedNodes);

				foreach (var node in resolvedNodes)
				{
					if (node.Type == AddressableAssetGroupNodeType.Name)
						nodes.Add(node.Id);
				}
			}

			m_nodes = nodes.ToArray();
			m_filteredNodes = nodes.ToArray();
		}

		public bool HandlesCurrentNode()
		{
			return !string.IsNullOrEmpty(m_selectedGroupId);
		}

		public void OnGui()
		{
			if (m_nodes.Length == 0)
			{
				EditorGUILayout.LabelField("AddressableGroupTempCache not activated");
				EditorGUILayout.LabelField("or no addressable groups found");
				return;
			}

			EditorGUILayout.LabelField("Selected Group:");
			EditorGUILayout.LabelField(m_selectedGroupId);
			EditorGUILayout.Space();

			var newFilter = EditorGUILayout.TextField("Filter:", m_filter);

			if (newFilter != m_filter)
			{
				m_filter = newFilter;
				var filteredNodes = new HashSet<string>();

				foreach (var node in m_nodes)
				{
					if (node.Contains(m_filter))
						filteredNodes.Add(node);
				}

				m_filteredNodes = filteredNodes.ToArray();
			}

			m_dropDownIndex = EditorGUILayout.Popup("Groups: ", m_dropDownIndex, m_filteredNodes);

			if (GUILayout.Button("Select"))
			{
				m_selectedGroupId = m_filteredNodes[m_dropDownIndex];
				_viewerWindow.ChangeSelection(m_selectedGroupId, AddressableAssetGroupNodeType.Name);
			}
		}

		public void OnSelectAsset(string id, string type)
		{
			if (type == AddressableAssetGroupNodeType.Name)
				m_selectedGroupId = id;
			else
				m_selectedGroupId = string.Empty;
		}
	}

	public class AddressableGroupNodeHandler : INodeHandler
	{
		public string GetHandledNodeType()
		{
			return AddressableAssetGroupNodeType.Name;
		}

		public void InitializeOwnFileSize(Node node, NodeDependencyLookupContext stateContext, bool updateNodeData)
		{
			// nothing to do
		}

		public void CalculateOwnFileSize(Node node, NodeDependencyLookupContext stateContext, bool updateNodeData)
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

			node.OwnSize = new Node.NodeSize {Size = size, ContributesToTreeSize = false};
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

		public bool IsNodePackedToApp(Node node, bool alwaysExclude)
		{
			return true;
		}

		public bool IsNodeEditorOnly(string id, string type)
		{
			return false;
		}

		public Node CreateNode(string id, string type, bool update, out bool wasCached)
		{
			var name = id;
			var concreteType = "AddressableAssetGroupBundle";

			wasCached = false;
			return new Node(id, type, name, concreteType);
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