using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
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

        public static bool NeedsCacheUpdate(CreatedDependencyCache usage)
        {
            foreach (CreatedResolver resolverUsage in usage.ResolverUsages)
            {
                if (resolverUsage.IsActive)
                    return true;
            }

            return false;
        }

        public static void LoadDependencyLookupForCaches(NodeDependencyLookupContext stateContext,
            ResolverUsageDefinitionList resolverUsageDefinitionList, ProgressBase progress, bool loadCache = true,
            bool updateCache = true, bool saveCache = true, string fileDirectory = DEFAULT_CACHE_SAVE_PATH)
        {
            stateContext.UpdateFromDefinition(resolverUsageDefinitionList);

            List<CreatedDependencyCache> caches = stateContext.GetCaches();

            foreach (CreatedDependencyCache cacheUsage in caches)
            {
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
            string fullpath = GetLibraryFullPath(GetGuidFromId(assetId));
            
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
                Hash128 artifactHash = AssetDatabaseExperimental.GetArtifactHash(guid);

                if (!artifactHash.isValid)
                {
                    return null;
                }
                
                AssetDatabaseExperimental.GetArtifactPaths(artifactHash, out string[] paths);
            
                foreach (string artifactPath in paths)
                {
                    if(artifactPath.EndsWith(".info")) 
                        continue;
                
                    return Path.GetFullPath(artifactPath);
                }
            }
#else // For older version that dont have asset database V2 yet
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
            NodeDependencyLookupContext stateContext)
        {
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
                size += nodeHandler.GetOwnFileSize(id, type, stateContext);
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
                            size += GetNodeSize(true, true, childNode.Id, childNode.Type, traversedNodes, stateContext);
                        }
                    }
                }
            }

            return size;
        }

        private const string BuiltinExtraGuid = "0000000000000000f000000000000000";
        private const string BuiltinGuid = "0000000000000000e000000000000000";

        public static string GetGuidFromId(string id)
        {
            return id.Split('_')[0];
        }
        
        public static string GetFileIdFromId(string id)
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
            string fileId = GetFileIdFromId(id);
            string guid = GetGuidFromId(id);
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object[] assetsAtPath = LoadAllAssetsAtPath(path);
			
            foreach (Object asset in assetsAtPath)
            {
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
            string guid = GetGuidFromId(id);
            string path = AssetDatabase.GUIDToAssetPath(guid);

            return AssetDatabase.LoadAssetAtPath<Object>(path);
        }

        public static Object[] LoadAllAssetsAtPath(string path)
        {
            if (path.EndsWith(".unity"))
            {
                return new Object[] {AssetDatabase.LoadMainAssetAtPath(path)};
            }
            else
            {
                return AssetDatabase.LoadAllAssetsAtPath(path);
            }
        }

        public static string[] GetAllAssetIds(ProgressBase progress)
        {
            string[] pathes = AssetDatabase.FindAssets("");

            for (int i = 0; i < pathes.Length; ++i)
            {
                pathes[i] = AssetDatabase.GUIDToAssetPath(pathes[i]);
            }

            pathes = AssetDatabase.GetAllAssetPaths();

            List<string> pathList = new List<string>();
            List<string> idList = new List<string>();
            
            //pathList.Add(AssetDatabase.GUIDToAssetPath(BuiltinGuid));
            //pathList.Add(AssetDatabase.GUIDToAssetPath(BuiltinExtraGuid));

            foreach (string path in pathes)
            {
                pathList.Add(path);
            }

            for (var i = 0; i < pathList.Count; i++)
            {
                float progressAmount = (i / (float) pathList.Count) * 100;
                
                //progress.UpdateProgress("Loading assets", );
                
                string path = pathList[i];
                Object[] allAssets = LoadAllAssetsAtPath(path);
                
                foreach (Object asset in allAssets)
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileID);

                    /*if (fileID.ToString().EndsWith("3555"))
                    {
                        Debug.LogError("Test");
                    }*/

                    if (AssetDatabase.IsMainAsset(asset) || AssetDatabase.IsSubAsset(asset))
                    {
                        idList.Add($"{guid}_{fileID}");
                    }
                }
            }

            return idList.ToArray();
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
         * Return the dependency lookup for Objects using the SimpleObjectResolver
         */
        public static void BuildDefaultAssetLookup(NodeDependencyLookupContext stateContext, bool loadFromCache, string savePath,
            ProgressBase progress)
        {
            ResolverUsageDefinitionList usageDefinitionList = new ResolverUsageDefinitionList();
            usageDefinitionList.Add<AssetDependencyCache, ObjectDependencyResolver>();

            LoadDependencyLookupForCaches(stateContext, usageDefinitionList, progress, loadFromCache, true, false,
                savePath);
        }
    }
}