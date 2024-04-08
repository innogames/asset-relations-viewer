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
		/// <summary>
		///  Traverses an object (Monobehaviour, ScriptableObject) to get the dependencies from it
		/// </summary>
		protected abstract void TraverseObject(ResolverDependencySearchContext searchContext, Object obj,
			bool onlyOverriden, Stack<PathSegment> stack);

		/// <summary>
		/// What to do when a prefab got found, in case of searching for assets, it should be added as a dependency
		/// </summary>
		protected abstract void TraversePrefab(ResolverDependencySearchContext searchContext, Object obj,
			Stack<PathSegment> stack);

		/// <summary>
		/// What to do when a PrefabVariant got found, in case of searching for assets, it should be added as a dependency
		/// </summary>
		protected abstract void TraversePrefabVariant(ResolverDependencySearchContext searchContext, Object obj,
			Stack<PathSegment> stack);

		protected void Traverse(ResolverDependencySearchContext searchContext, Object obj, Stack<PathSegment> stack)
		{
			// TODO avoid adding them at another place
			if (obj is Mesh || obj is Texture)
			{
				return;
			}

			if (obj is GameObject gameObject)
			{
				TraverseGameObject(searchContext, gameObject, null, new List<AddedComponent>(), stack);
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

		private void TraverseScene(ResolverDependencySearchContext searchContext, SceneAsset sceneAsset,
			Stack<PathSegment> stack)
		{
			var path = AssetDatabase.GetAssetPath(sceneAsset);

			if (path.StartsWith("Packages"))
			{
				return;
			}

			var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

			TraverseObject(searchContext, sceneAsset, false, stack);
			var addedComponents = new List<AddedComponent>();

			foreach (var go in scene.GetRootGameObjects())
			{
				stack.Push(new PathSegment(go.name, PathSegmentType.GameObject));
				TraverseGameObject(searchContext, go, null, addedComponents, stack);
				stack.Pop();
			}

#pragma warning disable 618
			SceneManager.UnloadScene(scene);
#pragma warning restore 618
		}

		private void TraverseGameObject(ResolverDependencySearchContext searchContext, GameObject go,
			Object currentPrefab, List<AddedComponent> prefabAddedComponents, Stack<PathSegment> stack)
		{
			var isPrefabInstance = false;
			var onlyOverriden = false;
			var prefabAssetType = PrefabUtility.GetPrefabAssetType(go);

			if ((prefabAssetType == PrefabAssetType.Regular || prefabAssetType == PrefabAssetType.Variant) &&
			    PrefabUtility.GetCorrespondingObjectFromSource(go))
			{
				onlyOverriden = true;

				var prefabObj = PrefabUtility.GetPrefabInstanceHandle(go);
				isPrefabInstance = prefabObj != null;

				if (prefabObj != currentPrefab)
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

					if (isPrefabInstance)
					{
						prefabAddedComponents = PrefabUtility.GetAddedComponents(go);
						var addedGameObjects = PrefabUtility.GetAddedGameObjects(go);
						var propertyModifications = PrefabUtility.GetPropertyModifications(go);

						if (propertyModifications.All(modification =>
							    modification.target is Transform || modification.propertyPath == "m_Name") &&
						    prefabAddedComponents.Count == 0 && addedGameObjects.Count == 0)
						{
							return;
						}
					}
				}
			}

			foreach (var component in go.GetComponents<Component>())
			{
				if (component == null)
				{
					continue;
				}

				var componentOverriden = onlyOverriden;

				if (isPrefabInstance)
				{
					var isAddedComponent =
						prefabAddedComponents.Any(addedComponent => addedComponent.instanceComponent == component);
					componentOverriden &= !isAddedComponent;
				}

				var segmentName = component.GetType().Name;

				stack.Push(new PathSegment(segmentName, PathSegmentType.Component));
				TraverseObject(searchContext, component, componentOverriden, stack);
				stack.Pop();
			}

			for (var i = 0; i < go.transform.childCount; ++i)
			{
				var child = go.transform.GetChild(i).gameObject;

				stack.Push(new PathSegment(child.name, PathSegmentType.GameObject));
				TraverseGameObject(searchContext, child, currentPrefab, prefabAddedComponents, stack);
				stack.Pop();
			}
		}
	}
}