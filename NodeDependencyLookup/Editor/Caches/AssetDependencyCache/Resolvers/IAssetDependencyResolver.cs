using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public interface IDependencyResolver
	{
		string[] GetDependencyTypes();
		string GetId();
		DependencyType GetDependencyTypeForId(string typeId);
	}

	public class AssetDependencyResolverResult
	{
		public string Id;
		public string DependencyType;
		public string NodeType;
	}

	/// <summary>
	/// A ICustomAssetDependencyResolver is a class which enables to show relations between assets that are not found by unity AssetDatabase.GetDependencies() function
	/// A usecase for this would be the atlases in NGUI where you maybe want to see that there is a relation between the atlas and the textures which it is using as a raw input.
	/// Otherwise the textures maybe would appear as not being used by anything even though they are a source for an atlas.
	/// </summary>
	public interface IAssetDependencyResolver : IDependencyResolver
	{
		void SetValidGUIDs();
		void Initialize(AssetDependencyCache cache);
		bool IsGuidValid(string path);

		// What to to when a prefab got found, in case of searching for assets, it should be added as a dependency
		void TraversePrefab(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack);
		void TraversePrefabVariant(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack);

		// Returns a dependency result of the given serialized property is a UnityEngine.Object
		public AssetDependencyResolverResult GetDependency(ref string sourceAssetId, ref SerializedProperty property,
			ref string propertyPath, SerializedPropertyType type);
	}

	/// <summary>
	/// A connectionType stores the way a Node is a dependency of another Node
	/// For assets this is usually "Object" which means it is just connected though a member parameter.
	/// For example if a material has a direct connection to a texture.
	/// Other possible ConnectionTypes could be for example Addressable, AssetBundleUsage, etc.
	/// </summary>
	public class DependencyType
	{
		public DependencyType(string name, Color color, bool isIndirect, bool isHard, string description)
		{
			Colour = color;
			IsIndirect = isIndirect;
			IsHard = isHard;
			Description = description;
			Name = name;
		}

		// The color it is using (for the Asset Relation Viewer)
		public readonly Color Colour;

		// Indirect dependencies might be assets another asset is build on (NGUI texture atlas for example)
		public readonly bool IsIndirect;

		// A hard reference marks that the bundle should be loaded together
		public readonly bool IsHard;

		// Discription of the connection type that will be displayed in the AssetRelationsViewer
		public readonly string Description;

		public readonly string Name;

		public virtual bool IsHardConnection(Node source, Node target)
		{
			return IsHard;
		}
	}
}