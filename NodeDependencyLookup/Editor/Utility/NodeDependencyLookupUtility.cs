using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;
#if UNITY_2019_2_OR_NEWER
using UnityEditor.Experimental;

#endif

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    /// <summary>
    /// Contains utility functions that are needed by the AssetRelationsWindow but should be independent from the class so they can be used from other places
    /// </summary>
    public static class NodeDependencyLookupUtility
    {
        public const string DEFAULT_CACHE_SAVE_PATH = "AssetRelationsViewer/Cache/";

        [MenuItem("Window/Node Dependency Lookup/Clear Cache Files")]
        public static void ClearCacheFiles()
        {
            List<Type> types = GetTypesForBaseType(typeof(IDependencyCache));

            foreach (Type type in types)
            {
                IDependencyCache cache = InstantiateClass<IDependencyCache>(type);
                cache.ClearFile(DEFAULT_CACHE_SAVE_PATH);
            }
        }

        [MenuItem("Window/Node Dependency Lookup/Clear Cached Contexts")]
        public static void ClearCachedContexts()
        {
            NodeDependencyLookupContext.ResetContexts();
        }

        public static bool IsResolverActive(CreatedDependencyCache createdCache, string id, string connectionType)
        {
            Dictionary<string, CreatedResolver> resolverUsagesLookup = createdCache.ResolverUsagesLookup;
            return resolverUsagesLookup.ContainsKey(id) && resolverUsagesLookup[id].ConnectionTypes.Contains(connectionType);
        }

        public static long[] GetTimeStampsForFiles(string[] pathes)
        {
            long[] timestamps = new long[pathes.Length];

            for (int i = 0; i < pathes.Length; ++i)
            {
                timestamps[i] = GetTimeStampForPath(pathes[i]);
            }

            return timestamps;
        }

        public static long GetTimeStampForFileId(string fileId)
        {
            string guid = GetGuidFromAssetId(fileId);
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (string.IsNullOrEmpty(path))
            {
                return 0;
            }

            return GetTimeStampForPath(path);
        }

        public static long GetTimeStampForPath(string path)
        {
            long fileTimeStamp = File.GetLastWriteTime(path).ToFileTimeUtc();
            long metaFileTimeStamp = File.GetLastWriteTime(path + ".meta").ToFileTimeUtc();
            long timeStamp = Math.Max(fileTimeStamp, metaFileTimeStamp);

            return timeStamp;
        }

        public static void LoadDependencyLookupForCaches(NodeDependencyLookupContext stateContext,
            ResolverUsageDefinitionList resolverUsageDefinitionList, ProgressBase progress, bool loadCache = true,
            bool updateCache = true, bool saveCache = true, string fileDirectory = DEFAULT_CACHE_SAVE_PATH)
        {
            stateContext.UpdateFromDefinition(resolverUsageDefinitionList);

            List<CreatedDependencyCache> caches = stateContext.GetCaches();

            foreach (CreatedDependencyCache cacheUsage in caches)
            {
                if (cacheUsage.ResolverUsages.Count == 0)
                {
                    continue;
                }
                
                IDependencyCache cache = cacheUsage.Cache;

                if (loadCache && !cacheUsage.IsLoaded)
                {
                    cache.Load(fileDirectory);
                    cacheUsage.IsLoaded = true;
                }

                if (updateCache && cache.NeedsUpdate(progress))
                {
                    if (cache.CanUpdate())
                    {
                        cache.Update(progress);

                        if (saveCache)
                        {
                            cache.Save(fileDirectory);
                        }
                    }
                    else
                    {
                        Debug.LogErrorFormat("{0} could not be updated", cache.GetType().FullName);
                    }
                }
            }

            RelationLookup.RelationsLookup lookup = new RelationLookup.RelationsLookup();
            lookup.Build(caches);

            stateContext.RelationsLookup = lookup;
        }

        public static Dictionary<string, INodeHandler> BuildNodeHandlerLookup()
        {
            Dictionary<string, INodeHandler> result = new Dictionary<string, INodeHandler>();

            foreach (INodeHandler nodeHandler in GetNodeHandlers())
            {
                result.Add(nodeHandler.GetId(), nodeHandler);
            }

            return result;
        }

        public static List<INodeHandler> GetNodeHandlers()
        {
            List<Type> types = GetTypesForBaseType(typeof(INodeHandler));
            List<INodeHandler> nodeHandlers = new List<INodeHandler>();

            foreach (Type type in types)
            {
                nodeHandlers.Add(InstantiateClass<INodeHandler>(type));
            }

            return nodeHandlers;
        }

        /// <summary>
        /// Used to get the size of an asset inside the packed build.
        /// Currently sounds are not correct since the file isnt going to be written into the libraray in the final format.
        /// </summary>
        public static int GetPackedAssetSize(string assetId)
        {
            string fullpath = GetLibraryFullPath(GetGuidFromAssetId(assetId));

            if (!String.IsNullOrEmpty(fullpath) && File.Exists(fullpath))
            {
                FileInfo info = new FileInfo(fullpath);
                return (int) (info.Length / 1024);
            }

            return 0;
        }

        public static string GetLibraryFullPath(string guid)
        {
            if (String.IsNullOrEmpty(guid))
            {
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (Path.GetExtension(path).Equals(".asset"))
            {
                return path;
            }

#if UNITY_2019_2_OR_NEWER
            if (EditorSettings.assetPipelineMode == AssetPipelineMode.Version1)
            {
                return GetAssetDatabaseVersion1LibraryDataPath(guid);
            }
            else
            {
#if UNITY_2020_2_OR_NEWER
                Hash128 artifactHash = AssetDatabaseExperimental.LookupArtifact(new ArtifactKey(new GUID(guid))).value;
#else
                Hash128 artifactHash = AssetDatabaseExperimental.GetArtifactHash(guid);
#endif

                if (!artifactHash.isValid)
                {
                    return null;
                }

#if UNITY_2020_2_OR_NEWER
                ArtifactID artifactID = new ArtifactID();
                artifactID.value = artifactHash;
                AssetDatabaseExperimental.GetArtifactPaths(artifactID, out string[] paths);
#else
                AssetDatabaseExperimental.GetArtifactPaths(artifactHash, out string[] paths);
#endif

                foreach (string artifactPath in paths)
                {
                    if (artifactPath.EndsWith(".info"))
                        continue;

                    return Path.GetFullPath(artifactPath);
                }
            }
#else // For older unity versions that dont have asset database V2 yet
                return return GetAssetDatabaseVersion1LibraryDataPath(guid);
#endif

            return null;
        }

        private static string GetAssetDatabaseVersion1LibraryDataPath(string guid)
        {
            return Application.dataPath + "../../Library/metadata/" + guid.Substring(0, 2) + "/" + guid;
        }

        /// <summary>
        /// Right now this only works if the asset or one of its parents (referencers) are in a packaged scene or in a resources folder.
        /// If the asset is just in a bundle this is currently not tracked. Trying to find a solution for this.
        /// </summary>
        public static bool IsNodePackedToApp(string id, string type, NodeDependencyLookupContext stateContext)
        {
            return IsNodePackedToApp(id, type, stateContext, new HashSet<string>());
        }

        /// <summary>
        /// Right now this only works if the asset or one of its parents (referencers) are in a packaged scene or in a resources folder.
        /// If the asset is just in a bundle this is currently not tracked. Trying to find a solution for this.
        /// </summary>
        public static bool IsNodePackedToApp(string id, string type, NodeDependencyLookupContext stateContext,
            HashSet<string> visitedKeys)
        {
            if (visitedKeys.Contains(id))
            {
                return false;
            }

            visitedKeys.Add(id);

            Node node = stateContext.RelationsLookup.GetNode(id, type);

            if (node == null)
            {
                return false;
            }

            foreach (KeyValuePair<string, INodeHandler> pair in stateContext.NodeHandlerLookup)
            {
                INodeHandler nodeHandler = pair.Value;

                if (nodeHandler.GetHandledNodeTypes().Contains(type) && nodeHandler.IsNodePackedToApp(id, type))
                {
                    return true;
                }
            }

            foreach (Connection connection in node.Referencers)
            {
                Node refNode = connection.Node;

                if (!stateContext.ConnectionTypeLookup.GetDependencyType(connection.Type).IsIndirect &&
                    IsNodePackedToApp(refNode.Id, refNode.Type, stateContext, visitedKeys))
                {
                    return true;
                }
            }

            return false;
        }

        public static int GetNodeSize(bool own, bool tree, string id, string type, HashSet<string> traversedNodes,
            NodeDependencyLookupContext stateContext, Dictionary<string, int> ownSizeCache = null)
        {
            if (ownSizeCache == null)
            {
                ownSizeCache = new Dictionary<string, int>();
            }
            
            string key = GetNodeKey(id, type);

            if (traversedNodes.Contains(key) || !stateContext.NodeHandlerLookup.ContainsKey(type))
            {
                return 0;
            }

            traversedNodes.Add(key);

            int size = 0;

            INodeHandler nodeHandler = stateContext.NodeHandlerLookup[type];

            if (own && (!tree || nodeHandler.ContributesToTreeSize()))
            {
                if (!ownSizeCache.ContainsKey(key))
                {
                    ownSizeCache[key] = nodeHandler.GetOwnFileSize(id, type, stateContext);
                }
                
                size += ownSizeCache[key];
            }

            if (tree)
            {
                Node node = stateContext.RelationsLookup.GetNode(id, type);

                if (node != null)
                {
                    foreach (Connection connection in node.GetRelations(RelationType.DEPENDENCY))
                    {
                        if (stateContext.ConnectionTypeLookup.GetDependencyType(connection.Type).IsHard)
                        {
                            Node childNode = connection.Node;
                            size += GetNodeSize(true, true, childNode.Id, childNode.Type, traversedNodes, stateContext, ownSizeCache);
                        }
                    }
                }
            }

            return size;
        }

        public static string GetGuidFromAssetId(string id)
        {
            return id.Split('_')[0];
        }

        public static string GetFileIdFromAssetId(string id)
        {
            return id.Split('_')[1];
        }

        public static string GetAssetIdForAsset(Object asset)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileId);
            return $"{guid}_{fileId}";
        }

        public static Object GetAssetById(string id)
        {
            string fileId = GetFileIdFromAssetId(id);
            string guid = GetGuidFromAssetId(id);
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Profiler.BeginSample("LoadAllAssets");
            Object[] assetsAtPath = LoadAllAssetsAtPath(path);
            Profiler.EndSample();

            foreach (Object asset in assetsAtPath)
            {
                if (asset == null)
                {
                    continue;
                }
                
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string aguid, out long afileId);
                if (afileId.ToString() == fileId)
                {
                    return asset;
                }
            }

            return null;
        }

        public static Object GetMainAssetById(string id)
        {
            string guid = GetGuidFromAssetId(id);
            string path = AssetDatabase.GUIDToAssetPath(guid);

            return AssetDatabase.LoadAssetAtPath<Object>(path);
        }

        public static Object[] LoadAllAssetsAtPath(string path)
        {
            if (path.EndsWith(".unity"))
            {
                return new [] {AssetDatabase.LoadMainAssetAtPath(path)};
            }

            return AssetDatabase.LoadAllAssetsAtPath(path);
        }

        public static string[] GetAllAssetPathes(ProgressBase progress, bool unityBuiltin)
        {
            string[] pathes = AssetDatabase.GetAllAssetPaths();

            pathes = AssetDatabase.GetAllAssetPaths();

            List<string> pathList = new List<string>();

            foreach (string path in pathes)
            {
                pathList.Add(path);
            }

            return pathList.ToArray();
        }

        public static void AddAssetsToList(HashSet<string> assetList, string path)
        {
            Object mainAsset = AssetDatabase.LoadAssetAtPath<Object>(path);
            Object[] allAssets = LoadAllAssetsAtPath(path);

            foreach (Object asset in allAssets)
            {
                if (asset == null)
                {
                    continue;
                }
                
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileID);

                if (!(mainAsset is GameObject) || (AssetDatabase.IsMainAsset(asset) || AssetDatabase.IsSubAsset(asset)))
                {
                    assetList.Add($"{guid}_{fileID}");
                }
            }
        }

        public static List<Type> GetTypesForBaseType(Type interfaceType)
        {
            List<Type> result = new List<Type>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsClass && !type.IsAbstract && interfaceType.IsAssignableFrom(type))
                    {
                        try
                        {
                            result.Add(type);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning(e);
                        }
                    }
                }
            }

            return result;
        }

        public static T InstantiateClass<T>(Type type) where T : class
        {
            return Activator.CreateInstance(type) as T;
        }

        public static string GetNodeKey(string id, string type)
        {
            return id + "___" + type;
        }

        /**
         * Return the dependency lookup for Objects using the ObjectDependencyResolver
         */
        public static void BuildDefaultAssetLookup(NodeDependencyLookupContext stateContext, bool loadFromCache,
            string savePath,
            ProgressBase progress)
        {
            ResolverUsageDefinitionList usageDefinitionList = new ResolverUsageDefinitionList();
            usageDefinitionList.Add<AssetDependencyCache, ObjectSerializedDependencyResolver>();

            LoadDependencyLookupForCaches(stateContext, usageDefinitionList, progress, loadFromCache, true, false,
                savePath);
        }
    }
}