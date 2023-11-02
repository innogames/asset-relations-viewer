using System;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    public class CacheUpdateResourcesCleaner
    {
        private int lastCleanIndex = 0;

        public void Clean(CacheUpdateSettings settings, int index)
        {
            if (index <= lastCleanIndex || index % settings.UnloadUnusedAssetsInterval != 0)
            {
                return;
            }

            ForceClean(settings);

            lastCleanIndex = index;
        }

        public static void ForceClean(CacheUpdateSettings settings)
        {
            if (settings.ShouldUnloadUnusedAssets)
            {
                Resources.UnloadUnusedAssets();
                EditorUtility.UnloadUnusedAssetsImmediate(true);
            }

            GC.Collect(0, GCCollectionMode.Optimized);
        }
    }
}