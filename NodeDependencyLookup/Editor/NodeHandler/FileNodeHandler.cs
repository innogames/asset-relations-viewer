using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEditor.U2D;
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
        private class CachedData
        {
            public string Id;
            public int Size;
            public long TimeStamp;
        }

        private Dictionary<Node, string> nodeToArtifactPathLookup = new Dictionary<Node, string>();

        private MethodInfo getPreviewTextureMethod;
        private MethodInfo getAudioSizeMethod;
        private MethodInfo getStorageMemorySizeMethod;

        private string audioClipTypeName = typeof(AudioClip).FullName;
        private string spriteTypeName = typeof(Sprite).FullName;
        private string spriteAtlasTypeName = typeof(SpriteAtlas).FullName;

        private ConcurrentDictionary<string, CachedData> cachedSizeLookup = new ConcurrentDictionary<string, CachedData>();

        public FileNodeHandler()
        {
            Type spriteAtlasExtensionsType = typeof(SpriteAtlasExtensions);
            getPreviewTextureMethod = spriteAtlasExtensionsType.GetMethod("GetPreviewTextures", BindingFlags.Static | BindingFlags.NonPublic);

            Assembly unityAssembly = Assembly.Load("UnityEditor.dll");
            Type textureUtilType = unityAssembly.GetType("UnityEditor.TextureUtil");
            getStorageMemorySizeMethod = textureUtilType.GetMethod("GetStorageMemorySizeLong", BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);

            Type audioUtilType = unityAssembly.GetType("UnityEditor.AudioImporter");
            getAudioSizeMethod = audioUtilType.GetMethod("get_compSize", BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public string GetHandledNodeType()
        {
            return FileNodeType.Name;
        }


        private static long? GetCompressedSize(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return 0;
            }

            using (MemoryStream memoryStream = new MemoryStream())
            {
                FileInfo fileInfo = new FileInfo(path);
                if (!fileInfo.Exists)
                {
                    return null;
                }

                using (var compressionStream = new GZipStream(memoryStream, CompressionMode.Compress, true))

                using (FileStream originalFileStream = fileInfo.OpenRead())
                {
                    originalFileStream.CopyTo(compressionStream);
                }

                return memoryStream.Position;
            }
        }

        public void InitializeOwnFileSize(Node node, NodeDependencyLookupContext stateContext)
        {
            bool isSpriteOfSpriteAtlas = IsSpriteOfSpriteAtlas(node);
            bool contributesToTreeSize = !isSpriteOfSpriteAtlas;

            int size = GetSpriteAtlasSize(node) + GetAudioClipSize(node);

            string guid = NodeDependencyLookupUtility.GetGuidFromAssetId(node.Id);
            nodeToArtifactPathLookup.Add(node, NodeDependencyLookupUtility.GetLibraryFullPath(guid));

            node.OwnSize.Size = size;
            node.OwnSize.ContributesToTreeSize = contributesToTreeSize;
        }

        public void CalculateOwnFileSize(Node node, NodeDependencyLookupContext stateContext)
        {
            string id = node.Id;
            int packedAssetSize = 0;
            bool wasCached = cachedSizeLookup.TryGetValue(id, out CachedData cachedValue);
            bool timeStampChanged = !wasCached || cachedValue.TimeStamp != node.ChangedTimeStamp;

            if (wasCached && !timeStampChanged)
            {
                packedAssetSize = cachedValue.Size;
            }
            else
            {
                long? compressedSize = GetCompressedSize(nodeToArtifactPathLookup[node]);

                if (compressedSize != null)
                {
                    packedAssetSize = (int)compressedSize.Value;
                }
            }

            CachedData cachedData = new CachedData{Id = id, Size = packedAssetSize, TimeStamp = node.ChangedTimeStamp};

            if (wasCached)
            {
                cachedSizeLookup[id] = cachedData;
            }
            else
            {
                cachedSizeLookup.TryAdd(id, cachedData);
            }

            node.OwnSize.Size += packedAssetSize;
        }

        public void CalculateOwnFileDependencies(Node node, NodeDependencyLookupContext context, HashSet<Node> calculatedNodes)
        {
            // nothing to do
        }

        private int GetSpriteAtlasSize(Node node)
        {
            int size = 0;

            foreach (Connection connection in node.Referencers)
            {
                if (connection.Node.ConcreteType == spriteAtlasTypeName)
                {
                    string guid = NodeDependencyLookupUtility.GetGuidFromAssetId(connection.Node.Id);
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    SpriteAtlas spriteAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                    Texture2D[] previewTextures = getPreviewTextureMethod.Invoke(null, new object[]{spriteAtlas}) as Texture2D[];

                    foreach (Texture2D previewTexture in previewTextures)
                    {
                        size += Convert.ToInt32(getStorageMemorySizeMethod.Invoke(null, new object[] { previewTexture }));
                    }
                }
            }

            return size;
        }

        private int GetAudioClipSize(Node node)
        {
            foreach (Connection connection in node.Referencers)
            {
                if (connection.Node.ConcreteType == audioClipTypeName)
                {
                    string guid = NodeDependencyLookupUtility.GetGuidFromAssetId(connection.Node.Id);
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    AudioImporter importer = AudioImporter.GetAtPath(path) as AudioImporter;
                    return (int)getAudioSizeMethod.Invoke(importer, new object[] {});
                }
            }

            return 0;
        }

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

        public Node CreateNode(string id, string type, bool update, out bool wasCached)
        {
            string name = AssetDatabase.GUIDToAssetPath(id);
            string concreteType = "File";

            wasCached = false;
            return new Node(id, type, name, concreteType, 0);
        }

        private string GetCachePath()
        {
            string version = "2.0";
            string buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            return Path.Combine(NodeDependencyLookupUtility.DEFAULT_CACHE_PATH, $"FileNodeHandlerCache_{buildTarget}_{version}.cache");
        }

        public void InitNodeCreation()
        {
            cachedSizeLookup.Clear();

            string cachePath = GetCachePath();

            int offset = 0;
            byte[] bytes;

            bytes = File.Exists(cachePath) ? File.ReadAllBytes(cachePath) : new byte[16 * 1024];

            long length = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);

            for (int i = 0; i < length; ++i)
            {
                string id = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
                long size = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);
                long timeStamp = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);

                cachedSizeLookup.TryAdd(id, new CachedData{Id = id, Size = (int)size, TimeStamp = timeStamp});
            }
        }

        public void SaveCaches()
        {
            if (cachedSizeLookup.Count == 0)
            {
                return;
            }

            int offset = 0;
            byte[] bytes = new byte[512 * 1024];

            CacheSerializerUtils.EncodeLong(cachedSizeLookup.Count, ref bytes, ref offset);

            foreach (var pair in cachedSizeLookup)
            {
                CacheSerializerUtils.EncodeString(pair.Value.Id, ref bytes, ref offset);
                CacheSerializerUtils.EncodeLong(pair.Value.Size, ref bytes, ref offset);
                CacheSerializerUtils.EncodeLong(pair.Value.TimeStamp, ref bytes, ref offset);

                bytes = CacheSerializerUtils.EnsureSize(bytes, offset);
            }

            File.WriteAllBytes(GetCachePath(), bytes);
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