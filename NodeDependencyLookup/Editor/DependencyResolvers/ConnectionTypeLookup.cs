using System.Collections.Generic;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEngine;

namespace Assets.Package.Editor.DependencyResolvers
{
	/// <summary>
	/// Lookup to get the ConnectionType by its typeId
	/// A connectionType could be Object or Addressable for example
	/// </summary>
	public class ConnectionTypeLookup
	{
		private const string ConnectionTypeDescription = "Default";
		private static ConnectionType _defaultType = new ConnectionType(new Color(0.9f, 0.9f, 0.9f, 1.0f), false, false, ConnectionTypeDescription);
		private Dictionary<string, ConnectionType> _lookup = new Dictionary<string, ConnectionType>();

		internal ConnectionTypeLookup(List<CreatedDependencyCache> usages)
		{
			foreach (CreatedDependencyCache usage in usages)
			{
				foreach (CreatedResolver resolverUsage in usage.ResolverUsages)
				{
					foreach (string type in resolverUsage.Resolver.GetConnectionTypes())
					{
						if(!_lookup.ContainsKey(type))
						{
							_lookup.Add(type, resolverUsage.Resolver.GetDependencyTypeForId(type));
						}
					}
				}
			}
		}

		public ConnectionType GetDependencyType(string typeId)
		{
			if (_lookup.ContainsKey(typeId))
			{
				return _lookup[typeId];
			}

			return _defaultType;
		}
	}
}
