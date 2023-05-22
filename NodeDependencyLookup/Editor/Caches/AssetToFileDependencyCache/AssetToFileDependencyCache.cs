using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    public class AssetToFileDependency
    {
        public const string Name = "AssetToFile";
    }

    // Cache to find get mapping of assets to the file the asset is included in
    public class AssetToFileDependencyCache : IDependencyCache
    {
        private const string Version = "1.5.1";
        private const string FileName = "AssetToFileDependencyCacheData_" + Version + ".cache";

        private Dictionary<string, GenericDependencyMappingNode> _fileNodesDict = new Dictionary<string, GenericDependencyMappingNode>();
        private FileToAssetsMapping[] _fileToAssetsMappings = new FileToAssetsMapping[0];

        private CreatedDependencyCache _createdDependencyCache;

        private bool _isLoaded;

        public void ClearFile(string directory)
        {
            string path = Path.Combine(directory, FileName);

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public void Initialize(CreatedDependencyCache createdDependencyCache)
        {
            _createdDependencyCache = createdDependencyCache;
        }

        private HashSet<string> FindDependenciesInChangedAssets(string[] pathes, IAssetToFileDependencyResolver resolver, long[] timestamps, ref FileToAssetsMapping[] fileToAssetMappings)
        {
            HashSet<string> changedAssetIds = new HashSet<string>();
            Dictionary<string, FileToAssetsMapping> fileToAssetMappingDictionary = RelationLookup.RelationLookupBuilder.ConvertToDictionary(fileToAssetMappings);

            float lastDisplayedPercentage = 0;

            for (int i = 0, j = 0; i < pathes.Length; ++i)
            {
                float progressPercentage = (float) i / pathes.Length;

                if (progressPercentage - lastDisplayedPercentage > 0.01f)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("AssetToFileDependencyCache", $"Finding changed assets {changedAssetIds.Count}", (float)i / pathes.Length))
                    {
                        throw new DependencyUpdateAbortedException();
                    }

                    lastDisplayedPercentage = progressPercentage;
                }

                string path = pathes[i];
                string guid = AssetDatabase.AssetPathToGUID(path);
                bool changed = false;

                if (fileToAssetMappingDictionary.ContainsKey(guid))
                {
                    FileToAssetsMapping fileToAssetsMapping = fileToAssetMappingDictionary[guid];
                    long timeStamp = timestamps[i];

                    if (fileToAssetsMapping.Timestamp != timeStamp)
                    {
                        changed = true;
                    }
                }
                else
                {
                    changed = true;
                }

                if (changed)
                {
                    j++;
                    FindDependenciesForAssets(changedAssetIds, resolver, path, fileToAssetMappingDictionary);
                }

                /*if (j % 3000 == 0)
                {
                    EditorUtility.UnloadUnusedAssetsImmediate();
                }*/
            }

            fileToAssetMappings = fileToAssetMappingDictionary.Values.ToArray();

            return changedAssetIds;
        }

        private void FindDependenciesForAssets(HashSet<string> changedAssetIds, IAssetToFileDependencyResolver resolver, string path, Dictionary<string, FileToAssetsMapping> fileToAssetMappingDictionary)
        {
            HashSet<string> assetIds = new HashSet<string>();
            NodeDependencyLookupUtility.AddAssetsToList(assetIds, path);

            foreach (string assetId in assetIds)
            {
                GetDependenciesForAssetInResolver(assetId, resolver, fileToAssetMappingDictionary);
                changedAssetIds.Add(assetId);
            }
        }

        public bool CanUpdate()
        {
            return true;
        }

        public bool Update(ResolverUsageDefinitionList resolverUsages, bool shouldUpdate)
        {
            if (!shouldUpdate)
            {
                return false;
            }

            return  GetDependenciesForAssets(ref _fileToAssetsMappings, _createdDependencyCache);
        }

        private bool GetDependenciesForAssets(ref FileToAssetsMapping[] fileToAssetsMappings,
            CreatedDependencyCache createdDependencyCache)
        {
            string[] pathes = NodeDependencyLookupUtility.GetAllAssetPathes(true);
            long[] timestamps = NodeDependencyLookupUtility.GetTimeStampsForFiles(pathes);
            NodeDependencyLookupUtility.RemoveNonExistingFilesFromIdentifyableList(pathes, ref fileToAssetsMappings);

            bool hasChanges = false;

            foreach (CreatedResolver resolverUsage in createdDependencyCache.ResolverUsages)
            {
                if (!(resolverUsage.Resolver is IAssetToFileDependencyResolver))
                {
                    continue;
                }

                IAssetToFileDependencyResolver resolver = (IAssetToFileDependencyResolver) resolverUsage.Resolver;
                resolver.Initialize(this);

                HashSet<string> changedAssetIds = FindDependenciesInChangedAssets(pathes, resolver, timestamps, ref fileToAssetsMappings);
                hasChanges |= changedAssetIds.Count > 0;
            }

            return hasChanges;
        }

        private void GetDependenciesForAssetInResolver(string assetId, IAssetToFileDependencyResolver resolver, Dictionary<string, FileToAssetsMapping> resultList)
        {
            string fileId = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);

            if (!resultList.ContainsKey(fileId))
            {
                resultList.Add(fileId, new FileToAssetsMapping{FileId = fileId});
            }

            FileToAssetsMapping fileToAssetsMapping = resultList[fileId];
            GenericDependencyMappingNode genericDependencyMappingNode = fileToAssetsMapping.GetFileNode(assetId);

            genericDependencyMappingNode.Dependencies.Clear();
            resolver.GetDependenciesForId(assetId, genericDependencyMappingNode.Dependencies);

            fileToAssetsMapping.Timestamp = NodeDependencyLookupUtility.GetTimeStampForFileId(fileId);
        }

        public void AddExistingNodes(List<IDependencyMappingNode> nodes)
        {
            foreach (FileToAssetsMapping fileToAssetsMapping in _fileToAssetsMappings)
            {
                foreach (GenericDependencyMappingNode fileNode in fileToAssetsMapping.FileNodes)
                {
                    nodes.Add(fileNode);
                }
            }
        }

        public List<Dependency> GetDependenciesForId(string id)
        {
            if (NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, AssetToFileDependencyResolver.Id, AssetToFileDependency.Name))
            {
                return _fileNodesDict[id].Dependencies;
            }

            return new List<Dependency>();
        }

        public void Load(string directory)
        {
            string path = Path.Combine(directory, FileName);

            if (_isLoaded)
                return;

            if (File.Exists(path))
            {
                byte[] bytes = File.ReadAllBytes(path);
                _fileToAssetsMappings = AssetToFileDependencyCacheSerializer.Deserialize(bytes);
            }
            else
            {
                _fileToAssetsMappings = new FileToAssetsMapping[0];
            }

            _isLoaded = true;
        }

        public void Save(string directory)
        {
            string path = Path.Combine(directory, FileName);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, AssetToFileDependencyCacheSerializer.Serialize(_fileToAssetsMappings));
        }

        public void InitLookup()
        {
            _fileNodesDict.Clear();

            foreach (FileToAssetsMapping fileToAssetsMapping in _fileToAssetsMappings)
            {
                foreach (GenericDependencyMappingNode fileNode in fileToAssetsMapping.FileNodes)
                {
                    _fileNodesDict.Add(fileNode.Id, fileNode);
                }
            }
        }

        public Type GetResolverType()
        {
            return typeof(IAssetToFileDependencyResolver);
        }
    }

    public class FileToAssetsMapping : IIdentifyable
    {
        public string FileId;
        public long Timestamp;

        public string Id => FileId;

        public List<GenericDependencyMappingNode> FileNodes = new List<GenericDependencyMappingNode>();

        public GenericDependencyMappingNode GetFileNode(string id)
        {
            foreach (GenericDependencyMappingNode fileNode in FileNodes)
            {
                if (fileNode.Id == id)
                {
                    return fileNode;
                }
            }

            GenericDependencyMappingNode newGenericDependencyMappingNode = new GenericDependencyMappingNode(id, AssetNodeType.Name);
            FileNodes.Add(newGenericDependencyMappingNode);

            return newGenericDependencyMappingNode;
        }
    }

    public interface IAssetToFileDependencyResolver : IDependencyResolver
    {
        void Initialize(AssetToFileDependencyCache cache);
        void GetDependenciesForId(string assetId, List<Dependency> dependencies);
    }

    public class AssetToFileDependencyResolver : IAssetToFileDependencyResolver
    {
        private const string ConnectionTypeDescription = "Dependencies between assets to the file they are contained in";
        private static DependencyType fileDependencyType = new DependencyType("Asset->File", new Color(0.7f, 0.9f, 0.7f), false, true, ConnectionTypeDescription);
        public const string Id = "AssetToFileDependencyResolver";

        public string[] GetDependencyTypes()
        {
            return new[] {AssetToFileDependency.Name};
        }

        public string GetId()
        {
            return Id;
        }

        public DependencyType GetDependencyTypeForId(string typeId)
        {
            return fileDependencyType;
        }

        public void Initialize(AssetToFileDependencyCache cache)
        {
        }

        public void GetDependenciesForId(string assetId, List<Dependency> dependencies)
        {
            string fileId = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);
            dependencies.Add(new Dependency(fileId, AssetToFileDependency.Name, FileNodeType.Name, new []{new PathSegment(FileNodeType.Name, PathSegmentType.Property)}));
        }
    }
}