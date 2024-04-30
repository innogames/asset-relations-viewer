using System;
using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public interface IAssetBasedDependencyCache : IDependencyCache
	{
		void PreAssetUpdate();
		void PostAssetUpdate();
		List<string> GetChangedAssetPaths(string[] allPaths, long[] pathTimestamps);

		List<IDependencyMappingNode>
			UpdateAssetsForPath(string path, long timeStamp, List<AssetListEntry> assetEntries);
	}
}
