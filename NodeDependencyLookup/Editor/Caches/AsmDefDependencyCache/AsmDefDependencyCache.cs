using System;
using System.Collections;
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
		private class AsmDefJson
		{
			public string name = string.Empty;
			public string[] references = Array.Empty<string>();
		}

		private class AsmRefJson
		{
			public string reference;
		}

		private IDependencyMappingNode[] Nodes = new IDependencyMappingNode[0];

		private Dictionary<string, GenericDependencyMappingNode> _lookup =
			new Dictionary<string, GenericDependencyMappingNode>();

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

		public IEnumerator Update(CacheUpdateSettings cacheUpdateSettings, ResolverUsageDefinitionList resolverUsages,
			bool shouldUpdate)
		{
			_lookup.Clear();

			var nodes = new List<IDependencyMappingNode>();
			var nameToFileMapping = GenerateAsmDefFileMapping();

			AddAsmDefs(nodes, nameToFileMapping);
			AddAsmRefs(nodes, nameToFileMapping);

			Nodes = nodes.ToArray();

			yield return null;
		}

		private Dictionary<string, string> GenerateAsmDefFileMapping()
		{
			var nameToFileMapping = new Dictionary<string, string>();

			var guids = AssetDatabase.FindAssets("t:asmdef");

			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var asmdef = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
				nameToFileMapping.Add(JsonUtility.FromJson<AsmDefJson>(asmdef.text).name, path);
			}

			return nameToFileMapping;
		}

		private bool TryGetRefPathFromGUID(string reference, Dictionary<string, string> nameToFileMapping,
			out string refPath)
		{
			if (reference.StartsWith("GUID:"))
			{
				refPath = AssetDatabase.GUIDToAssetPath(reference.Split(':')[1]);
			}
			else if (!nameToFileMapping.TryGetValue(reference, out refPath))
			{
				return false;
			}

			if (string.IsNullOrEmpty(refPath))
			{
				return false;
			}

			return true;
		}

		private void AddAsmDefs(List<IDependencyMappingNode> nodes, Dictionary<string, string> nameToFileMapping)
		{
			var guids = AssetDatabase.FindAssets("t:asmdef");

			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);

				var asmdef = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
				var asmDefJson = JsonUtility.FromJson<AsmDefJson>(asmdef.text);

				var node = new GenericDependencyMappingNode(NodeDependencyLookupUtility.GetAssetIdForAsset(asmdef),
					AssetNodeType.Name);

				var g = 0;

				foreach (var reference in asmDefJson.references)
				{
					if (!TryGetRefPathFromGUID(reference, nameToFileMapping, out var refPath))
					{
						continue;
					}

					var refAsmDef = AssetDatabase.LoadAssetAtPath<TextAsset>(refPath);

					if (refAsmDef == null)
					{
						Debug.LogWarning($"No Assembly Definition loaded for {refPath}");
						continue;
					}

					var assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(refAsmDef);
					var componentName = "Ref " + g++;

					node.Dependencies.Add(new Dependency(assetId, AsmdefToAsmdefDependency.Name, AssetNodeType.Name,
						new[] {new PathSegment(componentName, PathSegmentType.Property)}));
				}

				nodes.Add(node);
				_lookup.Add(node.Id, node);
			}
		}

		private void AddAsmRefs(List<IDependencyMappingNode> nodes, Dictionary<string, string> nameToFileMapping)
		{
			var guids = AssetDatabase.FindAssets("t:asmref");

			foreach (var guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);

				var asmref = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
				var asmRefJson = JsonUtility.FromJson<AsmRefJson>(asmref.text);

				var node = new GenericDependencyMappingNode(NodeDependencyLookupUtility.GetAssetIdForAsset(asmref),
					AssetNodeType.Name);

				if (!TryGetRefPathFromGUID(asmRefJson.reference, nameToFileMapping, out var refPath))
				{
					continue;
				}

				var asmRef = AssetDatabase.LoadAssetAtPath<TextAsset>(refPath);

				if (asmRef == null)
				{
					Debug.LogWarning($"No Assembly Definition loaded for {refPath}");
					continue;
				}

				var assetId = NodeDependencyLookupUtility.GetAssetIdForAsset(asmRef);
				var componentName = "Ref";

				node.Dependencies.Add(new Dependency(assetId, AsmdefToAsmdefDependency.Name, AssetNodeType.Name,
					new[] {new PathSegment(componentName, PathSegmentType.Property)}));

				nodes.Add(node);
				_lookup.Add(node.Id, node);
			}
		}

		public void AddExistingNodes(List<IDependencyMappingNode> nodes)
		{
			foreach (var node in Nodes)
			{
				nodes.Add(node);
			}
		}

		public List<Dependency> GetDependenciesForId(string id)
		{
			if (NodeDependencyLookupUtility.IsResolverActive(_createdDependencyCache, AsmDefDependencyResolver.Id,
				    AsmdefToAsmdefDependency.Name))
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

			private static DependencyType asmdefDependencyType = new DependencyType("AsmDef->AsmDef",
				new Color(0.9f, 0.9f, 0.5f), false, true, ConnectionTypeDescription);

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