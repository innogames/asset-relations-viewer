using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using Com.Innogames.Core.Frontend.NodeDependencyLookup.EditorCoroutine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	public interface INodeDisplayDataProvider
	{
		Color GetConnectionColorForType(string typeId);
		void EnqueueTreeSizeCalculationForNode(VisualizationNodeData nodeData);
	}

	public interface ISelectionChanger
	{
		void ChangeSelection(string id, string type, bool addUndoStep = true);
	}

	/// <summary>
	/// Editor window for the dependency viewer.
	/// </summary>
	public class AssetRelationsViewerWindow : EditorWindow, INodeDisplayDataProvider, ISelectionChanger
	{
		private class NodeDisplayOptions
		{
			public readonly PrefValueInt MaxDepth = new PrefValueInt("ARV_MaxDepth", 4, 0, 64);
			public readonly PrefValueBool ShowNodesOnce = new PrefValueBool("ARV_ShowNodesOnce", false);
			public readonly PrefValueBool ShowHierarchyOnce = new PrefValueBool("ARV_ShowHierarchyOnce", false);
			public readonly PrefValueBool DrawReferencerNodes = new PrefValueBool("ARV_DrawReferencerNodes", true);
			public readonly PrefValueBool ShowPropertyPaths = new PrefValueBool("ARV_ShowPropertyPaths", true);
			public readonly PrefValueBool AlignNodes = new PrefValueBool("ARV_AlignNodes", true);
			public readonly PrefValueBool HideFilteredNodes = new PrefValueBool("ARV_HideFilteredNodes", true);
			public readonly PrefValueBool MergeRelations = new PrefValueBool("ARV_MergeRelations", true);
			public readonly PrefValueBool SortBySize = new PrefValueBool("ARV_SortBySize", false);
			public readonly PrefValueBool OnlyHardDependencies = new PrefValueBool("ARV_OnlyHardDependencies", false);

			public HashSet<string> ConnectionTypesToDisplay = new HashSet<string>();
		}

		private class CacheUpgradeSettingsOptions
		{
			public readonly PrefValueBool AsyncUpdate = new PrefValueBool("AsyncUpdate_V4", true);
			public readonly PrefValueBool ShouldUnloadUnusedAssets = new PrefValueBool("ARV_UnloadUnusedAssets", false);

			public readonly PrefValueInt UnloadUnusedAssetsInterval =
				new PrefValueInt("ARV_UnloadUnusedAssetsInterval", 10000, 100, 100000);
		}

		private class UndoStep
		{
			public string Id;
			public string Type;
		}

		private class MergedNode
		{
			public Connection Target;
			public readonly List<VisualizationConnection.Data> Datas = new List<VisualizationConnection.Data>();
		}

		private class NodeFilterData
		{
			public Node Node;
			public string Name;
			public string TypeName;
			public int SortKey;
		}

		private class AssetCacheData
		{
			public int Size = -1;
		}

		private const string _ownName = "AssetRelationsViewer";
		private string _firstStartupPrefKey = string.Empty;

		private NodeDisplayData _displayData;

		// Selected Node
		private string _selectedNodeId;
		private string _selectedNodeType;

		private readonly int _maxHierarchyDepth = 256;

		private NodeVisualizationNode _nodeStructure;
		private readonly NodeDependencyLookupContext _nodeDependencyLookupContext = new NodeDependencyLookupContext();

		private readonly Dictionary<string, VisualizationNodeData> _cachedVisualizationNodeDatas =
			new Dictionary<string, VisualizationNodeData>();

		private readonly HashSet<string> _visibleNodes = new HashSet<string>();
		private readonly Dictionary<string, AssetCacheData> _cachedNodes = new Dictionary<string, AssetCacheData>();
		private readonly Dictionary<string, bool> _cachedPackedInfo = new Dictionary<string, bool>();
		private readonly HashSet<Node> _nodeSizesReachedNodes = new HashSet<Node>();

		private readonly Stack<UndoStep> _undoSteps = new Stack<UndoStep>();

		private readonly ViewAreaData _viewAreaData = new ViewAreaData();

		private bool _nodeStructureDirty = true;
		private bool _visualizationDirty = true;
		private bool _selectionDirty = true;

		private NodeDisplayOptions _nodeDisplayOptions;
		private CacheUpgradeSettingsOptions _cacheUpgradeSettingsOptions;

		private readonly List<CacheState> _cacheStates = new List<CacheState>();
		private readonly List<ITypeHandler> _typeHandlers = new List<ITypeHandler>();

		private Dictionary<string, ITypeHandler> _typeHandlerLookup = new Dictionary<string, ITypeHandler>();

		private Vector2 _cachesScrollPosition;
		private Vector2 _handlersScrollPosition;

		private NodeSizeThread _nodeSizeThread;

		// node search and filtering
		private string _nodeSearchString = string.Empty;
		private string _typeSearchString = string.Empty;
		private string[] _nodeSearchTokens = new string[0];
		private string[] _typeSearchTokens = new string[0];

		private string _nodeFilterString = string.Empty;
		private string _typeFilterString = string.Empty;
		private string[] _nodeFilterTokens = new string[0];
		private string[] _typeFilterTokens = new string[0];

		private readonly List<Node> _filteredNodes = new List<Node>();
		private string[] _filteredNodeNames = new string[0];

		private int _selectedSearchNodeIndex;

		private readonly Dictionary<string, NodeFilterData> _nodeFilterDataLookup =
			new Dictionary<string, NodeFilterData>();

		private readonly List<NodeFilterData> _nodeSearchList = new List<NodeFilterData>();

		private bool _canUnloadCaches;
		private bool _isInitialized;

		[NonSerialized]
		private bool _isUpdatingCache;

		private Vector2 _displayOptionsScrollPosition;
		private PrefValueBool _filterFoldout;
		private PrefValueBool _infoFoldout;
		private PrefValueBool _miscFoldout;

		private EditorCoroutineWithExceptionHandling _runningCoroutine;

		[MenuItem("Assets/Asset Relations Viewer/Open", false, 0)]
		public static void ShowWindowForAsset()
		{
			ShowWindowForAsset(true, true);
		}

		[MenuItem("Assets/Asset Relations Viewer/Open (No update)", false, 0)]
		public static void ShowWindowForAssetNoUpdate()
		{
			ShowWindowForAsset(false, true);
		}

		[MenuItem("Assets/Asset Relations Viewer/Open (No load)", false, 0)]
		public static void ShowWindowForAssetNoCacheLoad()
		{
			ShowWindowForAsset(false, false);
		}

		private static void ShowWindowForAsset(bool update, bool loadCaches)
		{
			//This workaround is needed because Unity 2017.1.2p4 crashes when calling ShowWindow directly
			//due to what appears to be a bug with showing a progress bar while the asset context menu is still open
			EditorApplication.delayCall += () => { ShowWindowForAssetInternal(update, loadCaches); };
		}

		private static void ShowWindowForAssetInternal(bool update, bool loadCaches)
		{
			var window = ShowWindow(update, loadCaches);
			window.OnAssetSelectionChanged();
		}

		private static AssetRelationsViewerWindow ShowWindow(bool update, bool loadCaches)
		{
			var window = GetWindow<AssetRelationsViewerWindow>(false, _ownName);

			window.Initialize(update, loadCaches);
			return window;
		}

		public void EnqueueTreeSizeCalculationForNode(VisualizationNodeData node)
		{
			// Do this to indicate it is currently being calculated
			node.HierarchySize = -2;
			_nodeSizeThread.EnqueueNodeData(node);
		}

		public void RefreshContext(Type cacheType, Type resolverType, List<string> activeConnectionTypes,
			bool fastUpdate = false)
		{
			var resolverUsageDefinitionList = new ResolverUsageDefinitionList();
			resolverUsageDefinitionList.Add(cacheType, resolverType, true, true, true, activeConnectionTypes);

			ReloadContext(resolverUsageDefinitionList, true, true, fastUpdate);
		}

		public bool IsCacheAndResolverTypeActive(Type cacheType, Type resolverType)
		{
			foreach (var cacheState in _cacheStates)
			{
				if (cacheState.Cache.GetType() != cacheType)
				{
					continue;
				}

				if (!cacheState.IsActive)
				{
					return false;
				}

				foreach (var resolverState in cacheState.ResolverStates)
				{
					if (resolverState.Resolver.GetType() == resolverType && resolverState.IsActive)
					{
						return true;
					}
				}
			}

			return false;
		}

		public bool IsCacheAndResolverTypeLoaded(Type cacheType, Type resolverType)
		{
			var caches = _nodeDependencyLookupContext.CreatedCaches;

			if (caches.TryGetValue(cacheType.FullName, out var cache))
			{
				return cache.CreatedResolvers.ContainsKey(resolverType.FullName);
			}

			return false;
		}

		public Color GetConnectionColorForType(string typeId)
		{
			return _nodeDependencyLookupContext.DependencyTypeLookup.GetDependencyType(typeId).Colour;
		}

		private void OnEnable()
		{
			_isInitialized = false;

			minSize = new Vector2(800, 600);

			_firstStartupPrefKey = EditorPrefUtilities.GetProjectSpecificKey("ARV_FirstStartup_V2.0");
			_displayData = new NodeDisplayData();
			_nodeDisplayOptions = new NodeDisplayOptions();
			_cacheUpgradeSettingsOptions = new CacheUpgradeSettingsOptions();
			_filterFoldout = new PrefValueBool("ARV_FilterFoldout", true);
			_infoFoldout = new PrefValueBool("ARV_InfoFoldout", true);
			_miscFoldout = new PrefValueBool("ARV_MiscFoldout", true);

			HandleFirstStartup();

			CreateCacheStates();
			CreateTypeHandlers();

			SetHandlerContext();

			_nodeSizeThread = new NodeSizeThread(_nodeDependencyLookupContext);
			_nodeSizeThread.Start();
		}

		private void OnDisable()
		{
			_nodeSizeThread.Kill();
		}

		private void Initialize(bool update, bool loadCaches)
		{
			if (_isInitialized)
			{
				return;
			}

			if (loadCaches)
			{
				LoadDependencyCache(CreateCacheUsageList(update));
			}
		}

		private void LoadDependencyCache(ResolverUsageDefinitionList resolverUsageDefinitionList, bool update = true,
			bool partialUpdate = true, bool fastUpdate = false)
		{
			if (_isUpdatingCache)
			{
				return;
			}

			var coroutine = new EditorCoroutineWithExceptionHandling();
			coroutine.Start(LoadDependencyCacheInternal(resolverUsageDefinitionList, update, partialUpdate, fastUpdate),
				exception =>
				{
					_isUpdatingCache = false;
					EditorUtility.ClearProgressBar();
					throw exception;
				});
		}

		private IEnumerator LoadDependencyCacheInternal(ResolverUsageDefinitionList resolverUsageDefinitionList,
			bool update, bool partialUpdate, bool fastUpdate)
		{
			_isUpdatingCache = true;

			_nodeDependencyLookupContext.Reset();
			_nodeDependencyLookupContext.CacheUpdateSettings = new CacheUpdateSettings
			{
				ShouldUnloadUnusedAssets = _cacheUpgradeSettingsOptions.ShouldUnloadUnusedAssets,
				UnloadUnusedAssetsInterval = _cacheUpgradeSettingsOptions.UnloadUnusedAssetsInterval
			};

			yield return null;

			if (_cacheUpgradeSettingsOptions.AsyncUpdate)
			{
				yield return NodeDependencyLookupUtility.LoadDependencyLookupForCachesAsync(
					_nodeDependencyLookupContext, resolverUsageDefinitionList, partialUpdate, fastUpdate);
			}
			else
			{
				NodeDependencyLookupUtility.LoadDependencyLookupForCaches(_nodeDependencyLookupContext,
					resolverUsageDefinitionList, partialUpdate, fastUpdate);
			}

			SetHandlerContext();
			PrepareNodeSearch();

			if (update)
			{
				_nodeFilterDataLookup.Clear();
				_nodeSizesReachedNodes.Clear();
			}

			_isInitialized = true;
			_isUpdatingCache = false;

			Repaint();
		}

		private void PrepareNodeSearch()
		{
			Task.Run(() =>
			{
				BuildNodeSearchLookup();
				FilterNodeList();
			});
		}

		private void SetHandlerContext()
		{
			foreach (var typeHandler in _typeHandlers)
			{
				typeHandler.InitContext(_nodeDependencyLookupContext, this);
			}

			_typeHandlerLookup = BuildTypeHandlerLookup();
		}

		private Dictionary<string, ITypeHandler> BuildTypeHandlerLookup()
		{
			var result = new Dictionary<string, ITypeHandler>();

			foreach (var typeHandler in _typeHandlers)
			{
				result.Add(typeHandler.GetHandledType(), typeHandler);
			}

			return result;
		}

		private ResolverUsageDefinitionList CreateCacheUsageList(bool update)
		{
			var resolverUsageDefinitionList = new ResolverUsageDefinitionList();

			foreach (var state in _cacheStates)
			{
				if (state.IsActive)
				{
					foreach (var resolverState in state.ResolverStates)
					{
						if (resolverState.IsActive)
						{
							var activeConnectionTypes = GetActiveConnectionTypesForResolverState(resolverState);
							resolverUsageDefinitionList.Add(state.Cache.GetType(), resolverState.Resolver.GetType(),
								true, update, update, activeConnectionTypes);
						}
					}
				}
			}

			return resolverUsageDefinitionList;
		}

		private List<string> GetActiveConnectionTypesForResolverState(ResolverState resolverState)
		{
			var activeConnectionTypes = new List<string>();

			foreach (var connectionType in resolverState.Resolver.GetDependencyTypes())
			{
				if (resolverState.ActiveConnectionTypes.Contains(connectionType))
				{
					activeConnectionTypes.Add(connectionType);
				}
			}

			return activeConnectionTypes;
		}

		private void HandleFirstStartup()
		{
			var firstStartup = EditorPrefs.GetBool(_firstStartupPrefKey, true);

			if (firstStartup)
			{
				var setupDefaultResolvers = EditorUtility.DisplayDialog("AssetRelationsViewer first startup",
					"This is the first startup of the AssetRelationsViewer. Do you want to setup default resolver settings and start finding asset dependencies?",
					"Yes", "No");

				if (setupDefaultResolvers)
				{
					SetDefaultResolverAndCacheState();
				}

				EditorPrefs.SetBool(_firstStartupPrefKey, false);
			}
		}

		private void SetDefaultResolverAndCacheState()
		{
			AddDefaultCacheActivation(new AssetDependencyCache(), new ObjectSerializedDependencyResolver());
			AddDefaultCacheActivation(new AssetToFileDependencyCache(), new AssetToFileDependencyResolver());
		}

		private void AddDefaultCacheActivation(IDependencyCache cache, IDependencyResolver resolver)
		{
			var cacheState = new CacheState(cache);
			var resolverState = new ResolverState(resolver);

			cacheState.ResolverStates.Add(resolverState);

			cacheState.IsActive = true;
			resolverState.IsActive = true;
			resolverState.ActiveConnectionTypes = new HashSet<string>(resolver.GetDependencyTypes());

			cacheState.SaveState();
		}

		private void InvalidateNodeStructure()
		{
			_nodeStructureDirty = true;
			InvalidateTreeVisualization();
		}

		public void OnAssetSelectionChanged()
		{
			var assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);

			// Make sure Selection.activeObject is an asset
			if (string.IsNullOrEmpty(assetPath))
			{
				return;
			}

			ChangeSelection(NodeDependencyLookupUtility.GetAssetIdForAsset(Selection.activeObject), AssetNodeType.Name);
			Repaint();
		}
		
		private void OnGUI()
		{
			DrawUiRoot();
		}

		private void DrawUiRoot()
		{
			var e = Event.current;

			if (_isUpdatingCache)
			{
				// Avoid interacting with gui while updating cache
				if (e.type == EventType.MouseDown)
				{
					e.Use();
				}
			}

			DrawHierarchy();
			DrawMenu();

			var area = GetArea();

			if (!area.Contains(e.mousePosition) && e.type == EventType.MouseDrag)
			{
				_viewAreaData.ScrollPosition -= e.delta;
				Repaint();
			}
		}

		private Rect GetArea()
		{
			return new Rect(0, 0, position.width, 16f * 11.0f);
		}

		private void DrawMenu()
		{
			var area = GetArea();
			EditorGUI.DrawRect(area, ARVStyles.TopRectColor);

			GUILayout.BeginArea(area);

			EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(700));
			EditorGUILayout.Space(1);
			EditorGUILayout.BeginVertical("Box", GUILayout.Height(170), GUILayout.MinWidth(300));
			DrawBasicOptions();
			EditorGUILayout.EndVertical();
			DisplayNodeDisplayOptions();
			DisplayCachesAndConnectionTypes();
			DisplaySearchAndFilterNodeList();
			DisplayTypeHandlers();

			EditorGUILayout.EndVertical();
			EditorGUILayout.EndHorizontal();

			GUILayout.EndArea();
		}

		private void DrawBasicOptions()
		{
			GUILayout.BeginHorizontal();

			GUI.enabled = _undoSteps.Count >= 2;
			if (GUILayout.Button("<<"))
			{
				UndoSelection();
			}

			GUI.enabled = true;

			GUILayout.EndHorizontal();

			EditorPrefUtilities.IntSliderPref(_displayData.AssetPreviewSize, "ThumbnailSize:",
				i => InvalidateTreeVisualization());
			EditorPrefUtilities.IntSliderPref(_nodeDisplayOptions.MaxDepth, "NodeDepth:",
				i => InvalidateNodeStructure());

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Reload"))
			{
				ReloadContext(false);
			}

			if (GUILayout.Button("Update"))
			{
				ReloadContext();
			}

			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Save and Update"))
			{
				AssetDatabase.SaveAssets();
				ReloadContext();
			}

			if (GUILayout.Button("Clear Cache and Update"))
			{
				if (EditorUtility.DisplayDialog("Clear cache",
					    "This will clear the cache and might take a while to recompute. Continue?", "Yes", "No"))
				{
					Stopwatch sw = Stopwatch.StartNew();
					AssetDatabase.SaveAssets();
					NodeDependencyLookupUtility.ClearCachedContexts();
					NodeDependencyLookupUtility.ClearCacheFiles();
					_nodeDependencyLookupContext.CreatedCaches.Clear();
					ReloadContext(onFinished: () =>
					{
						Debug.Log($"Finished clearing cache & updating in {sw.Elapsed:g}");
					});
				}
			}

			EditorGUILayout.EndHorizontal();

			DisplayUpgradeSettingsOptions();
		}

		private void RefreshNodeStructure()
		{
			InvalidateNodeStructure();
			Refresh();
		}

		private void RefreshNodeVisualizationData()
		{
			EditorUtility.DisplayProgressBar("Invalidate dependency tree", "Updating tree", 0.0f);

			InvalidateNodePositionData(_nodeStructure, RelationType.DEPENDENCY);
			InvalidateNodePositionData(_nodeStructure, RelationType.REFERENCER);

			EditorUtility.DisplayProgressBar("Prepare tree", "Updating tree", 0.0f);

			PrepareSubTree(RelationType.DEPENDENCY);
			PrepareSubTree(RelationType.REFERENCER);

			EditorUtility.ClearProgressBar();
		}

		private void DisplayUpgradeSettingsOptions()
		{
			EditorPrefUtilities.TogglePref(_cacheUpgradeSettingsOptions.AsyncUpdate, "Async Update");
			EditorPrefUtilities.TogglePref(_cacheUpgradeSettingsOptions.ShouldUnloadUnusedAssets,
				"Unload Unused Assets");
			EditorPrefUtilities.IntSliderPref(_cacheUpgradeSettingsOptions.UnloadUnusedAssetsInterval,
				"Unload Interval");
		}

		private void DisplayNodeDisplayOptions()
		{
			EditorGUILayout.BeginVertical("Box", GUILayout.Width(250), GUILayout.Height(170));
			_displayOptionsScrollPosition = EditorGUILayout.BeginScrollView(_displayOptionsScrollPosition);

			EditorGUILayout.BeginVertical("Box");
			_filterFoldout.SetValue(EditorGUILayout.Foldout(_filterFoldout, "Filter Options"));

			if (_filterFoldout)
			{
				EditorPrefUtilities.TogglePref(_nodeDisplayOptions.ShowNodesOnce, "Show Nodes Once",
					b => InvalidateNodeStructure());
				EditorPrefUtilities.TogglePref(_nodeDisplayOptions.ShowHierarchyOnce, "Show Hierarchy Once",
					b => InvalidateNodeStructure());
				EditorPrefUtilities.TogglePref(_nodeDisplayOptions.OnlyHardDependencies, "Only Hard Dependencies",
					b => InvalidateNodeStructure());
				EditorPrefUtilities.TogglePref(_nodeDisplayOptions.DrawReferencerNodes, "Show Referencers",
					b => InvalidateNodeStructure());
				EditorPrefUtilities.TogglePref(_nodeDisplayOptions.HideFilteredNodes, "Hide Filtered Nodes",
					b => InvalidateNodeStructure());
			}

			EditorGUILayout.EndVertical();

			EditorGUILayout.BeginVertical("Box");
			_infoFoldout.SetValue(EditorGUILayout.Foldout(_infoFoldout, "Info Options"));

			if (_infoFoldout)
			{
				EditorPrefUtilities.TogglePref(_displayData.ShowAdditionalInformation, "Show size info",
					b => RefreshNodeStructure());
				EditorPrefUtilities.TogglePref(_displayData.ShowAssetPreview, "Show AssetPreview",
					b => RefreshNodeStructure());
				EditorPrefUtilities.TogglePref(_displayData.HighlightPackagedAssets, "Highlight packaged assets",
					b => InvalidateNodeStructure());
			}

			EditorGUILayout.EndVertical();

			EditorGUILayout.BeginVertical("Box");
			_miscFoldout.SetValue(EditorGUILayout.Foldout(_miscFoldout, "Misc Options"));

			if (_miscFoldout)
			{
				EditorPrefUtilities.TogglePref(_nodeDisplayOptions.ShowPropertyPaths, "Show Property Paths",
					b => InvalidateNodeStructure());
				EditorPrefUtilities.TogglePref(_nodeDisplayOptions.AlignNodes, "Align Nodes",
					b => InvalidateTreeVisualization());
				EditorPrefUtilities.TogglePref(_nodeDisplayOptions.MergeRelations, "Merge Relations",
					b => InvalidateNodeStructure());
				EditorPrefUtilities.TogglePref(_nodeDisplayOptions.SortBySize, "Sort child nodes By Size",
					b => InvalidateNodeStructure());
			}

			EditorGUILayout.EndVertical();

			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();
		}

		private void CreateCacheStates()
		{
			_cacheStates.Clear();

			Profiler.BeginSample("Find Caches");
			var types = NodeDependencyLookupUtility.GetTypesForBaseType(typeof(IDependencyCache));
			Profiler.EndSample();

			foreach (var type in types)
			{
				var cache = NodeDependencyLookupUtility.InstantiateClass<IDependencyCache>(type);
				var cacheState = new CacheState(cache);

				Profiler.BeginSample("Find Resolvers");
				var resolverTypes = NodeDependencyLookupUtility.GetTypesForBaseType(cache.GetResolverType());
				Profiler.EndSample();

				foreach (var rtype in resolverTypes)
				{
					var dependencyResolver = NodeDependencyLookupUtility.InstantiateClass<IDependencyResolver>(rtype);
					cacheState.ResolverStates.Add(new ResolverState(dependencyResolver));
				}

				cacheState.UpdateActivation();

				_cacheStates.Add(cacheState);
			}
		}

		private void CreateTypeHandlers()
		{
			_typeHandlers.Clear();

			var types = NodeDependencyLookupUtility.GetTypesForBaseType(typeof(ITypeHandler));

			foreach (var type in types)
			{
				var typeHandler = NodeDependencyLookupUtility.InstantiateClass<ITypeHandler>(type);
				_typeHandlers.Add(typeHandler);
			}
		}

		private void ChangeValue<T>(ref T value, T newValue, ref bool changed)
		{
			changed |= !value.Equals(newValue);

			value = newValue;
		}

		private NodeFilterData GetOrCreateSearchDataForNode(Node node)
		{
			if (_nodeFilterDataLookup.TryGetValue(node.Key, out var cachedFilterData))
			{
				return cachedFilterData;
			}

			return CreateSearchDataForNode(node);
		}

		private NodeFilterData CreateSearchDataForNode(Node node)
		{
			var filterData = new NodeFilterData
				{ Node = node, Name = node.Name.ToLowerInvariant(), TypeName = node.ConcreteType.ToLowerInvariant() };
			_nodeFilterDataLookup.Add(node.Key, filterData);

			return filterData;
		}

		private void BuildNodeSearchLookup()
		{
			var update = _nodeFilterDataLookup.Count == 0;
			_nodeSearchList.Clear();
			var nodes = _nodeDependencyLookupContext.RelationsLookup.GetAllNodes();

			foreach (var node in nodes)
			{
				if (!update && _nodeFilterDataLookup.TryGetValue(node.Key, out var cachedFilterData))
				{
					_nodeSearchList.Add(cachedFilterData);
					continue;
				}

				var filterData = CreateSearchDataForNode(node);
				_nodeSearchList.Add(filterData);
			}

			_nodeSearchList.OrderBy(data => data.Name);
		}

		private bool IsNodeMatchingFilter(NodeFilterData filterData, string[] nameTokens, string[] typeTokens)
		{
			foreach (var nameToken in nameTokens)
			{
				if (!filterData.Name.Contains(nameToken))
				{
					return false;
				}
			}

			foreach (var typeToken in typeTokens)
			{
				if (!filterData.TypeName.Contains(typeToken))
				{
					return false;
				}
			}

			return true;
		}

		private void FilterNodeList()
		{
			_filteredNodes.Clear();

			foreach (var filterData in _nodeSearchList)
			{
				var node = filterData.Node;

				if (IsNodeMatchingFilter(filterData, _nodeSearchTokens, _typeSearchTokens))
				{
					_filteredNodes.Add(node);

					if (_filteredNodes.Count > 200)
					{
						break;
					}
				}
			}

			_filteredNodeNames = new string[_filteredNodes.Count];

			for (var i = 0; i < _filteredNodes.Count; i++)
			{
				var filteredNode = _filteredNodes[i];
				_filteredNodeNames[i] = $"[{filteredNode.ConcreteType}] {filteredNode.Name}";
			}
		}

		private void DisplaySearchAndFilterNodeList()
		{
			EditorGUILayout.BeginVertical("Box", GUILayout.Width(280), GUILayout.Height(170));

			DisplayNodeSearchOptions();
			DisplayNodeFilterOptions();

			EditorGUILayout.EndVertical();
		}

		private void DisplayTypeHandlers()
		{
			EditorGUILayout.BeginVertical("Box", GUILayout.Height(170), GUILayout.MinWidth(300));

			_handlersScrollPosition = EditorGUILayout.BeginScrollView(_handlersScrollPosition, GUILayout.Width(300));

			foreach (var typeHandler in _typeHandlers)
			{
				EditorGUILayout.BeginVertical("Box");

				var handledType = typeHandler.GetHandledType();
				var typeHandlerActiveEditorPrefKey = EditorPrefUtilities.GetProjectSpecificKey("Option_" + handledType);
				var isActive = EditorPrefs.GetBool(typeHandlerActiveEditorPrefKey, true);

				var newIsActive = EditorGUILayout.ToggleLeft("Options: " + handledType, isActive);

				if (typeHandler.HandlesCurrentNode())
				{
					var lastRect = GUILayoutUtility.GetLastRect();
					lastRect.height = 2;
					EditorGUI.DrawRect(lastRect, new Color(0.3f, 0.4f, 0.9f, 0.5f));
				}

				if (newIsActive != isActive)
				{
					EditorPrefs.SetBool(typeHandlerActiveEditorPrefKey, newIsActive);
				}

				if (newIsActive)
				{
					typeHandler.OnGui();
				}

				EditorGUILayout.EndVertical();
			}

			EditorGUILayout.EndScrollView();
		}

		private void DisplayNodeSearchOptions()
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Node search:");

			EditorGUILayout.EndHorizontal();
			var changed = false;

			var origWidth = EditorGUIUtility.labelWidth;

			EditorGUIUtility.labelWidth = 50;
			ChangeValue(ref _nodeSearchString, EditorGUILayout.TextField("Name:", _nodeSearchString), ref changed);
			ChangeValue(ref _typeSearchString, EditorGUILayout.TextField("Type:", _typeSearchString), ref changed);
			EditorGUIUtility.labelWidth = origWidth;

			if (changed)
			{
				_nodeSearchTokens = _nodeSearchString.ToLower()
					.Split(' ')
					.Where(s => !string.IsNullOrWhiteSpace(s))
					.ToArray();
				_typeSearchTokens = _typeSearchString.ToLower()
					.Split(' ')
					.Where(s => !string.IsNullOrWhiteSpace(s))
					.ToArray();

				FilterNodeList();
			}

			EditorGUILayout.BeginHorizontal();
			_selectedSearchNodeIndex = Math.Min(_selectedSearchNodeIndex, _filteredNodeNames.Length - 1);
			_selectedSearchNodeIndex = EditorGUILayout.Popup(_selectedSearchNodeIndex, _filteredNodeNames);

			if (_selectedSearchNodeIndex == -1)
			{
				_selectedSearchNodeIndex = 0;
			}

			if (GUILayout.Button("Select", GUILayout.MaxWidth(50)))
			{
				var filteredNode = _filteredNodes[_selectedSearchNodeIndex];
				ChangeSelection(filteredNode.Id, filteredNode.Type);
			}

			EditorGUILayout.EndHorizontal();
		}

		private void DisplayNodeFilterOptions()
		{
			EditorGUILayout.LabelField("Node hierarchy filter:");
			var origWidth = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 50;
			var changed = false;
			ChangeValue(ref _nodeFilterString, EditorGUILayout.TextField("Name:", _nodeFilterString), ref changed);
			ChangeValue(ref _typeFilterString, EditorGUILayout.TextField("Type:", _typeFilterString), ref changed);
			EditorGUIUtility.labelWidth = origWidth;

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Set as Filter", GUILayout.MaxWidth(100)))
			{
				_nodeFilterTokens = _nodeFilterString.ToLower()
					.Split(' ')
					.Where(s => !string.IsNullOrWhiteSpace(s))
					.ToArray();
				_typeFilterTokens = _typeFilterString.ToLower()
					.Split(' ')
					.Where(s => !string.IsNullOrWhiteSpace(s))
					.ToArray();

				InvalidateNodeStructure();
			}

			if (_nodeFilterTokens.Length > 0 || _typeFilterTokens.Length > 0)
			{
				if (GUILayout.Button("Reset filter"))
				{
					_nodeFilterTokens = new string[0];
					_typeFilterTokens = new string[0];
					InvalidateNodeStructure();
				}
			}

			GUILayout.EndHorizontal();
		}

		private string GetActivationStateString(bool value)
		{
			return value ? "Active" : "Inactive";
		}

		private void DisplayCachesAndConnectionTypes()
		{
			EditorGUILayout.BeginVertical("Box", GUILayout.Width(280), GUILayout.Height(170));

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Dependency types:");

			if (_canUnloadCaches &&
			    GUILayout.Button(new GUIContent("U", "Unload currently unused cached and dependency resolvers")))
			{
				UpdateCacheAndResolverActivation();
			}

			EditorGUILayout.EndHorizontal();

			_cachesScrollPosition = EditorGUILayout.BeginScrollView(_cachesScrollPosition);
			EditorGUILayout.BeginVertical(GUILayout.MaxWidth(230));
			var origColor = GUI.contentColor;

			_canUnloadCaches = false;

			var connectionTypeChanged = false;
			var needsCacheLoad = false;
			var loadedConnectionTypesChanged = false;

			foreach (var cacheState in _cacheStates)
			{
				GUI.contentColor = origColor;
				var cacheType = cacheState.Cache.GetType();

				var cacheName = cacheType.Name;

				EditorGUILayout.BeginVertical("Box");

				foreach (var resolverState in cacheState.ResolverStates)
				{
					GUI.contentColor = origColor;

					var resolver = resolverState.Resolver;
					var resolverName = resolver.GetId();

					var resolverIsLoaded = IsCacheAndResolverTypeLoaded(cacheType, resolver.GetType());

					foreach (var connectionTypeName in resolver.GetDependencyTypes())
					{
						var isActiveAndLoaded = cacheState.IsActive && resolverState.IsActive;
						var dependencyType = resolver.GetDependencyTypeForId(connectionTypeName);

						GUI.contentColor = dependencyType.Colour;
						var isActive = resolverState.ActiveConnectionTypes.Contains(connectionTypeName);
						var newIsActive = isActive;

						EditorGUILayout.BeginHorizontal();

						var label = new GUIContent
						{
							text = dependencyType.Name,
							tooltip = $"{dependencyType.Description} \n\n" +
								$"{cacheName}:{GetActivationStateString(cacheState.IsActive)}->\n" +
								$"{resolverName}:{GetActivationStateString(resolverState.IsActive)}->\n{connectionTypeName}"
						};

						ChangeValue(ref newIsActive, EditorGUILayout.ToggleLeft(label, isActive),
							ref connectionTypeChanged);

						if (newIsActive && !isActive)
						{
							resolverState.ActiveConnectionTypes.Add(connectionTypeName);
							loadedConnectionTypesChanged |= resolverIsLoaded && isActiveAndLoaded;
							resolverState.SaveState();
						}
						else if (isActive && !newIsActive)
						{
							resolverState.ActiveConnectionTypes.Remove(connectionTypeName);
							loadedConnectionTypesChanged |= resolverIsLoaded && isActiveAndLoaded;
							resolverState.SaveState();
						}

						if (resolverIsLoaded && isActiveAndLoaded)
						{
							GUI.contentColor = origColor;
							EditorGUILayout.LabelField("L", GUILayout.MaxWidth(10));
						}

						if (!isActiveAndLoaded && newIsActive)
						{
							GUI.contentColor = new Color(0.8f, 0.6f, 0.4f);
							EditorGUILayout.LabelField("R", GUILayout.MaxWidth(10));

							needsCacheLoad = true;
						}

						if (resolverIsLoaded && isActiveAndLoaded && !newIsActive)
						{
							GUI.contentColor = new Color(0.4f, 0.4f, 0.4f);
							EditorGUILayout.LabelField("U", GUILayout.MaxWidth(10));
							_canUnloadCaches = true;
						}

						GUI.contentColor = origColor;

						var refreshContent = new GUIContent("R", $"Refresh dependencies for {dependencyType.Name}");

						if (resolverIsLoaded && isActiveAndLoaded && newIsActive &&
						    GUILayout.Button(refreshContent, GUILayout.MaxWidth(20)))
						{
							var activeConnectionTypes = GetActiveConnectionTypesForResolverState(resolverState);
							RefreshContext(cacheType, resolverState.Resolver.GetType(), activeConnectionTypes);
						}

						EditorGUILayout.EndHorizontal();
					}
				}

				EditorGUILayout.EndVertical();
			}

			GUI.contentColor = origColor;

			EditorGUILayout.EndVertical();
			EditorGUILayout.Space(2);
			EditorGUILayout.EndScrollView();

			if (loadedConnectionTypesChanged)
			{
				ReloadContext(false);
				InvalidateNodeStructure();
			}

			if (needsCacheLoad)
			{
				if (GUILayout.Button("Load caches"))
				{
					UpdateCacheAndResolverActivation();
				}
			}

			EditorGUILayout.EndVertical();
		}

		private void UpdateCacheAndResolverActivation()
		{
			var resolverUsageDefinitionList = new ResolverUsageDefinitionList();

			foreach (var cacheState in _cacheStates)
			{
				var cacheNeedsActivation = false;

				foreach (var resolverState in cacheState.ResolverStates)
				{
					var resolverNeedsActivation = false;

					foreach (var connectionType in resolverState.Resolver.GetDependencyTypes())
					{
						resolverNeedsActivation |= resolverState.ActiveConnectionTypes.Contains(connectionType);
					}

					if (!resolverState.IsActive && resolverNeedsActivation)
					{
						var activeConnectionTypes = GetActiveConnectionTypesForResolverState(resolverState);
						resolverUsageDefinitionList.Add(cacheState.Cache.GetType(), resolverState.Resolver.GetType(),
							true, true, true, activeConnectionTypes);
					}

					resolverState.IsActive = resolverNeedsActivation;
					cacheNeedsActivation |= resolverState.IsActive;
				}

				cacheState.IsActive = cacheNeedsActivation;
				cacheState.SaveState();
			}

			ReloadContext(resolverUsageDefinitionList);
		}

		private HashSet<string> GetConnectionTypesToDisplay()
		{
			var types = new HashSet<string>();

			foreach (var cacheState in _cacheStates)
			{
				if (!cacheState.IsActive)
				{
					continue;
				}

				foreach (var state in cacheState.ResolverStates)
				{
					var connectionTypes = state.Resolver.GetDependencyTypes();

					foreach (var connectionType in connectionTypes)
					{
						if (state.ActiveConnectionTypes.Contains(connectionType))
						{
							types.Add(connectionType);
						}
					}
				}
			}

			return types;
		}

		private void DrawUpdatingCache()
		{
			float width = 130;
			var px = (position.width - width) * 0.2f;
			var py = position.height * 0.5f;

			EditorGUI.LabelField(new Rect(px, py, width, 20), "Updating Cache");
		}

		private void DrawNotLoadedError()
		{
			float width = 130;
			var px = (position.width - width) * 0.5f;
			var py = position.height * 0.5f;

			EditorGUI.LabelField(new Rect(px, py, width, 20), "Cache not loaded");

			if (GUI.Button(new Rect(px, py + 20, width, 20), "Refresh"))
			{
				ReloadContext(false);
			}
		}

		private void DrawNoNodeSelectedError()
		{
			float width = 130;
			var px = (position.width - width) * 0.5f;
			var py = position.height * 0.5f;

			EditorGUI.LabelField(new Rect(px, py, width, 20), "No node selected to show");
		}

		private void DrawNothingSelectedError()
		{
			float width = 1000;
			var px = (position.width - width) * 0.5f;
			var py = position.height * 0.5f;

			EditorGUI.LabelField(new Rect(px, py, width, 400),
				"Please select a node to show.\n" + "Also make sure a resolver and a connection type is selected" +
				"in order to show a dependency tree");

			if (GUI.Button(new Rect(px, py + 50, 200, 30), "Refresh"))
			{
				ReloadContext();
			}
		}

		private void ReloadContext(bool updateCache = true, Action onFinished = null)
		{
			ReloadContext(CreateCacheUsageList(updateCache), onFinished: onFinished);
		}

		private void ReloadContext(ResolverUsageDefinitionList resolverUsageDefinitionList, bool updateCache = true,
			bool partialUpdate = true, bool fastUpdate = false, Action onFinished = null)
		{
			if (_isUpdatingCache)
			{
				return;
			}

			var coroutine = new EditorCoroutineWithExceptionHandling();
			coroutine.Start(
				ReloadContextEnumerator(resolverUsageDefinitionList, updateCache, partialUpdate, fastUpdate,
					onFinished), exception =>
				{
					_isUpdatingCache = false;
					throw exception;
				});
		}

		private IEnumerator ReloadContextEnumerator(ResolverUsageDefinitionList resolverUsageDefinitionList,
			bool updateCache = true, bool partialUpdate = true, bool fastUpdate = false, Action onFinished = null)
		{
			Refresh();
			yield return LoadDependencyCacheInternal(resolverUsageDefinitionList, updateCache, partialUpdate,
				fastUpdate);
			ChangeSelection(_selectedNodeId, _selectedNodeType);
			if (onFinished != null)
			{
				onFinished.Invoke();
			}
		}

		private void PrepareDrawTree(Node rootNode)
		{
			_visibleNodes.Clear();

			if (_nodeStructureDirty || _nodeStructure == null)
			{
				EditorUtility.DisplayProgressBar("Building dependency tree", "Updating tree", 0.0f);

				_nodeDisplayOptions.ConnectionTypesToDisplay = GetConnectionTypesToDisplay();

				Profiler.BeginSample("BuildNodeStructure");
				BuildNodeStructure(rootNode);
				Profiler.EndSample();

				EditorUtility.ClearProgressBar();
			}

			if (_nodeStructure != null)
			{
				Profiler.BeginSample("_viewAreaDataUpdateAreaSize");
				_viewAreaData.UpdateAreaSize(_nodeStructure, position);
				Profiler.EndSample();

				Profiler.BeginSample("_viewAreaDataUpdate");
				_viewAreaData.Update(position);
				Profiler.EndSample();

				if (_visualizationDirty)
				{
					Profiler.BeginSample("RefreshNodeVisualizationData");
					RefreshNodeVisualizationData();
					Profiler.EndSample();
					_visualizationDirty = false;
				}

				_viewAreaData.UpdateAreaSize(_nodeStructure, position);
				_viewAreaData.Update(position);

				if (_selectionDirty)
				{
					JumpToNode(_nodeStructure);
					_viewAreaData.Update(position);
					_selectionDirty = false;
				}
			}
		}

		private void DrawTree()
		{
			if (_nodeStructure != null)
			{
				DrawRelations(_nodeStructure, 1, RelationType.DEPENDENCY);
				DrawRelations(_nodeStructure, 1, RelationType.REFERENCER);

				_nodeStructure.Draw(0, RelationType.DEPENDENCY, this, this, _displayData, _viewAreaData);
			}
		}

		private void PrepareSubTree(RelationType relationType)
		{
			_nodeStructure.CalculateBounds(_displayData, relationType);

			if (_nodeDisplayOptions.AlignNodes)
			{
				var maxPositions = new int[_maxHierarchyDepth];
				GetNodeWidths(_nodeStructure, maxPositions, relationType, 0);
				ApplyNodeWidths(_nodeStructure, maxPositions, relationType, 0);
			}

			_nodeStructure.CalculateXData(0, relationType, _displayData);
			_nodeStructure.CalculateYData(relationType);
		}

		private void DrawHierarchy()
		{
			if (_isUpdatingCache)
			{
				DrawUpdatingCache();
				return;
			}

			if (_nodeDependencyLookupContext == null)
			{
				DrawNotLoadedError();
				return;
			}

			if (_selectedNodeId == null)
			{
				DrawNoNodeSelectedError();
				return;
			}

			var entry = _nodeDependencyLookupContext.RelationsLookup.GetNode(_selectedNodeId, _selectedNodeType);

			if (entry == null)
			{
				DrawNothingSelectedError();
				return;
			}

			PrepareDrawTree(entry);

			var scrollViewStart = GetArea().height;

			_viewAreaData.ScrollPosition = GUI.BeginScrollView(
				new Rect(0, scrollViewStart, position.width, position.height - scrollViewStart),
				_viewAreaData.ScrollPosition, _viewAreaData.Bounds.Rect);

			DrawTree();

			GUI.EndScrollView();
		}

		private VisualizationNodeData AddNodeCacheForNode(Node node)
		{
			_visibleNodes.Add(node.Key);

			if (!_cachedVisualizationNodeDatas.ContainsKey(node.Key))
			{
				var nodeHandler = _nodeDependencyLookupContext.NodeHandlerLookup[node.Type];
				var typeHandler = _typeHandlerLookup[node.Type];

				var data = typeHandler.CreateNodeCachedData(node);

				data.Node = node;
				data.TypeHandler = typeHandler;
				data.IsEditorAsset = nodeHandler.IsNodeEditorOnly(node.Id, node.Type);
				data.IsPackedToApp =
					NodeDependencyLookupUtility.IsNodePackedToApp(node, _nodeDependencyLookupContext,
						_cachedPackedInfo);

				_cachedVisualizationNodeDatas.Add(node.Key, data);
			}

			return _cachedVisualizationNodeDatas[node.Key];
		}

		private void InvalidateTreeVisualization()
		{
			_visualizationDirty = true;
		}

		private void Refresh()
		{
			_nodeSizeThread.Kill();
			_nodeSizeThread.Start();

			_cachedPackedInfo.Clear();

			_cachedVisualizationNodeDatas.Clear();
			_cachedNodes.Clear();
			InvalidateNodeStructure();
		}

		/// <summary>
		/// Scrolls the view to a position where the main asset is centered
		/// </summary>
		private void JumpToNode(VisualizationNodeBase node)
		{
			var nodePos = node.GetPosition(_viewAreaData);
			_viewAreaData.ScrollPosition.x = -_viewAreaData.Bounds.MinX - _viewAreaData.ViewArea.width / 2 + nodePos.x +
				node.Bounds.Width;
			_viewAreaData.ScrollPosition.y = -_viewAreaData.Bounds.MinY - _viewAreaData.ViewArea.height / 2 +
				nodePos.y + node.Bounds.Height;
		}

		/// <summary>
		/// Called when the selection of the currently viewed asset has changed. Also makes sure it is added to the stack so you
		/// can go back to the previously selected ones
		/// <param name="oldSelection"> </param>
		public void ChangeSelection(string id, string type, bool addUndoStep = true)
		{
			if (id == null)
			{
				return;
			}

			if (id != _selectedNodeId || _undoSteps.Count == 0)
			{
				if (addUndoStep)
				{
					_undoSteps.Push(new UndoStep
					{
						Id = id,
						Type = type
					});
				}

				_selectionDirty = true;
				InvalidateNodeStructure();
			}

			_selectedNodeId = id;
			_selectedNodeType = type;

			SetHandlerSelection();
		}

		private void SetHandlerSelection()
		{
			foreach (var typeHandler in _typeHandlers)
			{
				typeHandler.OnSelectAsset(_selectedNodeId, _selectedNodeType);
			}
		}

		/// <summary>
		/// Goes back to the previously selected asset
		/// </summary>
		private void UndoSelection()
		{
			_undoSteps.Pop();
			var undoStep = _undoSteps.Peek();

			ChangeSelection(undoStep.Id, undoStep.Type, false);
		}

		private void InvalidateNodePositionData(VisualizationNodeBase node, RelationType relationType)
		{
			node.InvalidatePositionData();

			foreach (var childConnection in node.GetRelations(relationType))
			{
				InvalidateNodePositionData(childConnection.VNode, relationType);
			}
		}

		private void GetNodeWidths(VisualizationNodeBase node, int[] maxWidths, RelationType relationType, int depth)
		{
			maxWidths[depth] = Math.Max(maxWidths[depth], node.Bounds.Width);

			foreach (var childConnection in node.GetRelations(relationType))
			{
				GetNodeWidths(childConnection.VNode, maxWidths, relationType, depth + 1);
			}
		}

		private void ApplyNodeWidths(VisualizationNodeBase node, int[] maxPositions, RelationType relationType,
			int depth)
		{
			node.ExtendedNodeWidth = maxPositions[depth];

			foreach (var childConnection in node.GetRelations(relationType))
			{
				ApplyNodeWidths(childConnection.VNode, maxPositions, relationType, depth + 1);
			}
		}

		private void DrawRelations(VisualizationNodeBase node, int depth, RelationType relationType)
		{
			Profiler.BeginSample("DrawRelations");
			var visualizationConnections = node.GetRelations(relationType);
			var isRepaint = Event.current.type == EventType.Repaint;

			foreach (var childConnection in visualizationConnections)
			{
				if(isRepaint)
				{
					DrawConnectionForNodes(node, childConnection, relationType, false, visualizationConnections.Count);
				}

				var childNode = childConnection.VNode;

				if (_viewAreaData.IsRectInDrawArea(childNode.TreeBounds.Rect, new Color(0.1f, 0.2f, 0.5f, 0.3f)))
				{
					DrawRelations(childNode, depth + 1, relationType);

					var positionOffset = childNode.GetPositionOffset(_viewAreaData);
					var r = childNode.Bounds.Rect;
					r.Set(r.x, r.y + positionOffset, r.width, r.height);

					if (_viewAreaData.IsRectInDrawArea(r, new Color(0.6f, 0.2f, 0.1f, 0.3f)))
					{
						childNode.Draw(depth, relationType, this, this, _displayData, _viewAreaData);
					}
				}
			}
			
			if(isRepaint)
			{
				var invertedRelationType = NodeDependencyLookupUtility.InvertRelationType(relationType);
				var recursiveChildConnections = node.GetRelations(invertedRelationType, false, true);

				foreach (var childConnection in recursiveChildConnections)
				{
					DrawConnectionForNodes(node, childConnection, invertedRelationType, true,
						recursiveChildConnections.Count);
				}
			}

			Profiler.EndSample();
		}

		private void DrawConnectionForNodes(VisualizationNodeBase node, VisualizationConnection childConnection,
			RelationType relationType, bool isRecursion, int connectionCount)
		{
			Profiler.BeginSample("DrawConnectionForNodes");
			var childNode = childConnection.VNode;
			var current = relationType == RelationType.DEPENDENCY ? node : childNode;
			var target = relationType == RelationType.DEPENDENCY ? childNode : node;

			var currentPos = current.GetPosition(_viewAreaData);
			var targetPos = target.GetPosition(_viewAreaData);

			float distanceBlend = 1;

			if (connectionCount > 20)
			{
				distanceBlend = Mathf.Pow(1 - Mathf.Clamp01(Mathf.Abs(currentPos.y - targetPos.y) / 20000.0f), 3);
			}

			var alphaAmount = (isRecursion ? 0.15f : 1.0f) * distanceBlend;

			if (isRecursion)
			{
				DrawRecursionButton(node, childNode, relationType);
			}

			if (alphaAmount > 0.01)
			{
				DrawConnection(currentPos.x + current.Bounds.Width, currentPos.y, targetPos.x, targetPos.y,
					GetConnectionColorForType(childConnection.Datas[0].Type), alphaAmount);
			}

			Profiler.EndSample();
		}

		private void DrawRecursionButton(VisualizationNodeBase node, VisualizationNodeBase childNode,
			RelationType relationType)
		{
			var offset = relationType == RelationType.REFERENCER ? childNode.Bounds.Width : -16;
			var nodePosition = childNode.GetPosition(_viewAreaData);

			var rect = new Rect(nodePosition.x + offset, nodePosition.y, 16, 16);

			if (GUI.Button(rect, ">"))
			{
				JumpToNode(node);
			}
		}

		private void BuildNodeStructure(Node node)
		{
			var rootConnection = new Connection(node, "Root", new PathSegment[0], true);

			var rootConnectionNode = rootConnection.Node;
			_nodeStructure = GetVisualizationNode(rootConnectionNode);

			var iterations = 0;
			CreateNodeHierarchyRec(new HashSet<string>(), new Stack<NodeVisualizationNode>(), _nodeStructure,
				rootConnection, 0, RelationType.DEPENDENCY, _nodeDisplayOptions, ref iterations);

			if (_nodeDisplayOptions.DrawReferencerNodes)
			{
				iterations = 0;
				CreateNodeHierarchyRec(new HashSet<string>(), new Stack<NodeVisualizationNode>(), _nodeStructure,
					rootConnection, 0, RelationType.REFERENCER, _nodeDisplayOptions, ref iterations);
			}

			_nodeStructureDirty = false;
		}

		private IEnumerable<MergedNode> GetMergedNodes(Node source, List<Connection> connections)
		{
			var result = new Dictionary<string, MergedNode>();
			var i = 0;
			var mergeRelations = _nodeDisplayOptions.MergeRelations.GetValue();
			bool onlyHardDependencies = _nodeDisplayOptions.OnlyHardDependencies;

			foreach (var connection in connections)
			{
				var nodeKey = connection.Node.Key;

				if (onlyHardDependencies && !connection.IsHardDependency)
				{
					continue;
				}

				if (!mergeRelations)
				{
					nodeKey = (i++).ToString(); // leads to nodes not being merged by target
				}

				if (!result.ContainsKey(nodeKey))
				{
					result.Add(nodeKey, new MergedNode { Target = connection });
				}

				result[nodeKey]
					.Datas.Add(new VisualizationConnection.Data(connection.DependencyType, connection.PathSegments,
						connection.IsHardDependency));
			}

			return result.Values;
		}

		private NodeVisualizationNode HasRecursion(string key, Stack<NodeVisualizationNode> visualizationNodeStack)
		{
			var hash = key.GetHashCode();
			foreach (var node in visualizationNodeStack)
			{
				if (node.Hash == hash && node.Key == key)
					return node;
			}

			return null;
		}

		private void CreateNodeHierarchyRec(HashSet<string> addedVisualizationNodes,
			Stack<NodeVisualizationNode> visualizationNodeStack, NodeVisualizationNode visualizationNode,
			Connection connection, int depth, RelationType relationType, NodeDisplayOptions nodeDisplayOptions,
			ref int iterations)
		{
			visualizationNode.SetKey(connection.Node.Key);
			var containedNode = addedVisualizationNodes.Contains(connection.Node.Key);
			var connections = connection.Node.GetRelations(relationType);

			addedVisualizationNodes.Add(visualizationNode.Key);

			if (depth == nodeDisplayOptions.MaxDepth)
			{
				if (connections.Count > 0)
				{
					var cutData = visualizationNode.GetCutData(relationType, true);
					cutData.Entries.Add(new CutData.Entry
						{ Count = connections.Count, CutReason = CutReason.DepthReached });
				}

				iterations++;

				return;
			}

			if (containedNode && nodeDisplayOptions.ShowHierarchyOnce)
			{
				if (connections.Count > 0)
				{
					var cutData = visualizationNode.GetCutData(relationType, true);
					cutData.Entries.Add(new CutData.Entry
						{ Count = connections.Count, CutReason = CutReason.HierarchyAlreadyShown });
				}

				return;
			}

			if (iterations > 0xFFFF)
			{
				var cutData = visualizationNode.GetCutData(relationType, true);
				cutData.Entries.Add(new CutData.Entry
					{ Count = connections.Count, CutReason = CutReason.NodeLimitReached });
				return;
			}

			if (!nodeDisplayOptions.ConnectionTypesToDisplay.Contains(connection.DependencyType) &&
			    connection.DependencyType != "Root")
			{
				return;
			}

			iterations++;

			visualizationNodeStack.Push(visualizationNode);

			var mergedNodes = GetMergedNodes(connection.Node, connections);
			var cutConnectionCount = 0;
			var filteredOutCount = 0;

			foreach (var mergedNode in mergedNodes)
			{
				var childNode = mergedNode.Target.Node;

				if (addedVisualizationNodes.Contains(childNode.Key) && _nodeDisplayOptions.ShowNodesOnce)
				{
					cutConnectionCount++;
					continue;
				}

				var recursionVisualizationNode = HasRecursion(childNode.Key, visualizationNodeStack);
				var isRecursion = recursionVisualizationNode != null;

				var childVisualizationNode = GetVisualizationNode(childNode);
				var visualizationChildNode = isRecursion ? recursionVisualizationNode : childVisualizationNode;

				visualizationChildNode.Filtered = IsNodeFiltered(childNode);

				if (!isRecursion)
				{
					CreateNodeHierarchyRec(addedVisualizationNodes, visualizationNodeStack, visualizationChildNode,
						mergedNode.Target, depth + 1, relationType, nodeDisplayOptions, ref iterations);
				}

				if (!nodeDisplayOptions.HideFilteredNodes ||
				    HasNoneFilteredChildren(childVisualizationNode, relationType))
				{
					AddBidirConnection(relationType, visualizationNode, visualizationChildNode, mergedNode.Datas,
						isRecursion);
					visualizationChildNode.HasNonFilteredChildren = true;
				}
				else
				{
					filteredOutCount++;
				}
			}

			if (cutConnectionCount > 0)
			{
				var cutData = visualizationNode.GetCutData(relationType, true);
				cutData.Entries.Add(new CutData.Entry
					{ Count = cutConnectionCount, CutReason = CutReason.NodeAlreadyShown });
			}

			if (filteredOutCount > 0)
			{
				var cutData = visualizationNode.GetCutData(relationType, true);
				cutData.Entries.Add(new CutData.Entry { Count = filteredOutCount, CutReason = CutReason.FilteredOut });
			}

			SortChildNodes(visualizationNode, relationType);

			visualizationNodeStack.Pop();
		}

		private void AddBidirConnection(RelationType relationType, VisualizationNodeBase node,
			VisualizationNodeBase target, List<VisualizationConnection.Data> datas, bool isRecursion)
		{
			if (_nodeDisplayOptions.ShowPropertyPaths)
			{
				var pathVisualizationNode = new PathVisualizationNode();

				node.AddRelation(relationType, new VisualizationConnection(datas, pathVisualizationNode, false));

				pathVisualizationNode.AddRelation(NodeDependencyLookupUtility.InvertRelationType(relationType),
					new VisualizationConnection(datas, node, false));

				node = pathVisualizationNode;
			}

			node.AddRelation(relationType, new VisualizationConnection(datas, target, isRecursion));

			target.AddRelation(NodeDependencyLookupUtility.InvertRelationType(relationType),
				new VisualizationConnection(datas, node, isRecursion));
		}

		private bool HasNoneFilteredChildren(NodeVisualizationNode node, RelationType relationType)
		{
			foreach (var connection in node.GetRelations(relationType))
			{
				if (connection.VNode.HasNoneFilteredChildren(relationType) ||
				    !connection.VNode.IsFiltered(relationType))
					return true;
			}

			return !node.Filtered;
		}

		private bool IsNodeFiltered(Node node)
		{
			if (_nodeFilterTokens.Length == 0 && _typeFilterTokens.Length == 0)
			{
				return false;
			}

			return !IsNodeMatchingFilter(GetOrCreateSearchDataForNode(node), _nodeFilterTokens, _typeFilterTokens);
		}

		private void SortChildNodes(NodeVisualizationNode visualizationNode, RelationType relationType)
		{
			visualizationNode.SetRelations(
				visualizationNode.GetRelations(relationType, true, true)
					.OrderBy(p => { return p.VNode.GetSortingKey(relationType, _nodeDisplayOptions.SortBySize); })
					.ToList(), relationType);
		}

		private NodeVisualizationNode GetVisualizationNode(Node node)
		{
			return new NodeVisualizationNode
				{ NodeData = AddNodeCacheForNode(node), TypeHandler = _typeHandlerLookup[node.Type] };
		}

		/// <summary>
		/// Draws a bezier curve between two given points
		/// </summary>
		public static void DrawConnection(float sX, float sY, float eX, float eY, Color color, float alphaModifier = 1,
			bool markWeak = false)
		{
			var distance = Math.Abs(sX - eX) / 2.0f;

			if (distance < 0.5)
				return;

			var tan = Math.Max(distance, 0.5f);
			var startPos = new Vector3(sX, sY + 8, 0);
			var endPos = new Vector3(eX, eY + 8, 0);
			var startTan = startPos + Vector3.right * tan;
			var endTan = endPos + Vector3.left * tan;

			color *= ARVStyles.ConnectionColorMod;
			color.a *= alphaModifier;

			Handles.DrawBezier(startPos, endPos, startTan, endTan, color, null, 3);
		}
	}
}
