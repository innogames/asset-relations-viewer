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
		protected abstract void TraverseObject(ResolverDependencySearchContext searchContext, Object obj, bool onlyOverriden, Stack<PathSegment> stack);

		// What to to when a prefab got found, in case of searching for assets, it should be added as a dependency
		protected abstract void TraversePrefab(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack);

		protected abstract void TraversePrefabVariant(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack);

		protected void Traverse(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack)
		{
			// TODO avoid adding them at another place
			if (obj is Mesh)
			{
				return;
			}

			if (obj is Texture)
			{
				return;
			}

			if (obj is GameObject gameObject)
			{
				TraverseGameObject(searchContext, gameObject, null, stack);
			}
			else if (obj is SceneAsset sceneAsset)
			{
				TraverseScene(searchContext, sceneAsset, stack);
			}
			else
			{
				TraverseObject(searchContext, obj, false, stack);
			}
		}

		private void TraverseScene(ResolverDependencySearchContext searchContext, SceneAsset sceneAsset, Stack<PathSegment> stack)
		{
			string path = AssetDatabase.GetAssetPath(sceneAsset);

			if (path.StartsWith("Packages"))
			{
				return;
			}

			Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

			TraverseObject(searchContext, sceneAsset, false, stack);

			foreach (GameObject go in scene.GetRootGameObjects())
			{
				stack.Push(new PathSegment(go.name, PathSegmentType.GameObject));
				TraverseGameObject(searchContext, go, null, stack);
				stack.Pop();
			}

#pragma warning disable 618
			SceneManager.UnloadScene(scene);
#pragma warning restore 618
		}

		private void TraverseGameObject(ResolverDependencySearchContext searchContext, GameObject go, Object currentPrefab, Stack<PathSegment> stack)
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
						TraversePrefab(searchContext, go, stack);
					}
					else
					{
						TraversePrefabVariant(searchContext, go, stack);
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
				TraverseObject(searchContext, component, componentOverriden, stack);
				stack.Pop();
			}

			for (int i = 0; i < go.transform.childCount; ++i)
			{
				GameObject child = go.transform.GetChild(i).gameObject;

				stack.Push(new PathSegment(child.name, PathSegmentType.GameObject));
				TraverseGameObject(searchContext, child, currentPrefab, stack);
				stack.Pop();
			}
		}
	}
}