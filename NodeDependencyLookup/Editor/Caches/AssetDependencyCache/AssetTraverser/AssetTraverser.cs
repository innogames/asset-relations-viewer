using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Traverses assets and their child GameObjects and components to find all dependencies in them
	/// </summary>
	public abstract class AssetTraverser
	{
		// Traverses an object (Monobehaviour, ScriptableObject) to get the dependencies from it
		public abstract void TraverseObject(string id, Object obj, Stack<PathSegment> stack, bool onlyOverriden);
		
		// What to to when a prefab got found, in case of searching for assets, it should be added as a dependency
		public abstract void TraversePrefab(string id, Object obj, Stack<PathSegment> stack);
		
		public abstract void TraversePrefabVariant(string id, Object obj, Stack<PathSegment> stack);

		public void Traverse(string id, Object obj, Stack<PathSegment> stack)
		{
			Profiler.BeginSample($"Traverse: {obj.name}");

			if (obj is GameObject)
			{
				GameObject go = obj as GameObject;
				TraverseGameObject(id, go, stack, null, true);
			}
			else if (obj is SceneAsset)
			{
				TraverseScene(id, obj, stack);
			}
			else
			{
				TraverseObject(id, obj, stack, false);
			}

			Profiler.EndSample();
		}

		public void TraverseScene(string id, Object obj, Stack<PathSegment> stack)
		{
			var sceneAsset = obj as SceneAsset;
			string path = AssetDatabase.GetAssetPath(sceneAsset);

			Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

			TraverseObject(id, obj, new Stack<PathSegment>(), false);

			foreach (GameObject go in scene.GetRootGameObjects())
			{
				stack.Push(new PathSegment(go.name, PathSegmentType.GameObject));
				TraverseGameObject(id, go, stack, null, false);
				stack.Pop();
			}

#pragma warning disable 618
			SceneManager.UnloadScene(scene);
#pragma warning restore 618
		}

		public void TraverseGameObject(string id, Object obj, Stack<PathSegment> stack, Object currentPrefab, bool isRoot)
		{
			var go = obj as GameObject;
			bool isPrefabInstance = false;
			bool onlyOverriden = false;

#if UNITY_2018_3_OR_NEWER
			PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(obj);

			if (prefabAssetType == PrefabAssetType.Regular || prefabAssetType == PrefabAssetType.Variant)
			{
				if (PrefabUtility.GetCorrespondingObjectFromSource(go))
				{
					onlyOverriden = true;
					isPrefabInstance = true;
					var prefabObj = PrefabUtility.GetPrefabInstanceHandle(obj);

					if(prefabObj != currentPrefab)
					{
						if (prefabAssetType == PrefabAssetType.Regular)
						{
							TraversePrefab(id, obj, stack);
						}
						else if (prefabAssetType == PrefabAssetType.Variant)
						{
							TraversePrefabVariant(id, obj, stack);
						}
						
						currentPrefab = prefabObj;
					}
				}
			}
#endif
			
#if !UNITY_2018_3_OR_NEWER
			PrefabType prefabType = PrefabUtility.GetPrefabType(obj);

			if (prefabType == PrefabType.PrefabInstance)
			{
				onlyOverriden = true;
				var prefabObj = PrefabUtility.GetPrefabParent(obj);

				if(prefabObj != currentPrefab)
				{
					TraversePrefab(id, prefabAssetType, obj, stack);
					currentPrefab = prefabObj;
				}
			}
#endif

			Dictionary<string, int> componentToCount = new Dictionary<string, int>();

			List<AddedComponent> addedComponents = isPrefabInstance ? PrefabUtility.GetAddedComponents(go) : null;

			foreach (Component component in go.GetComponents<Component>())
			{
				if (component == null)
				{
					continue;
				}

				bool componentOverriden = onlyOverriden;
				
				if (isPrefabInstance)
				{
					bool isAddedComponent = addedComponents.Any(addedComponent => addedComponent.instanceComponent == component);
					componentOverriden &= !isAddedComponent;
				}

				string componentName = component.GetType().Name;

				if (!componentToCount.ContainsKey(componentName))
				{
					componentToCount.Add(componentName, 1);
				}

				int sameComponentCount = componentToCount[componentName]++;
				string segmentName = sameComponentCount > 1 ? $"{componentName}_{sameComponentCount}" : componentName;
				
				stack.Push(new PathSegment(segmentName, PathSegmentType.Component));
				TraverseObject(id, component, stack, componentOverriden);
				stack.Pop();
			}

			for (int i = 0; i < go.transform.childCount; ++i)
			{
				GameObject child = go.transform.GetChild(i).gameObject;

				stack.Push(new PathSegment(child.name, PathSegmentType.GameObject));
				TraverseGameObject(id, child, stack, currentPrefab, false);
				stack.Pop();
			}
		}
	}
}