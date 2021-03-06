﻿using System.Collections.Generic;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public interface IDependencyResolver
	{
		string[] GetConnectionTypes();
		string GetId();
		ConnectionType GetDependencyTypeForId(string typeId);
	}

	/// <summary>
	/// A ICustomAssetDependencyResolver is a class which enables to show relations between assets that are not found by unity AssetDatabase.GetDependencies() function
	/// A usecase for this would be the atlases in NGUI where you maybe want to see that there is a relation between the atlas and the textures which it is using as a raw input.
	/// Otherwise the textures maybe would appear as not being used by anything even though they are a source for an atlas.
	/// </summary>
	public interface IAssetDependencyResolver : IDependencyResolver
	{
		void SetValidGUIDs();
		void Initialize(AssetDependencyCache cache, HashSet<string> changedAssets, ProgressBase progress);
		void GetDependenciesForId(string fileId, List<Dependency> dependencies);
		bool IsGuidValid(string path);
	}

	/// <summary>
	/// A connectionType stores the way a Node is a dependency of another Node
	/// For assets this is usually "Object" which means it is just connected though a member parameter.
	/// For example if a material has a direct connection to a texture.
	/// Other possible ConnectionTypes could be for example Addressable, AssetBundleUsage, etc.
	/// </summary>
	public class ConnectionType
	{
		public ConnectionType(Color color, bool isIndirect, bool isHard)
		{
			Colour = color;
			IsIndirect = isIndirect;
			IsHard = isHard;
		}

		// The color it is using (for the Asset Relation Viewer)
		public readonly Color Colour;

		// Indirect dependencies might be assets another asset is build on (NGUI texture atlas for example)
		public readonly bool IsIndirect;

		// A hard reference marks that the bundle should be loaded together 
		public readonly bool IsHard;
	}
}