using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup.Addressables
{
	/// <summary>
	/// Base class for both AddressableGroup caches which contains the group hashing logic to avoid update
	/// if the group hash did not change
	/// </summary>
	public abstract class AddressableGroupBaseCache
	{
		private readonly Dictionary<string, string> _groupToHashLookup = new Dictionary<string, string>();

		protected readonly HashSet<string> _groupsToBeUpdated = new HashSet<string>();
		protected Dictionary<string, GenericDependencyMappingNode> _dependencyLookup =
			new Dictionary<string, GenericDependencyMappingNode>();

		protected void LoadGroupHashes(string directory, string fileName)
		{
			_groupToHashLookup.Clear();

			var path = Path.Combine(directory, fileName);
			var bytes = File.Exists(path) ? File.ReadAllBytes(path) : new byte[16 * 1024];
			var offset = 0;
			var length = CacheSerializerUtils.DecodeInt(ref bytes, ref offset);

			for (var i = 0; i < length; ++i)
			{
				_groupToHashLookup.Add(CacheSerializerUtils.DecodeString(ref bytes, ref offset),
					CacheSerializerUtils.DecodeString(ref bytes, ref offset));
			}
		}

		protected void SaveGroupHashes(string directory, string fileName)
		{
			var bytes = new byte[4096];
			var offset = 0;
			CacheSerializerUtils.EncodeInt(_groupToHashLookup.Count, ref bytes, ref offset);

			foreach (var pair in _groupToHashLookup)
			{
				CacheSerializerUtils.EncodeString(pair.Key, ref bytes, ref offset);
				CacheSerializerUtils.EncodeString(pair.Value, ref bytes, ref offset);
				bytes = CacheSerializerUtils.EnsureSize(bytes, offset);
			}

			CacheSerializerUtils.SaveBytes(bytes, directory, fileName);
		}

		protected void UpdateHashLookupsForGroup(string groupName, string hash)
		{
			var groupHash = new Hash128();
			groupHash.Append(hash);
			var hashString = groupHash.ToString();

			if (_groupToHashLookup.TryGetValue(groupName, out var savedHash))
			{
				if (savedHash != hashString)
				{
					_groupsToBeUpdated.Add(groupName);
					_dependencyLookup.Remove(groupName);
				}
			}
			else
			{
				_groupsToBeUpdated.Add(groupName);
				_groupToHashLookup.Add(groupName, hashString);
				_dependencyLookup.Remove(groupName);
			}
		}
	}
}
