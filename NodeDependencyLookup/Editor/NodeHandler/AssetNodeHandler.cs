using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class AssetNodeType
	{
		public const string Name = "Asset";
	}

	/**
	 * NodeHandler for assets
	 */
	public class AssetNodeHandler : INodeHandler
	{
		public string GetId()
		{
			return "AssetNodeHandler";
		}

		public string GetHandledNodeType()
		{
			return AssetNodeType.Name;
		}
		
		public int GetOwnFileSize(string type, string id, string key,
			NodeDependencyLookupContext stateContext,
			Dictionary<string, NodeDependencyLookupUtility.NodeSize> ownSizeCache)
		{
			Node node = stateContext.RelationsLookup.GetNode(key);
			
			foreach (Connection dependency in node.Dependencies)
			{
				if (dependency.Type == FileNodeType.Name)
				{
					Node dependencyNode = dependency.Node;
					return NodeDependencyLookupUtility.GetOwnNodeSize(dependencyNode.Id, dependencyNode.Type,
						dependencyNode.Key, stateContext, ownSizeCache);
				}
			}

			return 0;
		}

		public bool IsNodePackedToApp(string id, string type, bool alwaysExcluded = false)
		{
			if (alwaysExcluded)
			{
				return !IsNodeEditorOnly(id, type);
			}
			
			string path = AssetDatabase.GUIDToAssetPath(NodeDependencyLookupUtility.GetGuidFromAssetId(id));
			return IsSceneAndPacked(path) || IsInResources(path) || id.StartsWith("0000000");
		}

		public bool IsNodeEditorOnly(string id, string type)
		{
			string path = AssetDatabase.GUIDToAssetPath(NodeDependencyLookupUtility.GetGuidFromAssetId(id));
			return path.Contains("/Editor/");
		}

		public bool ContributesToTreeSize()
		{
			return false;
		}

		public string GetName(string id)
		{
			Object asset = NodeDependencyLookupUtility.GetAssetById(id);
			string guid = NodeDependencyLookupUtility.GetGuidFromAssetId(id);
			string path = AssetDatabase.GUIDToAssetPath(guid);

			if (asset != null)
			{
				return $"{asset.name}";
			}

			if (!string.IsNullOrEmpty(path))
			{
				return path;
			}

			return id; 
		}

		public string GetTypeName(string id)
		{
			Object asset = NodeDependencyLookupUtility.GetAssetById(id);
			return asset != null ? asset.GetType().Name : "Not found";
		}

		public void InitContext(NodeDependencyLookupContext nodeDependencyLookupContext)
		{
			// nothing to do
		}
		
		private bool IsSceneAndPacked(string path)
		{
			if (Path.GetExtension(path).Equals(".unity"))
			{
				return EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path.Equals(path));
			}

			return false;
		}

		private bool IsInResources(string path)
		{
			return path.Contains("/Resources/");
		}
	}
}
