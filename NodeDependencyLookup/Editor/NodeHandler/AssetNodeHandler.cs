using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/**
	 * NodeHandler for assets
	 */
	public class AssetNodeHandler : INodeHandler
	{
		private string[] HandledTypes = {"Asset"};
		
		public string GetId()
		{
			return "AssetNodeHandler";
		}

		public string[] GetHandledNodeTypes()
		{
			return HandledTypes;
		}
		
		public int GetOwnFileSize(string type, string id, string key, HashSet<string> traversedNodes,
			NodeDependencyLookupContext stateContext)
		{
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
			return true;
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
