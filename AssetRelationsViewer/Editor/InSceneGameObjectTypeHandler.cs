﻿using System;
using System.Collections.Generic;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	/// <summary>
	/// For InScene nodes we cant display any texture
	/// </summary>
	public class InSceneGameObjectVisualizationNodeData : VisualizationNodeData
	{
		public override Texture2D AssetPreviewTexture => null;
		public override Texture2D ThumbNailTexture => null;
	}

	/// <summary>
	/// TypeHandler for InScene GameObject nodes.
	/// These are GameObjects of the currently opened Scene or Prefab
	/// </summary>
	[UsedImplicitly]
	public class InSceneGameObjectTypeHandler : ITypeHandler
	{
		private readonly PrefValueBool SyncPref = new PrefValueBool("InSceneTypeHandler_Sync", false);
		private readonly PrefValueBool AutoRefreshPref = new PrefValueBool("InSceneTypeHandler_AutoRefresh", true);
		private AssetRelationsViewerWindow _viewerWindow;
		private NodeDependencyLookupContext _context;

		private HashSet<string> _nodes = new HashSet<string>();

		private GameObject _currentNode = null;
		private int _currentLoadedSceneKey;

		private readonly Type cacheType = typeof(OpenSceneDependencyCache);
		private readonly Type resolverType = typeof(InSceneDependencyResolver);

		public string GetHandledType()
		{
			return InSceneNodeType.Name;
		}

		public string GetSortingKey(string name)
		{
			return $"InScene {name}";
		}

		public VisualizationNodeData CreateNodeCachedData(Node node)
		{
			return new InSceneGameObjectVisualizationNodeData();
		}

		public string GetNodeDisplayName(Node node)
		{
			return node.Name;
		}

		public void SelectInEditor(string id)
		{
			foreach (var rootGameObject in OpenSceneDependencyCache.GetRootGameObjects())
			{
				SelectGameObject(rootGameObject, id);
			}
		}

		private void SelectGameObject(GameObject go, string id)
		{
			if (go.GetHashCode().ToString() == id)
			{
				Selection.activeObject = go;
				return;
			}

			for (var i = 0; i < go.transform.childCount; ++i)
			{
				SelectGameObject(go.transform.GetChild(i).gameObject, id);
			}
		}

		public void OnGui()
		{
			if (!IsCacheActive())
			{
				EditorGUILayout.LabelField("Scene GameObject->GameObject");
				EditorGUILayout.LabelField("dependency type not loaded!");
			}

			EditorPrefUtilities.TogglePref(SyncPref, "Sync to Hierarchy:");
			EditorPrefUtilities.TogglePref(AutoRefreshPref, "Auto refresh scene switch:");

			var newSelection = EditorGUILayout.ObjectField(_currentNode, typeof(GameObject), true) as GameObject;

			var selectionChanged = newSelection != null && newSelection != _currentNode;
			_currentNode = newSelection;

			if (selectionChanged || (_currentNode != null && GUILayout.Button("Select")))
			{
				_viewerWindow.ChangeSelection(_currentNode.GetHashCode().ToString(), InSceneNodeType.Name);
			}

			AutoRefreshSceneAfterChange();
		}

		public void OnSelectAsset(string id, string type)
		{
			var nodeHandler = _context.NodeHandlerLookup[GetHandledType()] as InSceneDependencyNodeHandler;
			var node = nodeHandler.GetGameObjectById(id);

			if (type == InSceneNodeType.Name && node != null)
			{
				_currentNode = node;
				Selection.activeObject = _currentNode;
			}
		}

		public void InitContext(NodeDependencyLookupContext context, AssetRelationsViewerWindow viewerWindow)
		{
			_viewerWindow = viewerWindow;
			_context = context;

			var nodes = new HashSet<string>();

			foreach (var pair in context.CreatedCaches)
			{
				var resolvedNodes = new List<IDependencyMappingNode>();
				pair.Value.Cache.AddExistingNodes(resolvedNodes);

				foreach (var node in resolvedNodes)
				{
					if (node.Type == InSceneNodeType.Name)
						nodes.Add(node.Id);
				}
			}

			_nodes = new HashSet<string>(nodes);
			Selection.selectionChanged += SelectionChanged;
		}

		public bool HandlesCurrentNode()
		{
			return _currentNode != null;
		}

		private void SelectionChanged()
		{
			_currentNode = null;
			var activeObject = Selection.activeObject;

			if (activeObject == null)
			{
				return;
			}

			var hashCode = activeObject.GetHashCode().ToString();

			if (_nodes.Contains(hashCode))
			{
				var nodeHandler = _context.NodeHandlerLookup[GetHandledType()] as InSceneDependencyNodeHandler;
				_currentNode = nodeHandler.GetGameObjectById(hashCode);

				if (SyncPref.GetValue())
				{
					_viewerWindow.ChangeSelection(hashCode, InSceneNodeType.Name);
				}

				_viewerWindow.Repaint();
			}
		}

		private bool IsCacheActive()
		{
			return _viewerWindow.IsCacheAndResolverTypeActive(cacheType, resolverType) &&
			       _viewerWindow.IsCacheAndResolverTypeLoaded(cacheType, resolverType);
		}

		private void AutoRefreshSceneAfterChange()
		{
			if (!AutoRefreshPref.GetValue())
			{
				return;
			}

			if (!IsCacheActive())
			{
				return;
			}

			var loadedScenesKey = GetLoadedScenesKey();

			if (_currentLoadedSceneKey != 0 && loadedScenesKey != _currentLoadedSceneKey)
			{
				_viewerWindow.RefreshContext(cacheType, resolverType, null, true);
			}

			_currentLoadedSceneKey = loadedScenesKey;
		}

		private int GetLoadedScenesKey()
		{
			var result = 0;
			var sceneCount = 0;

#if UNITY_2022_3_OR_NEWER
			sceneCount = SceneManager.loadedSceneCount;
#else
			sceneCount = EditorSceneManager.loadedSceneCount;
#endif
			
			for (var i = 0; i < sceneCount; ++i)
			{
				var scene = SceneManager.GetSceneAt(i);
				result ^= scene.name.GetHashCode();
			}

			return result;
		}
	}
}