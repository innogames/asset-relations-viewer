using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
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
		private class ReflectionStackItem
		{
			public string fieldName;
			public int arrayIndex;
			public FieldInfo fieldInfo;
			public object value;
			public Type type;
		}

		private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
		private readonly ReflectionStackItem[] ReflectionStack = new ReflectionStackItem[128];
		private Dictionary<Type, bool> isReflectableCache = new Dictionary<Type, bool>();

		public void Initialize()
		{
			for (int i = 0; i < 128; ++i)
			{
				ReflectionStack[i] = new ReflectionStackItem();
			}
		}

		public void Search(ResolverDependencySearchContext searchContext)
		{
			if (searchContext.Asset == null)
			{
				return;
			}

			Traverse(searchContext, searchContext.Asset, new Stack<PathSegment>());
		}

		protected override void TraverseObject(ResolverDependencySearchContext searchContext, Object obj, bool onlyOverriden, Stack<PathSegment> stack)
		{
			// this can happen if the linked asset doesnt exist anymore
			if (obj == null)
			{
				return;
			}

			SerializedObject serializedObject = new SerializedObject(obj);
			SerializedProperty property = serializedObject.GetIterator();

			Type objType = obj.GetType();

			if (!isReflectableCache.TryGetValue(objType, out bool isReflectable))
			{
				isReflectable = objType.GetFields(Flags).Length > 0;
				isReflectableCache[objType] = isReflectable;
			}

			ReflectionStackItem rootItem = ReflectionStack[0];
			rootItem.value = obj;
			rootItem.type = objType;

			Type type = typeof(SerializedProperty);
			type.GetProperty("unsafeMode", BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance).SetValue(property, true);
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
					if (!isReflectable)
					{
						if (propertyType == SerializedPropertyType.ObjectReference)
						{
							TraverseProperty(searchContext, property.objectReferenceValue, propertyType, property.propertyPath, stack);
						}

						continue;
					}

					string propertyPath = property.propertyPath;
					string modifiedPath = propertyPath;

					if (modifiedPath.IndexOf(".Array.data[") != -1)
					{
						modifiedPath = propertyPath.Replace(".Array.data[", "[");
					}

					int stackIndex = propertyType == SerializedPropertyType.Generic ? UpdateStack(modifiedPath, ReflectionStack) : -1;

					object generic = stackIndex != -1 ? ReflectionStack[stackIndex].value : (propertyType == SerializedPropertyType.ObjectReference ? property.objectReferenceValue : null);

					if (generic == null)
					{
						continue;
					}

					TraverseProperty(searchContext, generic, propertyType, propertyPath, stack);
				}
			} while (property.Next(propertyType == SerializedPropertyType.Generic));

			serializedObject.Dispose();
		}

		private int UpdateStack(string path, ReflectionStackItem[] stack)
		{
			int subIndex = 0;
			int tokenCount = 0;

			for (int i = 0; i < path.Length; ++i)
			{
				if (path[i] == '.')
				{
					subIndex = i + 1;
					tokenCount++;
				}
			}

			int stackPos = tokenCount + 1;

			ref ReflectionStackItem parent = ref stack[stackPos - 1];

			if (parent.type == null)
				return -1;

			ref ReflectionStackItem item = ref stack[stackPos];

			string elementName = path.Substring(subIndex);
			item.fieldName = elementName;
			item.arrayIndex = -1;

			int arrayStartIndex = elementName.LastIndexOf('[');

			if (arrayStartIndex != -1)
			{
				int arrayEndIndex = elementName.LastIndexOf(']');
				int length = arrayEndIndex - arrayStartIndex - 1;
				string index = elementName.Substring(arrayStartIndex + 1, length);
				item.arrayIndex = System.Convert.ToInt32(index);
				item.fieldName = elementName.Substring(0, arrayStartIndex);
			}

			item.fieldInfo = parent.type.GetField( item.fieldName , Flags);

			if (item.fieldInfo == null || parent.value == null)
				return -1;

			item.value = item.fieldInfo.GetValue(parent.value);

			if (item.value == null)
				return -1;

			Type itemType = item.value.GetType();

			item.type = itemType;

			if (item.value != null && item.arrayIndex != -1)
			{
				IList genericList = item.value as IList;

				if (genericList == null)
				{
					return -1;
				}

				item.type = genericList.GetType().IsGenericType ? item.type.GetGenericArguments()[0] : item.type.GetElementType();
				item.value = genericList[item.arrayIndex];
			}

			return tokenCount + 1;
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

		private void TraverseProperty(ResolverDependencySearchContext searchContext, object obj, SerializedPropertyType type, string propertyPath, Stack<PathSegment> stack)
		{
			foreach (IAssetDependencyResolver resolver in searchContext.Resolvers)
			{
				AssetDependencyResolverResult result = resolver.GetDependency(searchContext.AssetId, obj, propertyPath, type);

				if (result == null || searchContext.AssetId == result.Id)
				{
					continue;
				}

				stack.Push(new PathSegment(propertyPath, PathSegmentType.Property));
				searchContext.AddDependency(resolver, new Dependency(result.Id, result.DependencyType, result.NodeType, stack.ToArray()));
				stack.Pop();
			}
		}
	}
}