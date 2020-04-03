using System;
using System.Collections.Generic;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public class CacheState
    {
        public IDependencyCache Cache = null;
        public bool IsActive;
        public List<ResolverState> ResolverStates = new List<ResolverState>();

        private static string GetPrefKey(Type type)
        {
            return "Cache_" + type.Name;
        }
			
        private string GetPrefKey()
        {
            return GetPrefKey(Cache.GetType());
        }

        public CacheState(IDependencyCache cache)
        {
            Cache = cache;
            IsActive = EditorPrefs.GetBool(GetPrefKey());
        }

        public void SaveState()
        {
            EditorPrefs.SetBool(GetPrefKey(), IsActive);
				
            foreach (ResolverState state in ResolverStates)
            {
                state.SaveState();
            }
        }
    }
}