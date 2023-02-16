﻿using System;
using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public struct CacheUpdateInfo
	{
		public bool Load;
		public bool Update;
		public bool Save;
	}
	
	/**
	 * Interface for the Dependency Cache
	 * A dependencyCache stores already updated Assets, etc. so that a not changed asset for example doesnt need to be searched for dependencies again.
	 * This saves a lot of time when having thousands of assets and only like 10 changes which needs to be updated.
	 * A Cache for now can only resolve one nodetype.
	 * A Nodetype for example could be an Asset, AssetBundle, LocaKey, etc.
	 */
	public interface IDependencyCache
	{
		void ClearFile(string directory);
		void Initialize(CreatedDependencyCache createdDependencyCache);
		bool CanUpdate();
		bool Update(ResolverUsageDefinitionList resolverUsages, bool shouldUpdate);
		void AddExistingNodes(List<IDependencyMappingNode> nodes);
		List<Dependency> GetDependenciesForId(string id);
		void Load(string directory);
		void Save(string directory);
		void InitLookup();
		
		// Required for AssetRelationsViewer!
		Type GetResolverType();
	}
}