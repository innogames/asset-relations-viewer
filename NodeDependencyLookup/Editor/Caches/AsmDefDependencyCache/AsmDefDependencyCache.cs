using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup.AsmDefDependencyCache
{
    public class AsmdefToAsmdefDependency
    {
        public const string Name = "AsmDefToAsmDef";
    }
    
    public class AsmDefDependencyCache : IDependencyCache
    {
        private IDependencyMappingNode[] Nodes = new IDependencyMappingNode[0];
        private Dictionary<string, GenericDependencyMappingNode> _lookup = new Dictionary<string, GenericDependencyMappingNode>();
        private CreatedDependencyCache _createdDependencyCache;
        
        public void ClearFile(string directory)
        {
            // not needed
        }

        public void Initialize(CreatedDependencyCache createdDependencyCache)
        {
            _createdDependencyCache = createdDependencyCache;
        }

        public bool CanUpdate()
        {
            return true;
        }

        private class AsmDefJson
        {
            public string name = String.Empty;
            public string[] references = Array.Empty<string>();
        }
        
        private class AsmRefJson
        {
            public string reference;
        }

        public bool Update(ResolverUsageDefinitionList resolverUsages, bool shouldUpdate)
        {
            _lookup.Clear();

            List<IDependencyMappingNode> nodes = new List<IDependencyMappingNode>();
            Dictionary<string, string> nameToFileMapping = GenerateAsmDefFileMapping();

            AddAsmDefs(nodes, nameToFileMapping);
            AddAsmRefs(nodes, nameToFileMapping);
            
            Nodes = nodes.ToArray();

            return true;
        }

        private Dictionary<string, string> GenerateAsmDefFileMapping()
        {
            Dictionary<string, string> nameToFileMapping = new Dictionary<string, string>();

            string[] guids = AssetDatabase.FindAssets("t:asmdef");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextAsset asmdef = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                nameToFileMapping.Add(JsonUtility.FromJson<AsmDefJson>(asmdef.text).name, path);
            }
            
            return nameToFileMapping;
        }

        private bool TryGetRefPathFromGUID(string reference, Dictionary<string, string> nameToFileMapping, out string refPath)
        {
            if (reference.StartsWith("GUID:"))
            {
                refPath = AssetDatabase.GUIDToAssetPath(reference.Split(':')[1]);
            }
            else if(!nameToFileMapping.TryGetValue(reference, out refPath))
            {
                return false;
            }
                    
            if (string.IsNullOrEmpty(refPath))
            {
                return false;
            }
            
            return true;
        }

        private void AddAsmDefs( List<IDependencyMappingNode> nodes, Dictionary<string, string> nameToFileMapping)
        {
            string[] guids = AssetDatabase.FindAssets("t:asmdef");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                TextAsset asmdef = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                AsmDefJson asmDefJson = JsonUtility.FromJson<AsmDefJson>(asmdef.text);
                
                GenericDependencyMappingNode node = new GenericDependencyMappingNode(NodeDependencyLookupUtility.GetAssetIdForAsset(asmdef), AssetNodeType.Name);

                int g = 0;
                
                foreach (string reference in asmDefJson.references)
                {
                    if (!TryGetRefPathFromGUID(reference, nameToFileMapping, out string refPath))
                    {
                        continue;
                    }

                    TextAsset refAsmDef = AssetDatabase.LoadAssetAtPath<TextAsset>(refPath);
                    string assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(refAsmDef);
                    string componentName = "Ref " + g++;
                    
                    node.Dependencies.Add(new Dependency(assetId, AsmdefToAsmdefDependency.Name, AssetNodeType.Name, new []{new PathSegment(componentName, PathSegmentType.Property), }));
                }
                
                nodes.Add(node);
                _lookup.Add(node.Id, node);
            }
        }
        
        private void AddAsmRefs( List<IDependencyMappingNode> nodes, Dictionary<string, string> nameToFileMapping)
        {
            string[] guids = AssetDatabase.FindAssets("t:asmref");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                TextAsset asmref = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                AsmRefJson asmRefJson = JsonUtility.FromJson<AsmRefJson>(asmref.text);
                
                GenericDependencyMappingNode node = new GenericDependencyMappingNode(NodeDependencyLookupUtility.GetAssetIdForAsset(asmref), AssetNodeType.Name);

                if (!TryGetRefPathFromGUID(asmRefJson.reference, nameToFileMapping, out string refPath))
                {
                    continue;
                }

                TextAsset refAsmDef = AssetDatabase.LoadAssetAtPath<TextAsset>(refPath);
                string assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(refAsmDef);
                string componentName = "Ref";
                
                node.Dependencies.Add(new Dependency(assetId, AsmdefToAsmdefDependency.Name, AssetNodeType.Name, new []{new PathSegment(componentName, PathSegmentType.Property), }));

                nodes.Add(node);
                _lookup.Add(node.Id, node);
            }
        }

        public void AddExistingNodes(List<IDependencyMappingNode> nodes)
        {
            foreach (IDependencyMappingNode node in Nodes)
            {
                nodes.Add(node);
            }
        }

        public List<Dependency> GetDependenciesForId(string id)
        {
            if(NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, AsmDefDependencyResolver.Id, AsmdefToAsmdefDependency.Name))	
            {
                return _lookup[id].Dependencies;
            }
			
            return new List<Dependency>();
        }

        public void Load(string directory)
        {
        }

        public void Save(string directory)
        {
        }

        public void InitLookup()
        {
        }

        public Type GetResolverType()
        {
            return typeof(IAsmDefDependencyResolver);
        }

        public interface IAsmDefDependencyResolver : IDependencyResolver
        {
        }
        
        public class AsmDefDependencyResolver : IAsmDefDependencyResolver
        {
            private const string ConnectionTypeDescription = "Dependencies between AssemblyDefinitions";
            private static DependencyType asmdefDependencyType = new DependencyType("AsmDef->AsmDef", new Color(0.9f, 0.9f, 0.5f), false, true, ConnectionTypeDescription);
            public const string Id = "AsmdefDependencyResolver";
            
            public string[] GetDependencyTypes()
            {
                return new[] {AsmdefToAsmdefDependency.Name};
            }

            public string GetId()
            {
                return Id;
            }

            public DependencyType GetDependencyTypeForId(string typeId)
            {
                return asmdefDependencyType;
            }
        }
    }
}