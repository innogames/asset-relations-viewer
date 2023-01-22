using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

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
        private MethodInfo getPreviewTextureMethod;
        private MethodInfo getAudioSizeMethod;
        private MethodInfo getStorageMemorySizeMethod;
        
        public FileNodeHandler()
        {
            Type spriteAtlasExtensionsType = typeof(SpriteAtlasExtensions); 
            getPreviewTextureMethod = spriteAtlasExtensionsType.GetMethod("GetPreviewTextures", BindingFlags.Static | BindingFlags.NonPublic);

            Assembly unityAssembly = Assembly.Load("UnityEditor.dll");
            Type textureUtilType = unityAssembly.GetType("UnityEditor.TextureUtil");
            getStorageMemorySizeMethod = textureUtilType.GetMethod("GetStorageMemorySize", BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);
            
            Type audioUtilType = unityAssembly.GetType("UnityEditor.AudioImporter");
            getAudioSizeMethod = audioUtilType.GetMethod("get_compSize", BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public string GetHandledNodeType()
        {
            return FileNodeType.Name;
        }

        public Node.NodeSize GetOwnFileSize(Node node, NodeDependencyLookupContext stateContext)
        {
            bool isSpriteOfSpriteAtlas = IsSpriteOfSpriteAtlas(node);
            bool contributesToTreeSize = !isSpriteOfSpriteAtlas;

            return new Node.NodeSize
            {
                Size = NodeDependencyLookupUtility.GetPackedAssetSize(node.Id) + GetSpriteAtlasSize(node) + GetAudioClipSize(node), 
                ContributesToTreeSize = contributesToTreeSize
            };
        }

        private int GetSpriteAtlasSize(Node node)
        {
            Type spriteAtlasType = typeof(SpriteAtlas);
            int size = 0;
            
            foreach (Connection connection in node.Referencers)
            {
                if (connection.Node.ConcreteType == spriteAtlasType.FullName)
                {
                    string guid = NodeDependencyLookupUtility.GetGuidFromAssetId(connection.Node.Id);
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    SpriteAtlas spriteAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                    Texture2D[] previewTextures = getPreviewTextureMethod.Invoke(null, new object[]{spriteAtlas}) as Texture2D[];
                            
                    foreach (Texture2D previewTexture in previewTextures)
                    {
                        size += (int)getStorageMemorySizeMethod.Invoke(null, new object[] { previewTexture });;
                    }
                }
            }

            return size;
        }
        
        private int GetAudioClipSize(Node node)
        {
            Type spriteAtlasType = typeof(AudioClip);
            
            foreach (Connection connection in node.Referencers)
            {
                if (connection.Node.ConcreteType == spriteAtlasType.FullName)
                {
                    string guid = NodeDependencyLookupUtility.GetGuidFromAssetId(connection.Node.Id);
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AudioImporter importer = AudioImporter.GetAtPath(path) as AudioImporter;
                    return (int)getAudioSizeMethod.Invoke(importer, new object[] {});
                }
            }

            return 0;
        }

        private string spriteTypeName = typeof(Sprite).FullName;
        private string spriteAtlasTypeName = typeof(SpriteAtlas).FullName;

        private bool IsSpriteOfSpriteAtlas(Node node)
        {
            foreach (Connection connection in node.Referencers)
            {
                if (connection.Node.ConcreteType == spriteTypeName)
                {
                    foreach (Connection cconnection in connection.Node.Referencers)
                    {
                        if (cconnection.Node.ConcreteType == spriteAtlasTypeName)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
            
            return false;
        }

        public bool IsNodePackedToApp(Node node, bool alwaysExcluded)
        {
            if (alwaysExcluded)
            {
                return !IsNodeEditorOnly(node.Id, node.Type);
            }
			
            string path = AssetDatabase.GUIDToAssetPath(node.Id);
            return IsSceneAndPacked(path) || IsInResources(path) || node.Id.StartsWith("0000000");
        }

        public bool IsNodeEditorOnly(string id, string type)
        {
            string path = AssetDatabase.GUIDToAssetPath(id);
            return path.Contains("/Editor/");
        }

        public void GetNameAndType(string id, out string name, out string type)
        {
            name = Path.GetFileName(AssetDatabase.GUIDToAssetPath(id));
            type = "File";
        }

        public long GetChangedTimeStamp(string id)
        {
            return NodeDependencyLookupUtility.GetTimeStampForFileId(id);
        }

        public void SaveCaches()
        {
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