using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class ResolverDependencySearchContext
	{
		public Object Asset;
		public string AssetId = string.Empty;
		public List<IAssetDependencyResolver> Resolvers;
		public Dictionary<IAssetDependencyResolver, List<Dependency>> ResolverDependencies = new Dictionary<IAssetDependencyResolver, List<Dependency>>();

		public void SetResolvers(List<IAssetDependencyResolver> resolvers)
		{
			Resolvers = resolvers;
			ResolverDependencies.Clear();
			foreach (IAssetDependencyResolver resolver in resolvers)
			{
				ResolverDependencies.Add(resolver, new List<Dependency>());
			}
		}

		public void AddDependency(IAssetDependencyResolver resolver, Dependency dependency)
		{
			if (dependency.Id == AssetId)
			{
				// Dont add self dependency
				return;
			}

			ResolverDependencies[resolver].Add(dependency);
		}
	}

	public class AssetSerializedPropertyTraverser : AssetTraverser
	{
		private HashSet<Type> excludedTypes = new HashSet<Type>
		{
			typeof(Transform),
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(TextAsset),
			typeof(AudioClip),
		};

		private PropertyInfo unsafeModeMethod = null;

		public void Initialize()
		{
			Type type = typeof(SerializedProperty);
			unsafeModeMethod = type.GetProperty("unsafeMode", BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance);
		}

		private Stack<PathSegment> tmpStack = new Stack<PathSegment>();

		public void Search(ResolverDependencySearchContext searchContext)
		{
			if (searchContext.Asset == null)
			{
				return;
			}

			tmpStack.Clear();
			Traverse(searchContext, searchContext.Asset, tmpStack);
		}

		protected override void TraverseObject(ResolverDependencySearchContext searchContext, Object obj, bool onlyOverriden, Stack<PathSegment> stack)
		{
			// this can happen if the linked asset doesnt exist anymore
			if (obj == null)
			{
				return;
			}

			Type objType = obj.GetType();

			if(excludedTypes.Contains(objType))
			{
				return;
			}

			SerializedObject serializedObject = new SerializedObject(obj);
			SerializedProperty property = serializedObject.GetIterator();

			unsafeModeMethod.SetValue(property, true);
			property.Next(true);

			SerializedPropertyType propertyType;

			do
			{
				propertyType = property.propertyType;

				if (propertyType != SerializedPropertyType.ObjectReference && propertyType != SerializedPropertyType.Generic)
				{
					continue;
				}

				if (!onlyOverriden || property.prefabOverride)
				{
					TraverseProperty(searchContext, property, propertyType, property.propertyPath, stack);
				}
			} while (property.Next(propertyType == SerializedPropertyType.Generic));

			serializedObject.Dispose();
		}

		private void StringToStackallocSpan(ref string value, ref Span<char> span)
		{
			for (var i = 0; i < value.Length; i++)
			{
				span[i] = value[i];
			}
		}

		protected override void TraversePrefab(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack)
		{
			foreach (IAssetDependencyResolver resolver in searchContext.Resolvers)
			{
				resolver.TraversePrefab(searchContext, obj, stack);
			}
		}

		protected override void TraversePrefabVariant(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack)
		{
			foreach (IAssetDependencyResolver resolver in searchContext.Resolvers)
			{
				resolver.TraversePrefabVariant(searchContext, obj, stack);
			}
		}

		private bool TraverseProperty(ResolverDependencySearchContext searchContext, SerializedProperty property, SerializedPropertyType type, string propertyPath, Stack<PathSegment> stack)
		{
			bool dependenciesAdded = false;

			foreach (IAssetDependencyResolver resolver in searchContext.Resolvers)
			{
				AssetDependencyResolverResult result = resolver.GetDependency(ref searchContext.AssetId, ref property, ref propertyPath, type);

				if (result == null || searchContext.AssetId == result.Id)
				{
					continue;
				}

				stack.Push(new PathSegment(propertyPath, PathSegmentType.Property));
				searchContext.AddDependency(resolver, new Dependency(result.Id, result.DependencyType, result.NodeType, stack.ToArray()));
				stack.Pop();

				dependenciesAdded = true;
			}

			return dependenciesAdded;
		}
	}
}