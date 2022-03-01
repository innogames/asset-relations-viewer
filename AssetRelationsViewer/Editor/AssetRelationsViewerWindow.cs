using System;
using System.Collections.Generic;
using System.Linq;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEngine;

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
	/// 
	/// </summary>
	public class AssetRelationsViewerWindow : EditorWindow, INodeDisplayDataProvider, ISelectionChanger
	{
		private class UndoStep
		{
			public string Id;
			public string Type;
		}
		
		private class MergedNode
		{
			public Connection Target;
			public List<VisualizationConnection.Data> Datas = new List<VisualizationConnection.Data>();
		}
		
		private const string OwnName = "AssetRelationsViewer";
		private const string FirstStartupPrefKey = "ARV_FirstStartup_V1.2";
		
		private NodeDisplayData DisplayData = new NodeDisplayData();
		
		// Selected Node
		private string _selectedNodeId;
		private string _selectedNodeType;

		private int _maxHierarchyDepth = 256;

		private VisualizationNode _nodeStructure = null;
		private NodeDependencyLookupContext _nodeDependencyLookupContext = new NodeDependencyLookupContext();
		private Dictionary<string, VisualizationNodeData> _cachedVisualizationNodeDatas = new Dictionary<string, VisualizationNodeData>();
		private HashSet<string> _visibleNodes = new HashSet<string>();
		private Dictionary<string, AssetCacheData> _cachedNodes = new Dictionary<string, AssetCacheData>();
		private Dictionary<string, NodeDependencyLookupUtility.NodeSize> _cachedNodeSizes = new Dictionary<string, NodeDependencyLookupUtility.NodeSize>();
		private Dictionary<string, bool> _cachedPackedInfo = new Dictionary<string, bool>();

		private bool skipNodeSizeUpdate;

		private Stack<UndoStep> _undoSteps = new Stack<UndoStep>();
		
		private ViewAreaData _viewAreaData = new ViewAreaData();

		private bool _nodeStructureDirty = true;
		private bool _visualizationDirty = true;
		private bool _selectionDirty = true;
		private bool _showThumbnails = false;

		private NodeDisplayOptions _nodeDisplayOptions = new NodeDisplayOptions();

		private List<CacheState> _cacheStates = new List<CacheState>();
		private List<INodeHandler> _nodeHandlers = new List<INodeHandler>();
		private List<ITypeHandler> _typeHandlers = new List<ITypeHandler>();

		private Dictionary<string, INodeHandler> _nodeHandlerLookup = new Dictionary<string, INodeHandler>();
		private Dictionary<string, ITypeHandler> _typeHandlerLookup = new Dictionary<string, ITypeHandler>();

		private PrefValueBool _mergeRelations = new PrefValueBool("MergeRelations", true);
		private PrefValueBool _showthumbnails = new PrefValueBool("Showthumbnails", false);
		
		private Vector2 _cachesScrollPosition;
		private Vector2 _handlersScrollPosition;
		
		private NodeSizeThread _nodeSizeThread;
		
		private string nodeSearchString = String.Empty;
		private string typeSearchString = String.Empty;
		
		private string nodeTmpFilterString = String.Empty;
		private string typeTmpFilterString = String.Empty;
		private string nodeFilterString = String.Empty;
		private string typeFilterString = String.Empty;
		
		private List<Node> filteredNodes = new List<Node>();
		private string[] filteredNodeNames = new string[0];

		private int selectedSearchNodeIndex = 0;

		private Dictionary<string, NodeFilterData> nodeFilterDataLookup = new Dictionary<string, NodeFilterData>();
		private List<NodeFilterData> nodeSearchList = new List<NodeFilterData>();
		private bool nodeSearchDirty = true;
		
		private bool _isInitialized = false;

		private NodeDataCache _nodeDataCache = new NodeDataCache();

		private class NodeFilterData
		{
			public Node Node;
			public string Name;
			public string TypeName;
			public int SortKey;
		}

		public class NodeDisplayOptions
		{
			public PrefValueInt MaxDepth = new PrefValueInt("MaxDepth", 64, 0, 64);
			public PrefValueBool ShowNodesOnce = new PrefValueBool("ShowNodesOnce", false);
			public PrefValueBool ShowHierarchyOnce = new PrefValueBool("ShowHierarchyOnce", true);
			public PrefValueBool DrawReferencerNodes = new PrefValueBool("DrawReferencerNodes", true);
			public PrefValueBool ShowPropertyPathes = new PrefValueBool("ShowPropertyPathes", true);
			public PrefValueBool AlignNodes = new PrefValueBool("AlignNodes", true);
			public PrefValueBool HideFilteredNodes = new PrefValueBool("HideFilteredNodes", true);
			
			public HashSet<string> ConnectionTypesToDisplay = new HashSet<string>();
		}

		public class AssetCacheData
		{
			public int Size = -1;
		}

		[MenuItem("Assets/Show in Asset Relations Viewer", false, 0)]
		public static void ShowWindowForAsset()
		{
			//This workaround is needed because Unity 2017.1.2p4 crashes when calling ShowWindow directly
			//due to what appears to be a bug with showing a progress bar while the asset context menu is still open
#if UNITY_2017_1_OR_NEWER
			EditorApplication.delayCall += () => { ShowWindowForAssetInternal(); };
#else
			ShowWindowForAssetInternal();
#endif
		}

		private static void ShowWindowForAssetInternal()
		{
			AssetRelationsViewerWindow window = ShowWindow();
			window.OnAssetSelectionChanged();
		}

		[MenuItem("Window/Asset Relations Viewer/Open")]
		public static AssetRelationsViewerWindow ShowWindow()
		{
			AssetRelationsViewerWindow window = GetWindow<AssetRelationsViewerWindow>(false, OwnName);

			window.Initialize();
			return window;
		}

		public void OnEnable()
		{
			_isInitialized = false;
			
			HandleFirstStartup();
			
			CreateCacheStates();
			CreateNodeHandlers();
			CreateTypeHandlers();
			
			SetHandlerContext();
			SetHandlerSelection();
			
			_nodeDataCache.Initialize(_nodeHandlerLookup);
			_nodeDataCache.LoadCache(NodeDependencyLookupUtility.DEFAULT_CACHE_SAVE_PATH);

			_nodeSizeThread = new NodeSizeThread(this);
			_nodeSizeThread.Start();
		}

		private void OnDisable()
		{
			_nodeSizeThread.Kill();
		}

		private void Initialize()
		{
			if (_isInitialized)
			{
				return;
			}
			
			LoadDependencyCache();

			_isInitialized = true;
		}

		private void LoadDependencyCache(bool update = true)
		{
			_nodeDependencyLookupContext.Reset();

			ResolverUsageDefinitionList resolverUsageDefinitionList = CreateCacheUsageList(update);

			ProgressBase progress = new ProgressBase(null);
			progress.SetProgressFunction((title, info, value) => EditorUtility.DisplayProgressBar(title, info, value));
			
			NodeDependencyLookupUtility.LoadDependencyLookupForCaches(_nodeDependencyLookupContext, resolverUsageDefinitionList, progress);
			
			SetHandlerContext();
			
			if (update)
			{
				nodeFilterDataLookup.Clear();
				_cachedNodeSizes.Clear();
			}

			nodeSearchDirty = true;
		}

		private void PrepareNodeSearch(bool isRefresh)
		{
			BuildNodeSearchLookup(isRefresh);
			FilterNodeList();
			nodeSearchDirty = false;
		}
		
		private void SetHandlerContext()
		{
			_nodeHandlerLookup = BuildNodeHandlerLookup();
			
			foreach (ITypeHandler typeHandler in _typeHandlers)
			{
				string handledType = typeHandler.GetHandledType();
				INodeHandler nodeHandler = _nodeHandlerLookup[handledType];
				typeHandler.InitContext(_nodeDependencyLookupContext, this, nodeHandler);
			}
			
			_typeHandlerLookup = BuildTypeHandlerLookup();
		}
		
		public Dictionary<string, INodeHandler> BuildNodeHandlerLookup()
		{
			Dictionary<string, INodeHandler> result = new Dictionary<string, INodeHandler>();

			foreach (INodeHandler nodeHandler in _nodeHandlers)
			{
				result.Add(nodeHandler.GetHandledNodeType(), nodeHandler);
			}

			return result;
		}
		
		public Dictionary<string, ITypeHandler> BuildTypeHandlerLookup()
		{
			Dictionary<string, ITypeHandler> result = new Dictionary<string, ITypeHandler>();

			foreach (ITypeHandler typeHandler in _typeHandlers)
			{
				result.Add(typeHandler.GetHandledType(), typeHandler);
			}

			return result;
		}

		private ResolverUsageDefinitionList CreateCacheUsageList(bool update)
		{
			ResolverUsageDefinitionList resolverUsageDefinitionList = new ResolverUsageDefinitionList();
			
			foreach (CacheState state in _cacheStates)
			{
				if (state.IsActive)
				{
					foreach (ResolverState resolverState in state.ResolverStates)
					{
						if (resolverState.IsActive)
						{
							List<string> activeConnectionTypes = new List<string>();

							foreach (string connectionType in resolverState.Resolver.GetConnectionTypes())
							{
								if (resolverState.ActiveConnectionTypes.Contains(connectionType))
								{
									activeConnectionTypes.Add(connectionType);
								}
							}
							
							resolverUsageDefinitionList.Add(state.Cache.GetType(), resolverState.Resolver.GetType(), true, update, update, activeConnectionTypes);
						}
					}
				}
			}

			return resolverUsageDefinitionList;
		}

		private void HandleFirstStartup()
		{
			bool firstStartup = EditorPrefs.GetBool(FirstStartupPrefKey, true);

			if (firstStartup)
			{
				bool setupDefaultResolvers = EditorUtility.DisplayDialog("AssetRelationsViewer first startup",
					"This is the first startup of the AssetRelationsViewer. Do you want to setup default resolver settings and start finding asset dependencies?",
					"Yes", "No");

				if (setupDefaultResolvers)
				{
					SetDefaultResolverAndCacheState();
				}

				EditorPrefs.SetBool(FirstStartupPrefKey, false);
			}
		}

		private void SetDefaultResolverAndCacheState()
		{
			AddDefaultCacheActivation(new AssetDependencyCache(), new ObjectSerializedDependencyResolver());
			AddDefaultCacheActivation(new AssetToFileDependencyCache(), new AssetToFileDependencyResolver());
		}

		private void AddDefaultCacheActivation(IDependencyCache cache, IDependencyResolver resolver)
		{
			CacheState cacheState = new CacheState(cache);
			ResolverState resolverState = new ResolverState(resolver);
			
			cacheState.ResolverStates.Add(resolverState);

			cacheState.IsActive = true;
			resolverState.IsActive = true;
			resolverState.ActiveConnectionTypes = new HashSet<string>(resolver.GetConnectionTypes());

			cacheState.SaveState();
		}

		public void InvalidateNodeStructure()
		{
			_nodeStructureDirty = true;
			InvalidateTreeVisualization();
		}

		public void OnAssetSelectionChanged()
		{
			string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
			
			// Make sure Selection.activeObject is an asset
			if (string.IsNullOrEmpty(assetPath))
			{
				return;
			}

			AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Selection.activeObject, out string guid, out long fileId);
			string assetId = $"{guid}_{fileId}";
			
			ChangeSelection(assetId, AssetNodeType.Name);
			Repaint();
		}
		
		public void EnqueueTreeSizeCalculationForNode(VisualizationNodeData node)
		{
			node.HierarchySize = -2;
			_nodeSizeThread.EnqueueNodeData(node);
		}

		public void GetCachedOwnSizeForNode(VisualizationNodeData node)
		{
			if (_cachedNodeSizes.ContainsKey(node.Key))
			{
				node.OwnSize = _cachedNodeSizes[node.Key].Size;
			}
		}
		
		public void CalculateTreeSizeForNode(VisualizationNodeData node, HashSet<string> traversedNodes)
		{
			traversedNodes.Clear();

			if (node == null)
			{
				return;
			}
			
			node.HierarchySize =  NodeDependencyLookupUtility.GetTreeSize(node.Key, _nodeDependencyLookupContext,
				_cachedNodeSizes);
		}

		private void OnGUI()
		{
			DrawHierarchy();
			DrawMenu();

			Event e = Event.current;

			Rect area = GetArea();

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
			Rect area = GetArea();
			EditorGUI.DrawRect(area, ARVStyles.TopRectColor);

			GUILayout.BeginArea(area);

			EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(700));
			EditorGUILayout.Space(1);
			EditorGUILayout.BeginVertical("Box", GUILayout.Height(170), GUILayout.MinWidth(300));
			DrawBasicOptions();
			DisplayMiscOptions();
			EditorGUILayout.EndVertical();
			DisplayNodeDisplayOptions();
			DisplayCachesAndConnectionTypes();
			DisplayNodeList();
			
			EditorGUILayout.BeginVertical("Box", GUILayout.Height(170), GUILayout.MinWidth(300));
			
			_handlersScrollPosition = EditorGUILayout.BeginScrollView(_handlersScrollPosition, GUILayout.Width(300));

			foreach (ITypeHandler typeHandler in _typeHandlers)
			{
				EditorGUILayout.BeginVertical("Box");
				
				string handledType = typeHandler.GetHandledType();
				string key = "Option_" + handledType;
				bool isActive = EditorPrefs.GetBool(key, true);

				bool newIsActive = EditorGUILayout.ToggleLeft("Options: " + handledType, isActive);

				if (typeHandler.HandlesCurrentNode())
				{
					Rect lastRect = GUILayoutUtility.GetLastRect();
					lastRect.height = 2;
					EditorGUI.DrawRect(lastRect, new Color(0.3f, 0.4f, 0.9f, 0.5f));
				}

				if (newIsActive != isActive)
				{
					EditorPrefs.SetBool(key, newIsActive);
				}

				if (newIsActive)
				{
					typeHandler.OnGui();
				}

				EditorGUILayout.EndVertical();
			}
			
			EditorGUILayout.EndScrollView();
			
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

			IntSliderPref(DisplayData.AssetPreviewSize, "ThumbnailSize:", i => InvalidateTreeVisualization());
			IntSliderPref(_nodeDisplayOptions.MaxDepth, "NodeDepth:", i => InvalidateNodeStructure());

			if (GUILayout.Button("Refresh"))
			{
				ReloadContext();
			}

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Save and Refresh"))
			{
				AssetDatabase.SaveAssets();
				ReloadContext();
			}
			
			if (GUILayout.Button("Clear and refresh"))
			{
				if (EditorUtility.DisplayDialog("Clear cache", "This will clear the cache and might take a while to recompute. Continue?", "Yes", "No"))
				{
					AssetDatabase.SaveAssets();
					NodeDependencyLookupUtility.ClearCachedContexts();
					NodeDependencyLookupUtility.ClearCacheFiles();
					_nodeDependencyLookupContext.CreatedCaches.Clear();
					ReloadContext();
				}
			}

			EditorGUILayout.EndHorizontal();
		}

		private void RefreshNodeStructure()
		{
			InvalidateNodeStructure();
			Refresh();
		}

		private void RefreshNodeVisualizationData()
		{
			EditorUtility.DisplayProgressBar("Layouting dependency tree", "Updating tree", 0.0f);
			
			InvalidateNodePositionData(_nodeStructure, RelationType.DEPENDENCY);
			InvalidateNodePositionData(_nodeStructure, RelationType.REFERENCER);

			PrepareSubTree(RelationType.DEPENDENCY);
			PrepareSubTree(RelationType.REFERENCER);
			
			EditorUtility.ClearProgressBar();
		}
		
		private void DisplayNodeDisplayOptions()
		{
			EditorGUILayout.BeginVertical("Box", GUILayout.Width(220), GUILayout.Height(170));
			
			TogglePref(_nodeDisplayOptions.ShowNodesOnce, "Show Nodes Once", b => InvalidateNodeStructure());
			TogglePref(_nodeDisplayOptions.ShowHierarchyOnce, "Show Hierarchy Once", b => InvalidateNodeStructure());
			TogglePref(_nodeDisplayOptions.DrawReferencerNodes, "Show Referencers", b => InvalidateNodeStructure());
			TogglePref(_nodeDisplayOptions.ShowPropertyPathes, "Show Property Pathes", b => InvalidateNodeStructure());
			TogglePref(_nodeDisplayOptions.AlignNodes, "Align Nodes", b => InvalidateTreeVisualization());
			TogglePref(_nodeDisplayOptions.HideFilteredNodes, "Hide Filtered Nodes", b => InvalidateNodeStructure());
			
			TogglePref(DisplayData.HighlightPackagedAssets, "Highlight packaged assets", b => InvalidateNodeStructure());
			TogglePref(_mergeRelations, "Merge Relations", b => InvalidateNodeStructure());

			EditorGUILayout.EndVertical();
		}

		public static void TogglePref(PrefValue<bool> pref, string label, Action<bool> onChange = null)
		{
			pref.DirtyOnChange(EditorGUILayout.ToggleLeft(label, pref, GUILayout.Width(180)), onChange);
		}

		public static void IntSliderPref(PrefValue<int> pref, string label, Action<int> onChange = null)
		{
			pref.DirtyOnChange(EditorGUILayout.IntSlider(label, pref, pref.MinValue, pref.MaxValue), onChange);
		}

		private void CreateCacheStates()
		{
			_cacheStates.Clear();
			
			List<Type> types = NodeDependencyLookupUtility.GetTypesForBaseType(typeof(IDependencyCache));

			foreach (Type type in types)
			{
				IDependencyCache cache = NodeDependencyLookupUtility.InstantiateClass<IDependencyCache>(type);
				CacheState cacheState = new CacheState(cache);

				List<Type> resolverTypes = NodeDependencyLookupUtility.GetTypesForBaseType(cache.GetResolverType());
				
				foreach (Type rtype in resolverTypes)
				{
					IDependencyResolver dependencyResolver = NodeDependencyLookupUtility.InstantiateClass<IDependencyResolver>(rtype);
					cacheState.ResolverStates.Add(new ResolverState(dependencyResolver));
				}
				
				cacheState.UpdateActivation();
				
				_cacheStates.Add(cacheState);
			}
		}

		private void CreateNodeHandlers()
		{
			_nodeHandlers.Clear();

			List<Type> types = NodeDependencyLookupUtility.GetTypesForBaseType(typeof(INodeHandler));

			foreach (Type type in types)
			{
				INodeHandler nodeHandler = NodeDependencyLookupUtility.InstantiateClass<INodeHandler>(type);
				_nodeHandlers.Add(nodeHandler);
			}
		}

		private void CreateTypeHandlers()
		{
			_typeHandlers.Clear();

			List<Type> types = NodeDependencyLookupUtility.GetTypesForBaseType(typeof(ITypeHandler));

			foreach (Type type in types)
			{
				ITypeHandler typeHandler = NodeDependencyLookupUtility.InstantiateClass<ITypeHandler>(type);
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
			if (nodeFilterDataLookup.TryGetValue(node.Key, out NodeFilterData cachedFilterData))
			{
				return cachedFilterData;
			}
			
			return CreateSearchDataForNode(node);
		}

		private NodeFilterData CreateSearchDataForNode(Node node)
		{
			NodeDataCache.Entry cacheEntry = _nodeDataCache.GetEntryForId(node.Key);
			string nodeName;
			string typeName;

			if (cacheEntry != null)
			{
				nodeName = cacheEntry.Name;
				typeName = cacheEntry.Type;
			}
			else
			{
				INodeHandler nodeHandler = _nodeHandlerLookup[node.Type];
				nodeHandler.GetNameAndType(node.Id, out nodeName, out typeName);
			}
			
			NodeFilterData filterData = new NodeFilterData {Node = node, Name = nodeName.ToLowerInvariant(), TypeName = typeName.ToLowerInvariant()};
			nodeFilterDataLookup.Add(node.Key, filterData);

			return filterData;
		}

		private void BuildNodeSearchLookup(bool isRefresh)
		{
			bool update = nodeFilterDataLookup.Count == 0;
			nodeSearchList.Clear();
			List<Node> nodes = _nodeDependencyLookupContext.RelationsLookup.GetAllNodes();
			
			_nodeDataCache.Update(nodes, !isRefresh || update);
			_nodeDataCache.SaveCache(NodeDependencyLookupUtility.DEFAULT_CACHE_SAVE_PATH);

			for (var i = 0; i < nodes.Count; i++)
			{
				Node node = nodes[i];

				if (!update && nodeFilterDataLookup.TryGetValue(node.Key, out NodeFilterData cachedFilterData))
				{
					nodeSearchList.Add(cachedFilterData);
					continue;
				}

				NodeFilterData filterData = CreateSearchDataForNode(node);
				nodeSearchList.Add(filterData);

				if (i % 100 == 0)
				{
					EditorUtility.DisplayProgressBar("Getting node search information", filterData.Name, (float)i / nodes.Count);
				}
			}

			EditorUtility.DisplayProgressBar("Sorting node search information", "Sorting", 1);
			nodeSearchList = nodeSearchList.OrderBy(data => data.Name).ToList();
			EditorUtility.ClearProgressBar();
		}

		private bool IsNodeMatchingFilter(NodeFilterData filterData, string nameString, string typeString)
		{
			return filterData.Name.Contains(nameString) && filterData.TypeName.Contains(typeString);
		}
		
		private void FilterNodeList()
		{
			filteredNodes.Clear();

			string nodeSearchName = nodeSearchString.ToLower();
			string typeSearchName = typeSearchString.ToLower();

			foreach (NodeFilterData filterData in nodeSearchList)
			{
				Node node = filterData.Node;

				if (IsNodeMatchingFilter(filterData, nodeSearchName, typeSearchName))
				{
					filteredNodes.Add(node);

					if (filteredNodes.Count > 50)
					{
						break;
					}
				}
			}

			filteredNodeNames = new string[filteredNodes.Count];
			
			for (var i = 0; i < filteredNodes.Count; i++)
			{
				Node filteredNode = filteredNodes[i];
				INodeHandler nodeHandler = _nodeHandlerLookup[filteredNode.Type];
				nodeHandler.GetNameAndType(filteredNode.Id, out string nodeName, out string typeName);
				nodeHandler.GetChangedTimeStamp(filteredNode.Id);
				filteredNodeNames[i] = $"[{typeName}] {nodeName}";
			}
		}

		private void DisplayNodeList()
		{
			EditorGUILayout.BeginVertical("Box", GUILayout.Width(280), GUILayout.Height(170));

			DisplayNodeSearchOptions();
			DisplayNodeFilterOptions();

			EditorGUILayout.EndVertical();
		}

		private void DisplayNodeSearchOptions()
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Node search:");

			bool searchInformationExistent = nodeSearchList.Count > 0;

			if (searchInformationExistent && nodeSearchDirty && GUILayout.Button("Update", GUILayout.MaxWidth(60)))
			{
				PrepareNodeSearch(true);
			}
			
			EditorGUILayout.EndHorizontal();
			bool changed = false;

			float origWidth = EditorGUIUtility.labelWidth;

			if (!searchInformationExistent)
			{
				if (GUILayout.Button("Enable"))
				{
					PrepareNodeSearch(false);
				}
				
				return;
			}
			
			EditorGUIUtility.labelWidth = 50;
			ChangeValue(ref nodeSearchString, EditorGUILayout.TextField("Name:", nodeSearchString), ref changed);
			ChangeValue(ref typeSearchString, EditorGUILayout.TextField("Type:", typeSearchString), ref changed);
			EditorGUIUtility.labelWidth = origWidth;
			
			if (changed)
			{
				foreach (ITypeHandler typeHandler in _typeHandlers)
				{
					typeHandler.ApplyFilterString(nodeSearchString);
				}

				FilterNodeList();
			}

			EditorGUILayout.BeginHorizontal();
			selectedSearchNodeIndex = Math.Min(selectedSearchNodeIndex, filteredNodeNames.Length - 1);
			selectedSearchNodeIndex = EditorGUILayout.Popup(selectedSearchNodeIndex, filteredNodeNames);

			if (selectedSearchNodeIndex == -1)
			{
				selectedSearchNodeIndex = 0;
			}
			
			if (GUILayout.Button("Select", GUILayout.MaxWidth(50)))
			{
				Node filteredNode = filteredNodes[selectedSearchNodeIndex];
				ChangeSelection(filteredNode.Id, filteredNode.Type);
			}
			
			EditorGUILayout.EndHorizontal();
		}

		private void DisplayNodeFilterOptions()
		{
			EditorGUILayout.LabelField("Node hierarchy filter:");
			float origWidth = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = 50;
			bool changed = false;
			ChangeValue(ref nodeTmpFilterString, EditorGUILayout.TextField("Name:", nodeTmpFilterString), ref changed);
			ChangeValue(ref typeTmpFilterString, EditorGUILayout.TextField("Type:", typeTmpFilterString), ref changed);
			EditorGUIUtility.labelWidth = origWidth;

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Set as Filter", GUILayout.MaxWidth(100)))
			{
				nodeFilterString = nodeTmpFilterString;
				typeFilterString = typeTmpFilterString;
				InvalidateNodeStructure();
			}

			if (!string.IsNullOrEmpty(nodeFilterString) || !string.IsNullOrEmpty(typeFilterString))
			{
				if (GUILayout.Button("Reset filter"))
				{
					nodeFilterString = String.Empty;
					typeFilterString = String.Empty;
					InvalidateNodeStructure();
				}
			}
			
			GUILayout.EndHorizontal();
		}

		private bool _canUnloadCaches = false;


		private string GetActivationStateString(bool value)
		{
			return value ? "Active" : "Inactive";
		}

		private void DisplayCachesAndConnectionTypes()
		{
			EditorGUILayout.BeginVertical("Box", GUILayout.Width(280), GUILayout.Height(170));

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Connection types:");

			if (_canUnloadCaches && GUILayout.Button(new GUIContent("U", "Unload currently unused cached and dependency resolvers")))
			{
				UpdateCacheAndResolverActivation();
			}
			
			EditorGUILayout.EndHorizontal();
			
			_cachesScrollPosition = EditorGUILayout.BeginScrollView(_cachesScrollPosition);
			EditorGUILayout.BeginVertical(GUILayout.MaxWidth(190));
			Color origColor = GUI.contentColor;
			
			_canUnloadCaches = false;
			
			bool stateChanged = false;
			bool connectionTypeChanged = false;
			bool needsCacheLoad = false;
			bool loadedConnectionTypesChanged = false;

			foreach (CacheState cacheState in _cacheStates)
			{
				GUI.contentColor = origColor;
				
				string cacheName = cacheState.Cache.GetType().Name;
				bool cacheStateActive = false;

				EditorGUILayout.BeginVertical("Box");

				foreach (ResolverState resolverState in cacheState.ResolverStates)
				{
					GUI.contentColor = origColor;

					bool resolverStateActive = false;

					IDependencyResolver resolver = resolverState.Resolver;
					string resolverName = resolver.GetId();

					foreach (string connectionTypeName in resolver.GetConnectionTypes())
					{
						bool isActiveAndLoaded = cacheState.IsActive && resolverState.IsActive;
						ConnectionType connectionType = resolver.GetDependencyTypeForId(connectionTypeName);

						GUI.contentColor = connectionType.Colour;
						bool isActive = resolverState.ActiveConnectionTypes.Contains(connectionTypeName);
						bool newIsActive = isActive;

						EditorGUILayout.BeginHorizontal();

						string description = $"{connectionType.Description} \n\n" +
						                     $"{cacheName}:{GetActivationStateString(cacheState.IsActive)}->\n" +
						                     $"{resolverName}:{GetActivationStateString(resolverState.IsActive)}->\n{connectionTypeName}";
						
						ChangeValue(ref newIsActive, EditorGUILayout.ToggleLeft(new GUIContent(connectionTypeName, description), isActive), ref connectionTypeChanged);

						resolverStateActive |= newIsActive;
	
						if (newIsActive && !isActive)
						{
							resolverState.ActiveConnectionTypes.Add(connectionTypeName);
							loadedConnectionTypesChanged |= isActiveAndLoaded;
							resolverState.SaveState(); 
						}
						else if (isActive && !newIsActive)
						{
							resolverState.ActiveConnectionTypes.Remove(connectionTypeName);
							loadedConnectionTypesChanged |= isActiveAndLoaded;
							resolverState.SaveState();
						}
						
						if (isActiveAndLoaded)
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
						
						if (isActiveAndLoaded && !newIsActive)
						{
							GUI.contentColor = new Color(0.4f, 0.4f, 0.4f);
							EditorGUILayout.LabelField("U", GUILayout.MaxWidth(10));
							_canUnloadCaches = true;
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
				skipNodeSizeUpdate = true;
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
			foreach (CacheState cacheState in _cacheStates)
			{
				bool cacheNeedsActivation = false;

				foreach (ResolverState resolverState in cacheState.ResolverStates)
				{
					bool resolverNeedsActivation = false;

					foreach (string connectionType in resolverState.Resolver.GetConnectionTypes())
					{
						resolverNeedsActivation |= resolverState.ActiveConnectionTypes.Contains(connectionType);
					}

					resolverState.IsActive = resolverNeedsActivation;
					cacheNeedsActivation |= resolverState.IsActive;
				}

				cacheState.IsActive = cacheNeedsActivation;
				cacheState.SaveState();
			}

			ReloadContext(true);
			InvalidateNodeStructure();
		}

		private HashSet<string> GetConnectionTypesToDisplay()
		{
			HashSet<string> types = new HashSet<string>();
			
			foreach (CacheState cacheState in _cacheStates)
			{
				if (cacheState.IsActive)
				{
					foreach (ResolverState state in cacheState.ResolverStates)
					{
						string[] connectionTypes = state.Resolver.GetConnectionTypes();

						foreach (string connectionType in connectionTypes)
						{
							if (state.ActiveConnectionTypes.Contains(connectionType))
							{
								types.Add(connectionType);
							}
						}
					}
				}
			}
			
			return types;
		}

		private void DisplayMiscOptions()
		{
			TogglePref(DisplayData.ShowAdditionalInformation, "Show additional node information", b => RefreshNodeStructure());
			TogglePref(_showthumbnails, "Show thumbnails", b => RefreshNodeStructure());

			EditorGUILayout.Space();
		}

		private void DrawNotLoadedError()
		{
			float width = 130;
			float px = (position.width - width) * 0.5f;
			float py = position.height * 0.5f;

			EditorGUI.LabelField(new Rect(px, py, width, 20), "Cache not loaded");

			if (GUI.Button(new Rect(px, py + 20, width, 20), "Refresh"))
			{
				ReloadContext();
			}
		}
		
		private void DrawNoNodeSelectedError()
		{
			float width = 130;
			float px = (position.width - width) * 0.5f;
			float py = position.height * 0.5f;

			EditorGUI.LabelField(new Rect(px, py, width, 20), "No node selected to show");
		}
		
		private void DrawNothingSelectedError()
		{
			float width = 1000;
			float px = (position.width - width) * 0.5f;
			float py = position.height * 0.5f;

			EditorGUI.LabelField(new Rect(px, py, width, 400), "Please select a node to show.\n" +
			                                                   "Also make sure a resolver and a connection type is selected" +
			                                                   "in order to show a dependency tree");
			
			if (GUI.Button(new Rect(px, py + 50, 200, 30), "Refresh"))
			{
				ReloadContext();
			}
		}

		private void ReloadContext(bool _updateCache = true)
		{
			Refresh();
			LoadDependencyCache(_updateCache);
			ChangeSelection(_selectedNodeId, _selectedNodeType);
		}

		private void CalculateAllNodeSizes()
		{
			if (!DisplayData.ShowAdditionalInformation || skipNodeSizeUpdate)
			{
				return;
			}
			
			EditorUtility.DisplayProgressBar("Calculating all node sizes", "", 0);
			List<Node> allNodes = _nodeDependencyLookupContext.RelationsLookup.GetAllNodes();

			foreach (Node node in allNodes)
			{
				if (!_cachedNodeSizes.ContainsKey(node.Key))
				{
					NodeDependencyLookupUtility.GetOwnNodeSize(node.Id, node.Type, node.Key,
						_nodeDependencyLookupContext, _cachedNodeSizes);
				}
			}
			
			EditorUtility.ClearProgressBar();
		}

		private void PrepareDrawTree(Node rootNode)
		{
			_visibleNodes.Clear();
			
			if (_nodeStructureDirty || _nodeStructure == null)
			{
				EditorUtility.DisplayProgressBar("Building dependency tree", "Updating tree", 0.0f);

				_nodeDisplayOptions.ConnectionTypesToDisplay = GetConnectionTypesToDisplay();

				BuildNodeStructure(rootNode);

				EditorUtility.ClearProgressBar();
			}

			if (_nodeStructure != null)
			{
				_viewAreaData.UpdateAreaSize(_nodeStructure, position);
				_viewAreaData.Update(position);

				if (_visualizationDirty)
				{
					RefreshNodeVisualizationData();
					CalculateAllNodeSizes();
					skipNodeSizeUpdate = false;
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
				
				_nodeStructure.Draw(0, RelationType.DEPENDENCY, this, this, DisplayData, _viewAreaData);
			}
		}

		private void PrepareSubTree(RelationType relationType)
		{
			_nodeStructure.CalculateBounds(DisplayData, relationType);
			
			if (_nodeDisplayOptions.AlignNodes)
			{
				int[] maxPositions = new int[_maxHierarchyDepth];
				GetNodeWidths(_nodeStructure, maxPositions, relationType, 0);
				ApplyNodeWidths(_nodeStructure, maxPositions, relationType, 0);
			}
			
			_nodeStructure.CalculateXData(0, relationType, DisplayData);
			_nodeStructure.CalculateYData(relationType);
		}

		private void DrawHierarchy()
		{
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

			Node entry = _nodeDependencyLookupContext.RelationsLookup.GetNode(_selectedNodeId, _selectedNodeType);

			if (entry == null)
			{
				DrawNothingSelectedError();
				return;
			}
			
			PrepareDrawTree(entry);

			float scrollViewStart = GetArea().height;
			
			_viewAreaData.ScrollPosition = GUI.BeginScrollView(new Rect(0, scrollViewStart, position.width, position.height - scrollViewStart), _viewAreaData.ScrollPosition, _viewAreaData.Bounds.Rect);

			DrawTree();

			GUI.EndScrollView();
		}

		private VisualizationNodeData AddNodeCacheForNode(string id, string type, string key)
		{
			_visibleNodes.Add(key);
			
			if (!_cachedVisualizationNodeDatas.ContainsKey(key))
			{
				INodeHandler nodeHandler = _nodeHandlerLookup[type];
				ITypeHandler typeHandler = _typeHandlerLookup[type];
				
				VisualizationNodeData data = typeHandler.CreateNodeCachedData(id);

				data.Id = id;
				data.Type = type;
				data.Key = key;

				data.NodeHandler = nodeHandler;
				data.TypeHandler = typeHandler;
				
				nodeHandler.GetNameAndType(id, out string nodeName, out string typeName);

				data.Name = nodeName;
				data.TypeName = typeName;
				data.IsEditorAsset = nodeHandler.IsNodeEditorOnly(id, type);
				data.IsPackedToApp = NodeDependencyLookupUtility.IsNodePackedToApp(id, type, _nodeDependencyLookupContext, _cachedPackedInfo);

				_cachedVisualizationNodeDatas.Add(key, data);
			}

			return _cachedVisualizationNodeDatas[key];
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
			Vector2 nodePos = node.GetPosition(_viewAreaData);
			_viewAreaData.ScrollPosition.x = -_viewAreaData.Bounds.MinX - _viewAreaData.ViewArea.width / 2  + nodePos.x + node.Bounds.Width;
			_viewAreaData.ScrollPosition.y = -_viewAreaData.Bounds.MinY - _viewAreaData.ViewArea.height / 2  + nodePos.y + node.Bounds.Height;
		}

		/// <summary>
		/// Called when the selection of the currently viewed asset has changed. Also makes sure it is added to the stack so you can go back to the previously selected ones
		/// <param name="oldSelection"></param>
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
						Id =  id,
						Type = type,
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
			foreach (ITypeHandler typeHandler in _typeHandlers)
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
			UndoStep undoStep = _undoSteps.Peek();
			
			ChangeSelection(undoStep.Id, undoStep.Type, false);
		}

		public static RelationType InvertRelationType(RelationType relationType)
		{
			switch (relationType)
			{
				case RelationType.DEPENDENCY:
					return RelationType.REFERENCER;
				case RelationType.REFERENCER:
					return RelationType.DEPENDENCY;
				default:
					throw new Exception();
			}
		}

		public Color GetConnectionColorForType(string typeId)
		{
			return _nodeDependencyLookupContext.ConnectionTypeLookup.GetDependencyType(typeId).Colour;
		}

		private void InvalidateNodePositionData(VisualizationNodeBase node, RelationType relationType)
		{
			node.InvalidatePositionData();
			
			foreach (VisualizationConnection childConnection in node.GetRelations(relationType))
			{
				InvalidateNodePositionData(childConnection.VNode, relationType);
			}
		}

		private void GetNodeWidths(VisualizationNodeBase node, int[] maxWidths, RelationType relationType, int depth)
		{
			maxWidths[depth] = Math.Max(maxWidths[depth], node.Bounds.Width);
			
			foreach (VisualizationConnection childConnection in node.GetRelations(relationType))
			{
				GetNodeWidths(childConnection.VNode, maxWidths, relationType, depth + 1);
			}
		}
		
		private void ApplyNodeWidths(VisualizationNodeBase node, int[] maxPositions, RelationType relationType, int depth)
		{
			node.ExtendedNodeWidth = maxPositions[depth];
			foreach (VisualizationConnection childConnection in node.GetRelations(relationType))
			{
				ApplyNodeWidths(childConnection.VNode, maxPositions, relationType, depth + 1);
			}
		}

		private void DrawRelations(VisualizationNodeBase node, int depth, RelationType relationType)
		{
			List<VisualizationConnection> visualizationConnections = node.GetRelations(relationType);

			foreach (VisualizationConnection childConnection in visualizationConnections)
			{
				DrawConnectionForNodes(node, childConnection, relationType, false, visualizationConnections.Count);
				
				VisualizationNodeBase childNode = childConnection.VNode;
	
				if (_viewAreaData.IsRectInDrawArea(childNode.TreeBounds.Rect, new Color(0.1f, 0.2f, 0.5f, 0.3f)))
				{
					DrawRelations(childNode, depth + 1, relationType);

					float positionOffset = childNode.GetPositionOffset(_viewAreaData);
					Rect r = childNode.Bounds.Rect;
					r.Set(r.x, r.y + positionOffset, r.width, r.height);

					if (_viewAreaData.IsRectInDrawArea(r, new Color(0.6f, 0.2f, 0.1f, 0.3f)))
					{
						childNode.Draw(depth, relationType, this, this, DisplayData, _viewAreaData);
					}
				}
			}

			List<VisualizationConnection> childConnections = node.GetRelations(InvertRelationType(relationType), false, true);

			foreach (VisualizationConnection childConnection in childConnections)
			{
				DrawConnectionForNodes(node, childConnection, InvertRelationType(relationType), true, childConnections.Count);
			}
		}

		private void DrawConnectionForNodes(VisualizationNodeBase node, VisualizationConnection childConnection, RelationType relationType, bool isRecursion, int connectionCount)
		{
			VisualizationNodeBase childNode = childConnection.VNode;
			VisualizationNodeBase current = relationType == RelationType.DEPENDENCY ? node : childNode;
			VisualizationNodeBase target = relationType == RelationType.DEPENDENCY ? childNode : node;

			Vector2 currentPos = current.GetPosition(_viewAreaData);
			Vector2 targetPos = target.GetPosition(_viewAreaData);

			float distanceBlend = 1;
			
			if (connectionCount > 20)
			{
				distanceBlend = Mathf.Pow(1 - Mathf.Clamp01(Mathf.Abs(currentPos.y - targetPos.y) / 20000.0f), 3);
			}

			float alphaAmount = (isRecursion ? 0.15f : 1.0f) * distanceBlend;

			DrawRecursionButton(isRecursion, node, childNode, relationType);

			if (childConnection.IsIndicator)
			{
				return;
			}

			if (alphaAmount > 0.01)
			{
				DrawConnection(currentPos.x + current.Bounds.Width, currentPos.y, targetPos.x, targetPos.y, GetConnectionColorForType(childConnection.Datas[0].Type), alphaAmount);
			}
		}

		private void DrawRecursionButton(bool isRecursion, VisualizationNodeBase node, VisualizationNodeBase childNode, RelationType relationType)
		{
			int offset = relationType == RelationType.REFERENCER ? childNode.Bounds.Width :  - 16;
			Vector2 position = childNode.GetPosition(_viewAreaData);

			if (isRecursion && GUI.Button(new Rect(position.x + offset, position.y, 16, 16), ">"))
			{
				JumpToNode(node);
			}
		}

		private void BuildNodeStructure(Node node)
		{
			Connection rootConnection = new Connection(node, "Root", new PathSegment[0]);

			Node rootConnectionNode = rootConnection.Node;
			_nodeStructure = GetVisualizationNode(rootConnectionNode);

			int iterations = 0;
			CreateNodeHierarchyRec(new HashSet<string>(), new Stack<VisualizationNode>(), _nodeStructure, rootConnection, 0, RelationType.DEPENDENCY, _nodeDisplayOptions, ref iterations);

			if (_nodeDisplayOptions.DrawReferencerNodes)
			{
				iterations = 0;
				CreateNodeHierarchyRec(new HashSet<string>(), new Stack<VisualizationNode>(), _nodeStructure, rootConnection, 0, RelationType.REFERENCER, _nodeDisplayOptions, ref iterations);
			}

			_nodeStructureDirty = false;
		}

		private List<MergedNode> GetMergedNodes(List<Connection> connections)
		{	
			Dictionary<string, MergedNode> result = new Dictionary<string, MergedNode>();
			int i = 0;
			
			foreach (Connection connection in connections)
			{
				string nodeKey = NodeDependencyLookupUtility.GetNodeKey(connection.Node.Id, connection.Node.Type);

				if (!_mergeRelations.GetValue())
				{
					nodeKey = (i++).ToString(); // leads to nodes not being merged by target
				}

				if (!result.ContainsKey(nodeKey))
				{
					result.Add(nodeKey, new MergedNode{Target = connection});
				}
				
				result[nodeKey].Datas.Add(new VisualizationConnection.Data(connection.Type, connection.PathSegments));
			}

			return result.Values.ToList();
		}

		private VisualizationNode HasRecursion(string key, Stack<VisualizationNode> visualizationNodeStack)
		{
			int hash = key.GetHashCode();
			foreach (VisualizationNode node in visualizationNodeStack)
			{
				if (node.Hash == hash && node.Key == key)
					return node;
			}

			return null;
		}

		public void CreateNodeHierarchyRec(HashSet<string> addedVisualizationNodes, Stack<VisualizationNode> visualizationNodeStack, VisualizationNode visualizationNode, Connection connection, int depth, RelationType relationType, NodeDisplayOptions nodeDisplayOptions, ref int iterations)
		{
			visualizationNode.SetKey(connection.Node.Key);
			bool containedNode = addedVisualizationNodes.Contains(connection.Node.Key);
			
			bool nodeLimitReached = iterations > 0xFFF;
			
			if (depth == nodeDisplayOptions.MaxDepth || (containedNode && (nodeDisplayOptions.ShowHierarchyOnce || nodeLimitReached)))
			{
				return;
			}

			if (!nodeDisplayOptions.ConnectionTypesToDisplay.Contains(connection.Type) && connection.Type != "Root")
			{
				return;
			}
			
			iterations++;

			addedVisualizationNodes.Add(visualizationNode.Key);
			visualizationNodeStack.Push(visualizationNode);

			List<MergedNode> mergedNodes = GetMergedNodes(connection.Node.GetRelations(relationType));

			foreach (MergedNode mergedNode in mergedNodes)
			{
				Node childNode = mergedNode.Target.Node;

				if (addedVisualizationNodes.Contains(childNode.Key) && _nodeDisplayOptions.ShowNodesOnce)
				{
					continue;
				}
				
				VisualizationNode recursionNode = HasRecursion(childNode.Key, visualizationNodeStack);
				bool isRecursion = recursionNode != null;
				VisualizationNode visualizationChildNode = isRecursion ? recursionNode : GetVisualizationNode(childNode);

				visualizationChildNode.IsFiltered = IsNodeFiltered(childNode);

				if (!isRecursion)
				{
					CreateNodeHierarchyRec(addedVisualizationNodes, visualizationNodeStack, visualizationChildNode, mergedNode.Target, depth + 1, relationType, nodeDisplayOptions, ref iterations);
				}

				if (!nodeDisplayOptions.HideFilteredNodes || HasNoneFilteredChildren(visualizationChildNode, relationType))
				{
					visualizationChildNode.HasNoneFilteredChildren = true;
					AddBidirConnection(relationType, visualizationNode, visualizationChildNode, mergedNode.Datas, isRecursion);
				}
			}
			
			SortChildNodes(visualizationNode, relationType);

			visualizationNodeStack.Pop();
		}

		private void AddBidirConnection(RelationType relationType, VisualizationNodeBase node, VisualizationNodeBase target,
			List<VisualizationConnection.Data> datas, bool isRecursion)
		{
			if (_nodeDisplayOptions.ShowPropertyPathes)
			{
				PathVisualizationNode pathVisualizationNode = new PathVisualizationNode();

				if (!VisualizationConnection.HasPathSegments(datas))
				{
					datas = new List<VisualizationConnection.Data>();
					datas.Add(new VisualizationConnection.Data("UnknownPath", new []{new PathSegment("Unknown Path", PathSegmentType.Unknown)}));
				}
			
				node.AddRelation(relationType, new VisualizationConnection(datas, pathVisualizationNode, false));
				pathVisualizationNode.AddRelation(InvertRelationType(relationType), new VisualizationConnection(datas, node, false));

				node = pathVisualizationNode;
			}
			
			node.AddRelation(relationType, new VisualizationConnection(datas, target, isRecursion));
			target.AddRelation(InvertRelationType(relationType), new VisualizationConnection(datas, node, isRecursion));
		}

		private bool HasNoneFilteredChildren(VisualizationNode node, RelationType relationType)
		{
			foreach (VisualizationConnection connection in node.GetRelations(relationType))
			{
				if (connection.VNode.HasNoneFilteredChildren || !connection.VNode.IsFiltered)
					return true;
			}

			return !node.IsFiltered;
		}

		private bool IsNodeFiltered(Node node)
		{
			if (string.IsNullOrEmpty(nodeFilterString) && string.IsNullOrEmpty(typeFilterString))
			{
				return false;
			}

			return !IsNodeMatchingFilter(GetOrCreateSearchDataForNode(node), nodeFilterString, typeFilterString);
		}

		private void SortChildNodes(VisualizationNode visualizationNode, RelationType relationType)
		{
			visualizationNode.SetRelations(visualizationNode.GetRelations(relationType, true, true).OrderBy(p =>
			{
				return p.VNode.GetSortingKey(relationType);

			}).ToList(), relationType);
		}

		private VisualizationNode GetVisualizationNode(Node node)
		{
			return new VisualizationNode{NodeData = AddNodeCacheForNode(node.Id, node.Type, node.Key)};
		}
		
		/// <summary>
		/// Draws a bezier curve between two given points
		/// </summary>
		public static void DrawConnection(float sX, float sY, float eX, float eY, Color color, float alphaModifier = 1)
		{
			float distance = Math.Abs(sX - eX) / 2.0f;

			if (distance < 0.5)
				return;
			
			float tan = Math.Max(distance, 0.5f);
			
			Vector3 startPos = new Vector3(sX, sY + 8, 0);
			Vector3 endPos = new Vector3(eX, eY + 8, 0);
			Vector3 startTan = startPos + Vector3.right * tan;
			Vector3 endTan = endPos + Vector3.left * tan;

			color *= ARVStyles.ConnectionColorMod;
			color.a *= alphaModifier;

			Handles.DrawBezier(startPos, endPos, startTan, endTan, color, null, 3);
		}
	}
}