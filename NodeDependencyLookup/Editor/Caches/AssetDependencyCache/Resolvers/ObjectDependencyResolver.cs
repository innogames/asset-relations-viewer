using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{	
	/**
	 * Resolver for resolving Object references by using unitys internal AssetDatabase.GetDependencies() function
	 * This one doesnt provide hierarchy and property pathes but is the fastest and most reliable.
	 * It is recommended to use this as long as hierarchy and property pathes are not needed
	 */
	public class ObjectDependencyResolver : IAssetDependencyResolver
	{
		private static ConnectionType ObjectType = new ConnectionType(new Color(0.8f, 0.8f, 0.8f), false, true);

		public const string ResolvedType = "Object";
		public const string Id = "ObjectDependencyResolver";

		private ResolverProgress Progress;

		public void GetDependenciesForId(string fileId, List<Dependency> dependencies)
		{
			string guid = NodeDependencyLookupUtility.GetGuidFromId(fileId);
			string path = AssetDatabase.GUIDToAssetPath(guid);

			string[] resolvedDependencies = AssetDatabase.GetDependencies(path, false);

			Progress.IncreaseProgress();
			Progress.UpdateProgress(Id, AssetDatabase.GUIDToAssetPath(fileId));
			
			foreach (string dependency in resolvedDependencies)
			{
				Object[] dependencyAssets = NodeDependencyLookupUtility.LoadAllAssetsAtPath(dependency);
				
				foreach (Object asset in dependencyAssets)
				{
					AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string depGuid, out long depFileId);
					string assetId = $"{depGuid}_{depFileId}";

					dependencies.Add(new Dependency(assetId, ResolvedType, "Asset", new PathSegment[0]));
				}
			}
		}

		public bool IsGuidValid(string path)
		{
			// For the SimpleObjectResolver all pathes are valid
			return true;
		}

		public string GetId()
		{
			return Id;
		}

		public ConnectionType GetDependencyTypeForId(string typeId)
		{
			return ObjectType;
		}

		public string[] GetConnectionTypes()
		{
			return new[] { ResolvedType };
		}

		public void SetValidGUIDs()
		{
			// nothing to do here
		}

		public void Initialize(AssetDependencyCache cache, HashSet<string> changedAssets, ProgressBase progress)
		{
			Progress = new ResolverProgress(progress, changedAssets.Count, 0.5f);
		}
	}
}
