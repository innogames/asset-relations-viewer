using System;
using UnityEditor;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Helper to unload all unused assets every n iterations
	/// </summary>
	public class CacheUpdateResourcesCleaner
	{
		private int lastCleanIndex;

		public void Clean(CacheUpdateSettings settings, int index)
		{
			if (index <= lastCleanIndex || index % settings.UnloadUnusedAssetsInterval != 0)
			{
				return;
			}

			ForceClean(settings);

			lastCleanIndex = index;
		}

		private static void ForceClean(CacheUpdateSettings settings)
		{
			if (settings.ShouldUnloadUnusedAssets)
			{
				EditorUtility.UnloadUnusedAssetsImmediate(true);
			}

			GC.Collect(int.MaxValue, GCCollectionMode.Optimized);
		}
	}
}
