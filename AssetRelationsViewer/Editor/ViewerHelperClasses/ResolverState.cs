using System.Collections.Generic;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public class ResolverState
    {
        public readonly IDependencyResolver Resolver;
        public bool IsActive;
        public HashSet<string> ActiveConnectionTypes;

        public ResolverState(IDependencyResolver resolver)
        {
            Resolver = resolver;
            IsActive = false;
            string[] types = Resolver.GetDependencyTypes();
            ActiveConnectionTypes = new HashSet<string>();

            for (int i = 0; i < types.Length; ++i)
            {
                string key = GetTypeKey(types[i]);

                if (EditorPrefs.GetBool(key, false))
                {
                    ActiveConnectionTypes.Add(types[i]);
                    IsActive = true;
                }
            }
        }

        public void SaveState()
        {
            string[] types = Resolver.GetDependencyTypes();

            EditorPrefs.SetBool(Resolver.GetId() + "|" + "IsActive", IsActive);

            for (int i = 0; i < types.Length; ++i)
            {
                string key = GetTypeKey(types[i]);
                EditorPrefs.SetBool(key, ActiveConnectionTypes.Contains(types[i]));
            }
        }
        
        private string GetTypeKey(string type)
        {
            return EditorPrefUtilities.GetProjectSpecificKey(Resolver.GetId() + "|" + type);
        }
    }
}