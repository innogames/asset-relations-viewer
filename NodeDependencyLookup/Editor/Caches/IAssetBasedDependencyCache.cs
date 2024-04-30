using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public interface IAssetBasedDependencyCache : IDependencyCache
	{
		void PreAssetUpdate();
		void PostAssetUpdate();
		List<(string, long)> GetChangedAssetPaths();

		List<IDependencyMappingNode>
			UpdateAssetsForPath(string path, long timeStamp, List<AssetListEntry> assetEntries);
	}
}
