using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// AssetTraverser which uses <see cref="SerializedObject"/> to find references in the components
	/// </summary>
	public class AssetSerializedPropertyTraverser : AssetTraverser
	{
		private readonly Stack<PathSegment> pathSegmentStack = new Stack<PathSegment>();

		private readonly HashSet<Type> excludedTypes = new HashSet<Type>
		{
			typeof(Transform),
			typeof(RectTransform),
			typeof(CanvasRenderer),
			typeof(TextAsset),
			typeof(AudioClip)
		};

		private PropertyInfo unsafeModeMethod;

		public void Initialize()
		{
			var type = typeof(SerializedProperty);
			unsafeModeMethod = type.GetProperty("unsafeMode",
				BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance);
		}

		public void Search(ResolverDependencySearchContext searchContext)
		{
			if (searchContext.Asset == null)
			{
				return;
			}

			pathSegmentStack.Clear();
			Traverse(searchContext, searchContext.Asset, pathSegmentStack);
		}

		protected override void TraverseObject(ResolverDependencySearchContext searchContext, Object obj,
			bool onlyOverriden, Stack<PathSegment> stack)
		{
			// this can happen if the linked asset doesnt exist anymore
			if (obj == null)
			{
				return;
			}

			var objType = obj.GetType();

			if (excludedTypes.Contains(objType))
			{
				return;
			}

			var serializedObject = new SerializedObject(obj);
			var property = serializedObject.GetIterator();

			unsafeModeMethod.SetValue(property, true);
			property.Next(true);

			SerializedPropertyType propertyType;

			do
			{
				propertyType = property.propertyType;

				if (propertyType != SerializedPropertyType.ObjectReference &&
				    propertyType != SerializedPropertyType.Generic &&
				    propertyType != SerializedPropertyType.ManagedReference)
				{
					continue;
				}

				if (!onlyOverriden || property.prefabOverride)
				{
					TraverseProperty(searchContext, property, propertyType, property.propertyPath, stack);
				}
			} while (property.Next(propertyType == SerializedPropertyType.Generic || propertyType == SerializedPropertyType.ManagedReference));

			serializedObject.Dispose();
		}

		protected override void TraversePrefab(ResolverDependencySearchContext searchContext, Object obj,
			Stack<PathSegment> stack)
		{
			foreach (var resolver in searchContext.Resolvers)
			{
				resolver.TraversePrefab(searchContext, obj, stack);
			}
		}

		protected override void TraversePrefabVariant(ResolverDependencySearchContext searchContext, Object obj,
			Stack<PathSegment> stack)
		{
			foreach (var resolver in searchContext.Resolvers)
			{
				resolver.TraversePrefabVariant(searchContext, obj, stack);
			}
		}

		private void TraverseProperty(ResolverDependencySearchContext searchContext, SerializedProperty property,
			SerializedPropertyType type, string propertyPath, Stack<PathSegment> stack)
		{
			foreach (var resolver in searchContext.Resolvers)
			{
				var result = resolver.GetDependency(ref searchContext.AssetId, ref property, ref propertyPath, type);

				if (result == null || searchContext.AssetId == result.Id)
				{
					continue;
				}

				stack.Push(new PathSegment(propertyPath, PathSegmentType.Property));
				searchContext.AddDependency(resolver,
					new Dependency(result.Id, result.DependencyType, result.NodeType, stack.ToArray()));
				stack.Pop();
			}
		}
	}
}