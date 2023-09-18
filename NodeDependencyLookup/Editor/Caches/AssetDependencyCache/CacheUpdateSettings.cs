namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Settings for the cache generation and upgrade
	/// </summary>
	public class CacheUpdateSettings
	{
		/// <summary>
		/// For finding dependencies of assets the assets need to be loaded.
		/// Usually these loaded assets never get unloaded since asset might be required multiple times.
		/// If the project however has huge amounts of bigger assets it might be useful the unload unused assets from time to time.
		/// This will limit the overall memory unity will allocate.
		/// </summary>
		public bool ShouldUnloadUnusedAssets = false;

		/// <summary>
		/// The interval (amount of assets) in which the unused resources will be unloaded
		/// </summary>
		public int UnloadUnusedAssetsInterval = 10000;
	}
}
