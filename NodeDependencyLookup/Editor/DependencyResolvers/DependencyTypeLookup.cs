using System.Collections.Generic;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEngine;

namespace Assets.Package.Editor.DependencyResolvers
{
	/// <summary>
	/// Lookup to get the ConnectionType by its typeId
	/// A connectionType could be Object or Addressable for example
	/// </summary>
	public class DependencyTypeLookup
	{
		private static DependencyType _defaultType = new DependencyType("Default", new Color(0.9f, 0.9f, 0.9f, 1.0f), false, false, "Default");
		private Dictionary<string, DependencyType> _lookup = new Dictionary<string, DependencyType>();

		internal DependencyTypeLookup(List<CreatedDependencyCache> usages)
		{
			foreach (CreatedDependencyCache usage in usages)
			{
				foreach (CreatedResolver resolverUsage in usage.ResolverUsages)
				{
					foreach (string type in resolverUsage.Resolver.GetDependencyTypes())
					{
						if(!_lookup.ContainsKey(type))
						{
							_lookup.Add(type, resolverUsage.Resolver.GetDependencyTypeForId(type));
						}
					}
				}
			}
		}

		public DependencyType GetDependencyType(string typeId)
		{
			if (_lookup.TryGetValue(typeId, out DependencyType type))
			{
				return type;
			}

			Debug.LogError($"No DependencyType found with typeId {typeId}");
			return _defaultType;
		}
	}
}
