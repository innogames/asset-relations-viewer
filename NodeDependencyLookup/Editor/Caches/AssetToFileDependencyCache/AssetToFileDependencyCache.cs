using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    // Cache to find get mapping of assets to the file the asset is included in
    public class AssetToFileDependencyCache : IDependencyCache
    {
        private const string Version = "1.10";
        private const string FileName = "AssetToFileDependencyCacheData_" + Version + ".cache";
        private const string ConnectionType = "File";

        private Dictionary<string, FileToAssetMappingNode> _fileNodesDict = new Dictionary<string, FileToAssetMappingNode>();
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

        public bool NeedsUpdate(ProgressBase progress)
        {
            string[] assetIds = NodeDependencyLookupUtility.GetAllAssetPathes(progress, true);
            long[] timeStampsForFiles = NodeDependencyLookupUtility.GetTimeStampsForFiles(assetIds);
            
            return GetNeedsUpdate(assetIds, timeStampsForFiles);
        }
        
        private bool GetNeedsUpdate(string[] pathes, long[] timestamps)
        {
            Dictionary<string, FileToAssetsMapping> list = RelationLookup.RelationLookupBuilder.ConvertToDictionary(_fileToAssetsMappings);

            for (int i = 0; i < pathes.Length; ++i)
            {
                string path = pathes[i];
                string guid = AssetDatabase.AssetPathToGUID(path);

                if (list.ContainsKey(guid))
                {
                    FileToAssetsMapping fileToAssetsMapping = list[guid];
                    long timeStamp = timestamps[i];

                    if (fileToAssetsMapping.Timestamp != timeStamp)
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }

            return false;
        }
        
        private HashSet<string> GetChangedAssetIds(string[] pathes, long[] timestamps, FileToAssetsMapping[] fileToAssetMappings)
        {
            HashSet<string> result = new HashSet<string>();
            Dictionary<string, FileToAssetsMapping> list = RelationLookup.RelationLookupBuilder.ConvertToDictionary(fileToAssetMappings);

            float lastDisplayedPercentage = 0;

            for (int i = 0; i < pathes.Length; ++i)
            {
                float progressPercentage = (float) i / pathes.Length;

                if (progressPercentage - lastDisplayedPercentage > 0.01f)
                {
                    EditorUtility.DisplayProgressBar("AssetToFileDependencyCache",$"Finding changed assets {result.Count}", (float)i / pathes.Length);
                    lastDisplayedPercentage = progressPercentage;
                }
                
                string path = pathes[i];
                string guid = AssetDatabase.AssetPathToGUID(path);

                if (list.ContainsKey(guid))
                {
                    FileToAssetsMapping fileToAssetsMapping = list[guid];
                    fileToAssetsMapping.SetExisting();
                    
                    long timeStamp = timestamps[i];

                    if (fileToAssetsMapping.Timestamp != timeStamp)
                    {
                        NodeDependencyLookupUtility.AddAssetsToList(result, path);
                    }
                }
                else
                {
                    NodeDependencyLookupUtility.AddAssetsToList(result, path);
                }
            }

            return result;
        }

        public bool CanUpdate()
        {
            return true;
        }

        public void Update(ProgressBase progress)
        {
            _fileToAssetsMappings = GetDependenciesForAssets(_fileToAssetsMappings, _createdDependencyCache, progress);
        }

        private FileToAssetsMapping[] GetDependenciesForAssets(FileToAssetsMapping[] fileToAssetsMappings,
            CreatedDependencyCache createdDependencyCache, ProgressBase progress)
        {
            string[] pathes = NodeDependencyLookupUtility.GetAllAssetPathes(progress, true);
            long[] timestamps = NodeDependencyLookupUtility.GetTimeStampsForFiles(pathes);
            
            List<AssetResolverData> data = new List<AssetResolverData>();

            foreach (CreatedResolver resolverUsage in createdDependencyCache.ResolverUsages)
            {
                if (!(resolverUsage.Resolver is IAssetToFileDependencyResolver))
                {
                    continue;
                }

                IAssetToFileDependencyResolver resolver = (IAssetToFileDependencyResolver) resolverUsage.Resolver;
                
                HashSet<string> changedAssets = GetChangedAssetIds(pathes, timestamps, fileToAssetsMappings);
                data.Add(new AssetResolverData{ChangedAssets = changedAssets, Resolver = resolver});
                
                resolver.Initialize(this, changedAssets, progress);
            }

            Dictionary<string, FileToAssetsMapping> nodeDict = RelationLookup.RelationLookupBuilder.ConvertToDictionary(fileToAssetsMappings);
            
            foreach (AssetResolverData resolverData in data)
            {
                GetDependenciesForAssetsInResolver(resolverData.ChangedAssets, resolverData.Resolver as IAssetToFileDependencyResolver, nodeDict, progress);
            }

            return nodeDict.Values.ToArray();
        }
        
        private void GetDependenciesForAssetsInResolver(HashSet<string> changedAssets, IAssetToFileDependencyResolver resolver, Dictionary<string, FileToAssetsMapping> resultList, ProgressBase progress)
        {
            foreach (string assetId in changedAssets)
            {
                string fileId = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);

                if (!resultList.ContainsKey(fileId))
                {
                    resultList.Add(fileId, new FileToAssetsMapping{FileId = fileId});
                }

                FileToAssetsMapping fileToAssetsMapping = resultList[fileId];
                FileToAssetMappingNode fileToAssetMappingNode = fileToAssetsMapping.GetFileNode(assetId);

                fileToAssetMappingNode.Dependencies.Clear();
                resolver.GetDependenciesForId(assetId, fileToAssetMappingNode.Dependencies);
                
                fileToAssetsMapping.Timestamp = NodeDependencyLookupUtility.GetTimeStampForFileId(fileId);
            }
        }

        public void AddExistingNodes(List<IResolvedNode> nodes)
        {
            foreach (FileToAssetsMapping fileToAssetsMapping in _fileToAssetsMappings)
            {
                foreach (FileToAssetMappingNode fileNode in fileToAssetsMapping.FileNodes)
                {
                    if (fileNode.Existing)
                    {
                        nodes.Add(fileNode);
                    }
                }
            }
        }

        public string GetHandledNodeType()
        {
            return "Asset";
        }

        public List<Dependency> GetDependenciesForId(string id)
        {
            if (NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, AssetToFileDependencyResolver.Id, ConnectionType))
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
                foreach (FileToAssetMappingNode fileNode in fileToAssetsMapping.FileNodes)
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

        public List<FileToAssetMappingNode> FileNodes = new List<FileToAssetMappingNode>();

        public FileToAssetMappingNode GetFileNode(string id)
        {
            foreach (FileToAssetMappingNode fileNode in FileNodes)
            {
                if (fileNode.Id == id)
                {
                    return fileNode;
                }
            }

            FileToAssetMappingNode newFileToAssetMappingNode = new FileToAssetMappingNode {AssetId = id};
            FileNodes.Add(newFileToAssetMappingNode);

            return newFileToAssetMappingNode;
        }

        public void SetExisting()
        {
            foreach (FileToAssetMappingNode fileNode in FileNodes)
            {
                fileNode.IsExisting = true;
            }
        }
    }
    
    public class FileToAssetMappingNode : IResolvedNode
    {
        public string AssetId;
        public List<Dependency> Dependencies = new List<Dependency>();
        public bool IsExisting = true;
        public string Id => AssetId;
        public string Type => "Asset";
        public bool Existing => IsExisting;
    }

    public interface IAssetToFileDependencyResolver : IDependencyResolver
    {
        void Initialize(AssetToFileDependencyCache cache, HashSet<string> changedAssets, ProgressBase progress);
        void GetDependenciesForId(string assetId, List<Dependency> dependencies);
    }

    public class AssetToFileDependencyResolver : IAssetToFileDependencyResolver
    {
        private static ConnectionType FileType = new ConnectionType(new Color(0.7f, 0.9f, 0.7f), false, true);

        public const string ResolvedType = "File";
        public const string Id = "AssetToFileDependencyResolver";
        
        private ResolverProgress Progress;

        public string[] GetConnectionTypes()
        {
            return new[] {"File"};
        }

        public string GetId()
        {
            return Id;
        }

        public ConnectionType GetDependencyTypeForId(string typeId)
        {
            return FileType;
        }

        public void Initialize(AssetToFileDependencyCache cache, HashSet<string> changedAssets, ProgressBase progress)
        {
            Progress = new ResolverProgress(progress, changedAssets.Count, 0.5f);
        }

        public void GetDependenciesForId(string assetId, List<Dependency> dependencies)
        {
            Progress.IncreaseProgress();
            string fileId = NodeDependencyLookupUtility.GetGuidFromAssetId(assetId);
            dependencies.Add(new Dependency(fileId, ResolvedType, "File", new []{new PathSegment("File", PathSegmentType.Property)}));
        }
    }
}