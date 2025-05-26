using System;
using System.Collections;
using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Values on how to load, update and save the cache
	/// </summary>
	public struct CacheUpdateInfo
	{
		public bool Load;
		public bool Update;
		public bool Save;
	}

	/// <summary>
	/// Interface for the Dependency Cache
	/// A dependencyCache stores already updated Assets, etc. so that a not changed asset for example doesnt need to be
	/// searched for dependencies again.
	/// This saves a lot of time when having thousands of assets and only like 10 changes which needs to be updated.
	/// A Cache for now can only resolve one NodeType.
	/// A NodeType for example could be an Asset, AssetBundle, LocaKey, etc.
	/// </summary>
	public interface IDependencyCache
	{
		void Initialize(CreatedDependencyCache createdDependencyCache);
		bool CanUpdate();

		IEnumerator Update(CacheUpdateSettings cacheUpdateSettings, ResolverUsageDefinitionList resolverUsages,
			bool shouldUpdate);

		void AddExistingNodes(List<IDependencyMappingNode> nodes);
		List<Dependency> GetDependenciesForId(string id);
		void Load(string directory);
		void Save(string directory);
		void InitLookup();

		// Required for AssetRelationsViewer!
		Type GetResolverType();

		void PreAssetUpdate(string[] allPaths);
		void PostAssetUpdate();
		List<string> GetChangedAssetPaths(string[] allPaths, long[] pathTimestamps);

		List<IDependencyMappingNode>
			UpdateAssetsForPath(string path, long timeStamp, List<AssetListEntry> assetEntries);
	}
}
