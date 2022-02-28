using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    public class FileNodeType
    {
        public const string Name = "File";
    }

    /**
	 * NodeHandler for files
	 */
    public class FileNodeHandler : INodeHandler
    {
        public string GetId()
        {
            return "FileNodeHandler";
        }

        public string GetHandledNodeType()
        {
            return FileNodeType.Name;
        }
		
        public int GetOwnFileSize(string type, string id, string key,
            NodeDependencyLookupContext stateContext,
            Dictionary<string, NodeDependencyLookupUtility.NodeSize> ownSizeCache)
        {
            return NodeDependencyLookupUtility.GetPackedAssetSize(id);
        }

        public bool IsNodePackedToApp(string id, string type, bool alwaysExcluded)
        {
            if (alwaysExcluded)
            {
                return !IsNodeEditorOnly(id, type);
            }
			
            string path = AssetDatabase.GUIDToAssetPath(id);
            return IsSceneAndPacked(path) || IsInResources(path) || id.StartsWith("0000000");
        }

        public bool IsNodeEditorOnly(string id, string type)
        {
            string path = AssetDatabase.GUIDToAssetPath(id);
            return path.Contains("/Editor/");
        }

        public bool ContributesToTreeSize()
        {
            return true;
        }

        public void GetNameAndType(string id, out string name, out string type)
        {
            name = Path.GetFileName(AssetDatabase.GUIDToAssetPath(id));
            type = "File";
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