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

        public CacheState(IDependencyCache cache)
        {
            Cache = cache;
        }

        public void UpdateActivation()
        {
            IsActive = false;
            
            foreach (ResolverState state in ResolverStates)
            {
                IsActive |= state.IsActive;
            }
        }

        public void SaveState()
        {
            foreach (ResolverState state in ResolverStates)
            {
                state.SaveState();
            }
        }
    }
}