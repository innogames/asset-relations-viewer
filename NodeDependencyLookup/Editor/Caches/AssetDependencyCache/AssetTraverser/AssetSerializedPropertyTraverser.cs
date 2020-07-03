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
			public string elementName = String.Empty;
			public string fieldName = String.Empty;
			public int arrayIndex;
			public FieldInfo fieldInfo;
			public object value;
			public Type type;
		}
		
		private Dictionary<string, List<SerializedPropertyTraverserSubSystem>> m_guidToResolver = new Dictionary<string, List<SerializedPropertyTraverserSubSystem>>();
		private ResolverProgress Progress;
		private BindingFlags FLAGS = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
		private ReflectionStackItem[] ReflectionStack = new ReflectionStackItem[128];
		
		public void Search()
		{
			for (int i = 0; i < 128; ++i)
			{
				ReflectionStack[i] = new ReflectionStackItem();
			}
			
			foreach (var pair in m_guidToResolver)
			{
				string assetPath = AssetDatabase.GUIDToAssetPath(pair.Key);
				Progress.IncreaseProgress();
				Progress.UpdateProgress("SerializedPropertySearcher", assetPath);
				Traverse(pair.Key, AssetDatabase.LoadAssetAtPath<Object>(assetPath), new Stack<PathSegment>());
			}
		}

		public void Clear()
		{
			m_guidToResolver.Clear();
		}

		public void Initialize(ProgressBase progress)
		{
			Progress = new ResolverProgress(progress, m_guidToResolver.Count, 10);
		}

		public void AddGuid(string key, SerializedPropertyTraverserSubSystem resolver)
		{
			if (!m_guidToResolver.ContainsKey(key))
			{
				m_guidToResolver.Add(key, new List<SerializedPropertyTraverserSubSystem>());
			}

			m_guidToResolver[key].Add(resolver);
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

			do
			{
				if (property.type == "char")
				{
					continue;
				}
				
				if (!onlyOverriden || property.prefabOverride)
				{
					string propertyPath = property.propertyPath;

					string modifiedPath = propertyPath.Replace(".Array.data[", "[");

					int stackIndex = UpdateStack(modifiedPath, ReflectionStack);

					if (stackIndex != -1)
					{
						ReflectionStackItem stackItem = ReflectionStack[stackIndex];
						TraverseProperty(id, objType, stackItem.value, property, propertyPath, stack);
					}
					else
					{
						TraverseProperty(id, objType, null, property, propertyPath, stack);
					}
				}

			} while (property.Next(true));
		}

		private int UpdateStack(string path, ReflectionStackItem[] stack)
		{
			if (string.IsNullOrEmpty(path))
				return -1;

			bool changed = false;

			string[] tokens = path.Split( '.' );
			
			for (int i = 0; i < tokens.Length; ++i)
			{
				string elementName = tokens[i];
				int stackPos = i + 1;
				
				ReflectionStackItem parent = stack[stackPos - 1];
				ReflectionStackItem item = stack[stackPos];

				if (item != null && item.elementName == elementName && !changed)
					continue;

				changed = true;
				
				item.elementName = elementName;
				item.fieldName = elementName;
				item.arrayIndex = -1;

				if (elementName.Contains("["))
				{
					item.arrayIndex = System.Convert.ToInt32(elementName.Substring(elementName.IndexOf("[")).Replace("[", "").Replace("]", ""));
					item.fieldName = elementName.Substring(0, elementName.IndexOf("["));
				}

				if (parent.type == null)
					return -1;

				item.fieldInfo = parent.type.GetField( item.fieldName , FLAGS);

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
			if (!m_guidToResolver.ContainsKey(id))
			{
				Debug.LogErrorFormat("AssetSerializedPropertyTraverser: could not find guid {0} in resolver list", id);
			}
			
			foreach (SerializedPropertyTraverserSubSystem subSystem in m_guidToResolver[id])
			{
				subSystem.TraversePrefab(id, obj, stack);
			}
		}

		public override void TraversePrefabVariant(string id, Object obj, Stack<PathSegment> stack)
		{
			if (!m_guidToResolver.ContainsKey(id))
			{
				Debug.LogErrorFormat("AssetSerializedPropertyTraverser: could not find guid {0} in resolver list", id);
			}
			
			foreach (SerializedPropertyTraverserSubSystem subSystem in m_guidToResolver[id])
			{
				subSystem.TraversePrefabVariant(id, obj, stack);
			}
		}

		public void TraverseProperty(string id, Type objType, object obj, SerializedProperty property, string propertyPath, Stack<PathSegment> stack)
		{
			SerializedPropertyType type = property.propertyType;

			stack.Push(new PathSegment(propertyPath, PathSegmentType.Property));
			
			if (!m_guidToResolver.ContainsKey(id))
			{
				Debug.LogErrorFormat("AssetSerializedPropertyTraverser: could not find guid {0} in resolver list", id);
			}
			
			foreach (SerializedPropertyTraverserSubSystem subSystem in m_guidToResolver[id])
			{
				SerializedPropertyTraverserSubSystem.Result result = subSystem.GetDependency(objType, obj, property, propertyPath, type, stack);

				if (result == null || id == result.Id)
					continue;
			
				subSystem.AddDependency(id, new Dependency(result.Id, result.ConnectionType, result.NodeType, stack.ToArray()));
			}

			stack.Pop();
		}
	}
}