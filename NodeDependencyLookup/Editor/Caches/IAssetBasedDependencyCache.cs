using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public interface IAssetBasedDependencyCache : IDependencyCache
	{
		void PreUpdate();
		void PostUpdate();
		List<(string, long)> GetChangedAssetPaths();
		List<IDependencyMappingNode> UpdateAssets(string path, long timeStamp, List<AssetListEntry> assetEntries);
	}
}
