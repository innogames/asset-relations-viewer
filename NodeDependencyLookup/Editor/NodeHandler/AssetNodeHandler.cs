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
				if (dependency.DependencyType == AssetToFileDependency.Name)
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

		public void GetNameAndType(string id, out string name, out string type)
		{
			Object asset = NodeDependencyLookupUtility.GetAssetById(id);
			string guid = NodeDependencyLookupUtility.GetGuidFromAssetId(id);
			string path = AssetDatabase.GUIDToAssetPath(guid);

			if (asset != null)
			{
				name = $"{asset.name}";
				type = asset.GetType().Name;
				return;
			}

			if (!string.IsNullOrEmpty(path))
			{
				name = path;
				type = "Unknown";
			}

			name = id;
			type = "Unknown";
		}

		public long GetChangedTimeStamp(string id)
		{
			return NodeDependencyLookupUtility.GetTimeStampForFileId(id);
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
