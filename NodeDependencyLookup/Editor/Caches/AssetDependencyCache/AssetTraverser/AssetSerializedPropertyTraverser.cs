using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public class AssetSerializedPropertyTraverser : AssetTraverser
	{
		private class ReflectionStackItem
		{
			public string fieldName = String.Empty;
			public int arrayIndex;
			public FieldInfo fieldInfo;
			public object value;
			public Type type;
		}
		
		private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
		private Dictionary<string, List<SerializedPropertyTraverserSubSystem>> assetIdToResolver = new Dictionary<string, List<SerializedPropertyTraverserSubSystem>>();
		private ResolverProgress progress;
		private readonly ReflectionStackItem[] ReflectionStack = new ReflectionStackItem[128];
		private ObjectPropertyList objectPropertyList = new ObjectPropertyList();

		public void Search()
		{
			for (int i = 0; i < 128; ++i)
			{
				ReflectionStack[i] = new ReflectionStackItem();
			}

			foreach (var pair in assetIdToResolver)
			{
				Object asset = NodeDependencyLookupUtility.GetAssetById(pair.Key);

				if (asset == null)
				{
					continue;
				}
				
				progress.IncreaseProgress();
				progress.UpdateProgress("Finding dependencies", asset.name);
				Traverse(pair.Key, asset, new Stack<PathSegment>());
			}
		}

		public void Clear()
		{
			assetIdToResolver.Clear();
		}

		public void Initialize(ProgressBase progress)
		{
			this.progress = new ResolverProgress(progress, assetIdToResolver.Count, 10);
		}

		public void AddAssetId(string key, SerializedPropertyTraverserSubSystem resolver)
		{
			if (!assetIdToResolver.ContainsKey(key))
			{
				assetIdToResolver.Add(key, new List<SerializedPropertyTraverserSubSystem>());
			}

			assetIdToResolver[key].Add(resolver);
		}

		private class ObjectPropertyList
		{
			public struct PropertyEntry
			{
				public string Id;
				public string PropertyPath;
				public SerializedPropertyType PropertyType;
				public Object ObjectValue;
			}

			public Stack<PathSegment> Stack;
			public List<PropertyEntry> PropertyEntries = new List<PropertyEntry>(16);
		}

		public override void TraverseObject(string id, Object obj, Stack<PathSegment> stack, bool onlyOverriden)
		{
			// this can happen if the linked asset doesnt exist anymore
			if (obj == null)
			{
				return;
			}
			
			SerializedObject serializedObject = new SerializedObject(obj);
			SerializedProperty property = serializedObject.GetIterator();

			Type objType = obj.GetType();
			
			ReflectionStackItem rootItem = ReflectionStack[0];
			rootItem.value = obj;
			rootItem.type = objType;

			Type type = typeof(SerializedProperty);
			type.GetProperty("unsafeMode", BindingFlags.NonPublic | BindingFlags.SetProperty | BindingFlags.Instance).SetValue(property, true);

			SerializedPropertyType propertyType;

			objectPropertyList.PropertyEntries.Clear();
			objectPropertyList.Stack = new Stack<PathSegment>(stack);
			
			do
			{
				propertyType = property.propertyType;

				if (propertyType != SerializedPropertyType.ObjectReference && propertyType != SerializedPropertyType.Generic)
				{
					continue;
				}

				if (!onlyOverriden || property.prefabOverride)
				{
					Object objectValue = propertyType == SerializedPropertyType.ObjectReference ? property.objectReferenceValue : null;
					objectPropertyList.PropertyEntries.Add(new ObjectPropertyList.PropertyEntry{Id = id, PropertyPath = property.propertyPath, PropertyType = propertyType, ObjectValue = objectValue});
				}
			} while (property.Next(propertyType == SerializedPropertyType.Generic));
			
			IterateProperties(objectPropertyList);
		}

		private void IterateProperties(ObjectPropertyList item)
		{
			foreach (ObjectPropertyList.PropertyEntry entry in item.PropertyEntries)
			{
				string modifiedPath = entry.PropertyPath.Replace(".Array.data[", "[");
				int stackIndex = UpdateStack(modifiedPath, ReflectionStack);

				object generic = stackIndex != -1 ? ReflectionStack[stackIndex].value : entry.ObjectValue;
				TraverseProperty(entry.Id, generic, entry.PropertyType, entry.PropertyPath, item.Stack);
			}
		}

		private int UpdateStack(string path, ReflectionStackItem[] stack)
		{
			if (string.IsNullOrEmpty(path))
				return -1;

			string[] tokens = path.Split( '.' );

			for (int i = 0; i < tokens.Length; ++i)
			{
				string elementName = tokens[i];
				int stackPos = i + 1;
				
				ReflectionStackItem parent = stack[stackPos - 1];
				ReflectionStackItem item = stack[stackPos];
				
				if (parent.type == null)
					return -1;
				
				item.fieldName = elementName;
				item.arrayIndex = -1;

				if (elementName.Contains("["))
				{
					item.arrayIndex = System.Convert.ToInt32(elementName.Substring(elementName.IndexOf("[")).Replace("[", "").Replace("]", ""));
					item.fieldName = elementName.Substring(0, elementName.IndexOf("["));
				}

				item.fieldInfo = parent.type.GetField( item.fieldName , Flags);

				if (item.fieldInfo == null || parent.value == null)
					return -1;

				item.value = item.fieldInfo.GetValue(parent.value);

				if (item.value == null)
					return -1;
				
				item.type = item.value.GetType();
				
				if (item.value != null && item.arrayIndex != -1)
				{
					IList genericList = item.value as IList;

					if (genericList == null || item.arrayIndex >= genericList.Count)
					{
						return -1;
					}

					if (genericList.GetType().IsGenericType)
					{
						item.type = item.type.GetGenericArguments()[0];
					}
					else
					{
						item.type = item.type.GetElementType();
					}
					
					item.value = genericList[item.arrayIndex];
				}
			}

			return tokens.Length;
		}

		public override void TraversePrefab(string id, Object obj, Stack<PathSegment> stack)
		{
			foreach (SerializedPropertyTraverserSubSystem subSystem in assetIdToResolver[id])
			{
				subSystem.TraversePrefab(id, obj, stack);
			}
		}

		public override void TraversePrefabVariant(string id, Object obj, Stack<PathSegment> stack)
		{
			if (!assetIdToResolver.ContainsKey(id))
			{
				Debug.LogErrorFormat("AssetSerializedPropertyTraverser: could not find guid {0} in resolver list", id);
			}
			
			foreach (SerializedPropertyTraverserSubSystem subSystem in assetIdToResolver[id])
			{
				subSystem.TraversePrefabVariant(id, obj, stack);
			}
		}

		private void TraverseProperty(string assetId, object obj, SerializedPropertyType type, string propertyPath, Stack<PathSegment> stack)
		{
			foreach (SerializedPropertyTraverserSubSystem subSystem in assetIdToResolver[assetId])
			{
				SerializedPropertyTraverserSubSystem.Result result = subSystem.GetDependency(obj, propertyPath, type, stack);
				
				if (result == null)
					continue;
				
				if (assetId == result.Id)
				{
					continue;
				}
			
				stack.Push(new PathSegment(propertyPath, PathSegmentType.Property));
				subSystem.AddDependency(assetId, new Dependency(result.Id, result.ConnectionType, result.NodeType, stack.ToArray()));
				stack.Pop();
			}
		}
	}
}