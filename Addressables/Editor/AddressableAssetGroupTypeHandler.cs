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
		public override Texture2D AssetPreviewTexture
		{
			get { return null; }
		}

		public override Texture2D ThumbNailTexture
		{
			get { return null; }
		}
	}

	public class AddressableAssetGroupTypeHandler : ITypeHandler
	{
		private string[] m_nodes = new string[0];
		private string[] m_filteredNodes = new string[0];

		private string m_selectedGroupId = String.Empty;
		private string m_filter = String.Empty;

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

			HashSet<string> nodes = new HashSet<string>();

			foreach (KeyValuePair<string,CreatedDependencyCache> pair in context.CreatedCaches)
			{
				List<IDependencyMappingNode> resolvedNodes = new List<IDependencyMappingNode>();
				pair.Value.Cache.AddExistingNodes(resolvedNodes);

				foreach (IDependencyMappingNode node in resolvedNodes)
				{
					if(node.Type == AddressableAssetGroupNodeType.Name)
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

			string newFilter = EditorGUILayout.TextField("Filter:", m_filter);

			if (newFilter != m_filter)
			{
				m_filter = newFilter;
				HashSet<string> filteredNodes = new HashSet<string>();

				foreach (string node in m_nodes)
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
				m_selectedGroupId = String.Empty;
		}
	}

	public class AddressableGroupNodeHandler : INodeHandler
	{
		public string GetHandledNodeType()
		{
			return AddressableAssetGroupNodeType.Name;
		}

		public void CalculateOwnFileSize(Node node, NodeDependencyLookupContext stateContext)
		{
			HashSet<Node> addedNodes = new HashSet<Node>();
			HashSet<Node> addedFiles = new HashSet<Node>();

			GetTreeNodes(node, stateContext, addedNodes, addedFiles, 0);

			int size = 0;

			foreach (Node addedNode in addedFiles)
			{
				Node.NodeSize ownNodeSize = NodeDependencyLookupUtility.GetNodeSize(addedNode, stateContext, false);

				if (ownNodeSize.ContributesToTreeSize && addedNode != node)
				{
					size += ownNodeSize.Size;
				}
			}

			node.OwnSize = new Node.NodeSize {Size = size, ContributesToTreeSize = false};
		}

		private void GetTreeNodes(Node node, NodeDependencyLookupContext stateContext, HashSet<Node> addedNodes, HashSet<Node> addedFiles, int depth)
		{
			if (addedNodes.Contains(node))
			{
				return;
			}

			addedNodes.Add(node);

			if (depth > 1)
			{
				foreach (Connection referencerConnection in node.Referencers)
				{
					if (referencerConnection.Node.Type == AddressableAssetGroupNodeType.Name)
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

			foreach (Connection dependency in node.Dependencies)
			{
				if (!stateContext.DependencyTypeLookup.GetDependencyType(dependency.DependencyType).IsHard)
				{
					continue;
				}

				string dependencyNodeType = dependency.Node.Type;

				if (dependencyNodeType == AssetNodeType.Name || dependencyNodeType == FileNodeType.Name)
				{
					GetTreeNodes(dependency.Node, stateContext, addedNodes, addedFiles, depth + 1);
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
			string name = id;
			string concreteType = "AddressableAssetGroupBundle";

			wasCached = false;
			return new Node(id, type, name, concreteType, 0);
		}

		public long GetChangedTimeStamp(string id)
		{
			return -1;
		}

		public void InitNodeDataInformation()
		{
		}

		public void SaveCaches()
		{
		}

		public void InitContext(NodeDependencyLookupContext nodeDependencyLookupContext)
		{
		}
	}
}
