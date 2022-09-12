using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
		protected abstract void TraverseObject(string id, Object obj, Stack<PathSegment> stack, bool onlyOverriden);
		
		// What to to when a prefab got found, in case of searching for assets, it should be added as a dependency
		protected abstract void TraversePrefab(string id, Object obj, Stack<PathSegment> stack);

		protected abstract void TraversePrefabVariant(string id, Object obj, Stack<PathSegment> stack);

		protected void Traverse(string id, Object obj, Stack<PathSegment> stack)
		{
			// TODO avoid adding them at another place
			if (obj is Mesh)
			{
				return;
			}

			if (obj is GameObject gameObject)
			{
				TraverseGameObject(id, gameObject, stack, null);
			}
			else if (obj is SceneAsset sceneAsset)
			{
				TraverseScene(id, sceneAsset, stack);
			}
			else
			{
				TraverseObject(id, obj, stack, false);
			}
		}

		private void TraverseScene(string id, SceneAsset sceneAsset, Stack<PathSegment> stack)
		{
			string path = AssetDatabase.GetAssetPath(sceneAsset);

			if (path.StartsWith("Packages"))
			{
				return;
			}

			Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

			TraverseObject(id, sceneAsset, new Stack<PathSegment>(), false);

			foreach (GameObject go in scene.GetRootGameObjects())
			{
				stack.Push(new PathSegment(go.name, PathSegmentType.GameObject));
				TraverseGameObject(id, go, stack, null);
				stack.Pop();
			}

#pragma warning disable 618
			SceneManager.UnloadScene(scene);
#pragma warning restore 618
		}

		private void TraverseGameObject(string id, GameObject go, Stack<PathSegment> stack, Object currentPrefab)
		{
			bool isPrefabInstance = false;
			bool onlyOverriden = false;

#if UNITY_2018_3_OR_NEWER
			PrefabAssetType prefabAssetType = PrefabUtility.GetPrefabAssetType(go);

			if ((prefabAssetType == PrefabAssetType.Regular || prefabAssetType == PrefabAssetType.Variant) &&
				PrefabUtility.GetCorrespondingObjectFromSource(go))
			{
				onlyOverriden = true;

				Object prefabObj = PrefabUtility.GetPrefabInstanceHandle(go);
				isPrefabInstance = prefabObj != null;

				if(prefabObj != currentPrefab)
				{
					if (prefabAssetType == PrefabAssetType.Regular)
					{
						TraversePrefab(id, go, stack);
					}
					else
					{
						TraversePrefabVariant(id, go, stack);
					}
					
					currentPrefab = prefabObj;
				}
			}
#endif
			
#if !UNITY_2018_3_OR_NEWER
			PrefabType prefabType = PrefabUtility.GetPrefabType(go);

			if (prefabType == PrefabType.PrefabInstance)
			{
				onlyOverriden = true;
				var prefabObj = PrefabUtility.GetPrefabParent(go);

				if(prefabObj != currentPrefab)
				{
					TraversePrefab(id, prefabAssetType, go, stack);
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
				TraverseGameObject(id, child, stack, currentPrefab);
				stack.Pop();
			}
		}
	}
}