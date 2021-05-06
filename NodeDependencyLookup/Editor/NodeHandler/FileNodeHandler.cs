using System.IO;
using System.Linq;
using UnityEditor;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    /**
	 * NodeHandler for files
	 */
    public class FileNodeHandler : INodeHandler
    {
        private string[] HandledTypes = {"File"};
		
        public string GetId()
        {
            return "FileNodeHandler";
        }

        public string[] GetHandledNodeTypes()
        {
            return HandledTypes;
        }
		
        public int GetOwnFileSize(string id, string type, NodeDependencyLookupContext stateContext)
        {
            return NodeDependencyLookupUtility.GetPackedAssetSize(id);
        }

        public bool IsNodePackedToApp(string id, string type)
        {
            string path = AssetDatabase.GUIDToAssetPath(id);

            if (IsNodeEditorOnly(id, type))
            {
                return false;
            }

            if (IsSceneAndPacked(path) || IsInResources(path))
            {
                return true;
            }

            return false;
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