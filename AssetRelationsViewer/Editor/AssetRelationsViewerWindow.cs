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
		private class NodeDisplayOptions
		{
			public PrefValueInt MaxDepth = new PrefValueInt("MaxDepth", 64, 0, 64);
			public PrefValueBool ShowNodesOnce = new PrefValueBool("ShowNodesOnce", false);
			public PrefValueBool ShowHierarchyOnce = new PrefValueBool("ShowHierarchyOnce", false);
			public PrefValueBool DrawReferencerNodes = new PrefValueBool("DrawReferencerNodes", true);
			public PrefValueBool ShowPropertyPathes = new PrefValueBool("ShowPropertyPathes", true);
			public PrefValueBool AlignNodes = new PrefValueBool("AlignNodes", true);
			public PrefValueBool HideFilteredNodes = new PrefValueBool("HideFilteredNodes", true);
			public PrefValueBool MergeRelations = new PrefValueBool("MergeRelations", true);

			public HashSet<string> ConnectionTypesToDisplay = new HashSet<string>();
		}
		
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
		
		private const string OwnName = "AssetRelationsViewer";
		private string FirstStartupPrefKey = String.Empty;

		private NodeDisplayData _displayData;
		
		// Selected Node
		private string _selectedNodeId;
		private string _selectedNodeType;

		private int _maxHierarchyDepth = 256;

		private VisualizationNode _nodeStructure = null;
		private readonly NodeDependencyLookupContext _nodeDependencyLookupContext = new NodeDependencyLookupContext();
		private readonly Dictionary<string, VisualizationNodeData> _cachedVisualizationNodeDatas = new Dictionary<string, VisualizationNodeData>();
		private readonly HashSet<string> _visibleNodes = new HashSet<string>();
		private readonly Dictionary<string, AssetCacheData> _cachedNodes = new Dictionary<string, AssetCacheData>();
		private readonly Dictionary<string, NodeDependencyLookupUtility.NodeSize> _cachedNodeSizes = new Dictionary<string, NodeDependencyLookupUtility.NodeSize>();
		private readonly Dictionary<string, bool> _cachedPackedInfo = new Dictionary<string, bool>();

		private bool _skipNodeSizeUpdate;

		private Stack<UndoStep> _undoSteps = new Stack<UndoStep>();
		
		private ViewAreaData _viewAreaData = new ViewAreaData();

		private bool _nodeStructureDirty = true;
		private bool _visualizationDirty = true;
		private bool _selectionDirty = true;

		private NodeDisplayOptions _nodeDisplayOptions;

		private List<CacheState> _cacheStates = new List<CacheState>();
		private List<INodeHandler> _nodeHandlers = new List<INodeHandler>();
		private List<ITypeHandler> _typeHandlers = new List<ITypeHandler>();

		private Dictionary<string, INodeHandler> _nodeHandlerLookup = new Dictionary<string, INodeHandler>();
		private Dictionary<string, ITypeHandler> _typeHandlerLookup = new Dictionary<string, ITypeHandler>();

		private Vector2 _cachesScrollPosition;
		private Vector2 _handlersScrollPosition;
		
		private NodeSizeThread _nodeSizeThread;
		
		// node search and filtering
		private string _nodeSearchString = String.Empty;
		private string _typeSearchString = String.Empty;
		private string[] _nodeSearchTokens = new string[0];
		private string[] _typeSearchTokens = new string[0];
		
		private string _nodeFilterString = String.Empty;
		private string _typeFilterString = String.Empty;
		private string[] _nodeFilterTokens = new string[0];
		private string[] _typeFilterTokens = new string[0];
		
		private readonly List<Node> filteredNodes = new List<Node>();
		private string[] _filteredNodeNames = new string[0];

		private int _selectedSearchNodeIndex = 0;

		private readonly Dictionary<string, NodeFilterData> _nodeFilterDataLookup = new Dictionary<string, NodeFilterData>();
		private List<NodeFilterData> _nodeSearchList = new List<NodeFilterData>();
		private bool _nodeSearchDirty = true;
		
		private bool _canUnloadCaches = false;

		private readonly NodeDataCache _nodeDataCache = new NodeDataCache();
		
		private bool _isInitialized = false;

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
		
		public void RefreshContext(Type cacheType, Type resolverType, List<string> activeConnectionTypes)
		{
			ResolverUsageDefinitionList resolverUsageDefinitionList = new ResolverUsageDefinitionList();
			resolverUsageDefinitionList.Add(cacheType, resolverType, true, true, true, activeConnectionTypes);
			
			ReloadContext(resolverUsageDefinitionList);
		}
		
		public bool IsCacheAndResolverTypeActive(Type cacheType, Type resolverType)
		{
			foreach (CacheState cacheState in _cacheStates)
			{
				if (cacheState.Cache.GetType() != cacheType)
				{
					continue;
				}
				
				if (!cacheState.IsActive)
				{
					return false;
				}
				
				foreach (ResolverState resolverState in cacheState.ResolverStates)
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
			Dictionary<string, CreatedDependencyCache> caches = _nodeDependencyLookupContext.CreatedCaches;

			if (caches.TryGetValue(cacheType.FullName, out CreatedDependencyCache cache))
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
			
			FirstStartupPrefKey = EditorPrefUtilities.GetProjectSpecificKey("ARV_FirstStartup_V1.4");
			_displayData = new NodeDisplayData();
			_nodeDisplayOptions = new NodeDisplayOptions();

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
			
			LoadDependencyCache(CreateCacheUsageList(true));

			_isInitialized = true;
		}

		private void LoadDependencyCache(ResolverUsageDefinitionList resolverUsageDefinitionList, bool update = true, bool partialUpdate = true)
		{
			_nodeDependencyLookupContext.Reset();
			NodeDependencyLookupUtility.LoadDependencyLookupForCaches(_nodeDependencyLookupContext, resolverUsageDefinitionList, partialUpdate);
			
			SetHandlerContext();
			
			if (update)
			{
				_nodeFilterDataLookup.Clear();
				_cachedNodeSizes.Clear();
			}

			_nodeSearchDirty = true;
		}

		private void PrepareNodeSearch(bool isRefresh)
		{
			BuildNodeSearchLookup(isRefresh);
			FilterNodeList();
			_nodeSearchDirty = false;
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

		private Dictionary<string, INodeHandler> BuildNodeHandlerLookup()
		{
			Dictionary<string, INodeHandler> result = new Dictionary<string, INodeHandler>();

			foreach (INodeHandler nodeHandler in _nodeHandlers)
			{
				result.Add(nodeHandler.GetHandledNodeType(), nodeHandler);
			}

			return result;
		}

		private Dictionary<string, ITypeHandler> BuildTypeHandlerLookup()
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
							List<string> activeConnectionTypes = GetActiveConnectionTypesForResolverState(resolverState);
							resolverUsageDefinitionList.Add(state.Cache.GetType(), resolverState.Resolver.GetType(), true, update, update, activeConnectionTypes);
						}
					}
				}
			}

			return resolverUsageDefinitionList;
		}

		private List<string> GetActiveConnectionTypesForResolverState(ResolverState resolverState)
		{
			List<string> activeConnectionTypes = new List<string>();

			foreach (string connectionType in resolverState.Resolver.GetDependencyTypes())
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
			string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
			
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

			EditorPrefUtilities.IntSliderPref(_displayData.AssetPreviewSize, "ThumbnailSize:", i => InvalidateTreeVisualization());
			EditorPrefUtilities.IntSliderPref(_nodeDisplayOptions.MaxDepth, "NodeDepth:", i => InvalidateNodeStructure());

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
			
			EditorPrefUtilities.TogglePref(_nodeDisplayOptions.ShowNodesOnce, "Show Nodes Once", b => InvalidateNodeStructure());
			EditorPrefUtilities.TogglePref(_nodeDisplayOptions.ShowHierarchyOnce, "Show Hierarchy Once", b => InvalidateNodeStructure());
			EditorPrefUtilities.TogglePref(_nodeDisplayOptions.DrawReferencerNodes, "Show Referencers", b => InvalidateNodeStructure());
			EditorPrefUtilities.TogglePref(_nodeDisplayOptions.ShowPropertyPathes, "Show Property Pathes", b => InvalidateNodeStructure());
			EditorPrefUtilities.TogglePref(_nodeDisplayOptions.AlignNodes, "Align Nodes", b => InvalidateTreeVisualization());
			EditorPrefUtilities.TogglePref(_nodeDisplayOptions.HideFilteredNodes, "Hide Filtered Nodes", b => InvalidateNodeStructure());
			EditorPrefUtilities.TogglePref(_nodeDisplayOptions.MergeRelations, "Merge Relations", b => InvalidateNodeStructure());
			
			EditorPrefUtilities.TogglePref(_displayData.HighlightPackagedAssets, "Highlight packaged assets", b => InvalidateNodeStructure());

			EditorGUILayout.EndVertical();
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
			if (_nodeFilterDataLookup.TryGetValue(node.Key, out NodeFilterData cachedFilterData))
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
			_nodeFilterDataLookup.Add(node.Key, filterData);

			return filterData;
		}

		private void BuildNodeSearchLookup(bool isRefresh)
		{
			bool update = _nodeFilterDataLookup.Count == 0;
			_nodeSearchList.Clear();
			List<Node> nodes = _nodeDependencyLookupContext.RelationsLookup.GetAllNodes();
			
			_nodeDataCache.Update(nodes, !isRefresh || update);
			_nodeDataCache.SaveCache(NodeDependencyLookupUtility.DEFAULT_CACHE_SAVE_PATH);

			for (var i = 0; i < nodes.Count; i++)
			{
				Node node = nodes[i];

				if (!update && _nodeFilterDataLookup.TryGetValue(node.Key, out NodeFilterData cachedFilterData))
				{
					_nodeSearchList.Add(cachedFilterData);
					continue;
				}

				NodeFilterData filterData = CreateSearchDataForNode(node);
				_nodeSearchList.Add(filterData);

				if (i % 100 == 0)
				{
					EditorUtility.DisplayProgressBar("Getting node search information", filterData.Name, (float)i / nodes.Count);
				}
			}

			EditorUtility.DisplayProgressBar("Sorting node search information", "Sorting", 1);
			_nodeSearchList = _nodeSearchList.OrderBy(data => data.Name).ToList();
			EditorUtility.ClearProgressBar();
		}

		private bool IsNodeMatchingFilter(NodeFilterData filterData, string[] nameTokens, string[] typeTokens)
		{
			foreach (string nameToken in nameTokens)
			{
				if (!filterData.Name.Contains(nameToken))
				{
					return false;
				}
			}
			
			foreach (string typeToken in typeTokens)
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
			filteredNodes.Clear();

			foreach (NodeFilterData filterData in _nodeSearchList)
			{
				Node node = filterData.Node;

				if (IsNodeMatchingFilter(filterData, _nodeSearchTokens, _typeSearchTokens))
				{
					filteredNodes.Add(node);

					if (filteredNodes.Count > 200)
					{
						break;
					}
				}
			}

			_filteredNodeNames = new string[filteredNodes.Count];
			
			for (var i = 0; i < filteredNodes.Count; i++)
			{
				Node filteredNode = filteredNodes[i];
				INodeHandler nodeHandler = _nodeHandlerLookup[filteredNode.Type];
				nodeHandler.GetNameAndType(filteredNode.Id, out string nodeName, out string typeName);
				nodeHandler.GetChangedTimeStamp(filteredNode.Id);
				_filteredNodeNames[i] = $"[{typeName}] {nodeName}";
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

			foreach (ITypeHandler typeHandler in _typeHandlers)
			{
				EditorGUILayout.BeginVertical("Box");
				
				string handledType = typeHandler.GetHandledType();
				string typeHandlerActiveEditorPrefKey = EditorPrefUtilities.GetProjectSpecificKey("Option_" + handledType);
				bool isActive = EditorPrefs.GetBool(typeHandlerActiveEditorPrefKey, true);

				bool newIsActive = EditorGUILayout.ToggleLeft("Options: " + handledType, isActive);

				if (typeHandler.HandlesCurrentNode())
				{
					Rect lastRect = GUILayoutUtility.GetLastRect();
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

			bool searchInformationExistent = _nodeSearchList.Count > 0;

			if (searchInformationExistent && _nodeSearchDirty && GUILayout.Button("Update", GUILayout.MaxWidth(60)))
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
			ChangeValue(ref _nodeSearchString, EditorGUILayout.TextField("Name:", _nodeSearchString), ref changed);
			ChangeValue(ref _typeSearchString, EditorGUILayout.TextField("Type:", _typeSearchString), ref changed);
			EditorGUIUtility.labelWidth = origWidth;
			
			if (changed)
			{
				_nodeSearchTokens = _nodeSearchString.ToLower().Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
				_typeSearchTokens = _typeSearchString.ToLower().Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

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
				Node filteredNode = filteredNodes[_selectedSearchNodeIndex];
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
			ChangeValue(ref _nodeFilterString, EditorGUILayout.TextField("Name:", _nodeFilterString), ref changed);
			ChangeValue(ref _typeFilterString, EditorGUILayout.TextField("Type:", _typeFilterString), ref changed);
			EditorGUIUtility.labelWidth = origWidth;

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Set as Filter", GUILayout.MaxWidth(100)))
			{
				_nodeFilterTokens = _nodeFilterString.ToLower().Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
				_typeFilterTokens = _typeFilterString.ToLower().Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

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

			if (_canUnloadCaches && GUILayout.Button(new GUIContent("U", "Unload currently unused cached and dependency resolvers")))
			{
				UpdateCacheAndResolverActivation();
			}
			
			EditorGUILayout.EndHorizontal();
			
			_cachesScrollPosition = EditorGUILayout.BeginScrollView(_cachesScrollPosition);
			EditorGUILayout.BeginVertical(GUILayout.MaxWidth(230));
			Color origColor = GUI.contentColor;
			
			_canUnloadCaches = false;
			
			bool connectionTypeChanged = false;
			bool needsCacheLoad = false;
			bool loadedConnectionTypesChanged = false;

			foreach (CacheState cacheState in _cacheStates)
			{
				GUI.contentColor = origColor;
				Type cacheType = cacheState.Cache.GetType();

				string cacheName = cacheType.Name;

				EditorGUILayout.BeginVertical("Box");

				foreach (ResolverState resolverState in cacheState.ResolverStates)
				{
					GUI.contentColor = origColor;

					IDependencyResolver resolver = resolverState.Resolver;
					string resolverName = resolver.GetId();
					
					bool resolverIsLoaded = IsCacheAndResolverTypeLoaded(cacheType, resolver.GetType());

					foreach (string connectionTypeName in resolver.GetDependencyTypes())
					{
						bool isActiveAndLoaded = cacheState.IsActive && resolverState.IsActive;
						DependencyType dependencyType = resolver.GetDependencyTypeForId(connectionTypeName);

						GUI.contentColor = dependencyType.Colour; 
						bool isActive = resolverState.ActiveConnectionTypes.Contains(connectionTypeName);
						bool newIsActive = isActive;

						EditorGUILayout.BeginHorizontal();

						GUIContent label = new GUIContent
						{
							text = dependencyType.Name,
							tooltip = $"{dependencyType.Description} \n\n" +
							          $"{cacheName}:{GetActivationStateString(cacheState.IsActive)}->\n" +
							          $"{resolverName}:{GetActivationStateString(resolverState.IsActive)}->\n{connectionTypeName}"
						};

						ChangeValue(ref newIsActive, EditorGUILayout.ToggleLeft(label, isActive), ref connectionTypeChanged);

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

						GUIContent refreshContent = new GUIContent("R", $"Refresh dependencies for {dependencyType.Name}");
						
						if (resolverIsLoaded && isActiveAndLoaded && newIsActive && GUILayout.Button(refreshContent, GUILayout.MaxWidth(20)))
						{
							List<string> activeConnectionTypes = GetActiveConnectionTypesForResolverState(resolverState);
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
			ResolverUsageDefinitionList resolverUsageDefinitionList = new ResolverUsageDefinitionList();
			
			foreach (CacheState cacheState in _cacheStates)
			{
				bool cacheNeedsActivation = false;

				foreach (ResolverState resolverState in cacheState.ResolverStates)
				{
					bool resolverNeedsActivation = false;

					foreach (string connectionType in resolverState.Resolver.GetDependencyTypes())
					{
						resolverNeedsActivation |= resolverState.ActiveConnectionTypes.Contains(connectionType);
					}

					if (!resolverState.IsActive && resolverNeedsActivation)
					{
						List<string> activeConnectionTypes = GetActiveConnectionTypesForResolverState(resolverState);
						resolverUsageDefinitionList.Add(cacheState.Cache.GetType(), resolverState.Resolver.GetType(), true, true, true, activeConnectionTypes);
					}

					resolverState.IsActive = resolverNeedsActivation;
					cacheNeedsActivation |= resolverState.IsActive;
				}

				cacheState.IsActive = cacheNeedsActivation;
				cacheState.SaveState();
			}
			
			ReloadContext(resolverUsageDefinitionList, true, true);
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
						string[] connectionTypes = state.Resolver.GetDependencyTypes();

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
			EditorPrefUtilities.TogglePref(_displayData.ShowAdditionalInformation, "Show additional node information", b => RefreshNodeStructure(), 240);
			EditorPrefUtilities.TogglePref(_displayData.ShowThumbnails, "Show thumbnails", b => RefreshNodeStructure(), 240);

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
			ReloadContext(CreateCacheUsageList(_updateCache));
		}

		private void ReloadContext(ResolverUsageDefinitionList resolverUsageDefinitionList, bool _updateCache = true, bool partialUpdate = true)
		{
			Refresh();
			LoadDependencyCache(resolverUsageDefinitionList, _updateCache, partialUpdate);
			ChangeSelection(_selectedNodeId, _selectedNodeType);
		}
		
		private void GetAllReachableNodes(Node node, HashSet<Node> reachedNodes)
		{
			if (reachedNodes.Contains(node))
			{
				return;
			}

			reachedNodes.Add(node);
			
			GetAllReachableNodes(node, reachedNodes, RelationType.DEPENDENCY);
			GetAllReachableNodes(node, reachedNodes, RelationType.REFERENCER);
		}

		private void GetAllReachableNodes(Node node, HashSet<Node> reachedNodes, RelationType relationType)
		{
			foreach (Connection connection in node.GetRelations(relationType))
			{
				GetAllReachableNodes(connection.Node, reachedNodes);
			}
		}

		private void CalculateAllNodeSizes(Node rootNode)
		{
			if (!_displayData.ShowAdditionalInformation || _skipNodeSizeUpdate)
			{
				return;
			}

			HashSet<Node> reachedNodes = new HashSet<Node>();
			GetAllReachableNodes(rootNode, reachedNodes);

			List<Node> allNodes = reachedNodes.ToList();
			
			for (var i = 0; i < allNodes.Count; i++)
			{
				Node node = allNodes[i];
				
				if (!_cachedNodeSizes.ContainsKey(node.Key))
				{
					NodeDependencyLookupUtility.GetOwnNodeSize(node.Id, node.Type, node.Key,
						_nodeDependencyLookupContext, _cachedNodeSizes);
				}

				if (i % 100 == 0)
				{
					EditorUtility.DisplayProgressBar("Calculating all node sizes", "", i / (float)allNodes.Count);
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
				
				CalculateAllNodeSizes(rootNode);

				EditorUtility.ClearProgressBar();
			}

			if (_nodeStructure != null)
			{
				_viewAreaData.UpdateAreaSize(_nodeStructure, position);
				_viewAreaData.Update(position);

				if (_visualizationDirty)
				{
					RefreshNodeVisualizationData();
					_skipNodeSizeUpdate = false;
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
				int[] maxPositions = new int[_maxHierarchyDepth];
				GetNodeWidths(_nodeStructure, maxPositions, relationType, 0);
				ApplyNodeWidths(_nodeStructure, maxPositions, relationType, 0);
			}
			
			_nodeStructure.CalculateXData(0, relationType, _displayData);
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
						childNode.Draw(depth, relationType, this, this, _displayData, _viewAreaData);
					}
				}
			}

			List<VisualizationConnection> childConnections = node.GetRelations(NodeDependencyLookupUtility.InvertRelationType(relationType), false, true);

			foreach (VisualizationConnection childConnection in childConnections)
			{
				DrawConnectionForNodes(node, childConnection, NodeDependencyLookupUtility.InvertRelationType(relationType), true, childConnections.Count);
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
			Vector2 nodePosition = childNode.GetPosition(_viewAreaData);

			if (isRecursion && GUI.Button(new Rect(nodePosition.x + offset, nodePosition.y, 16, 16), ">"))
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

				if (!_nodeDisplayOptions.MergeRelations.GetValue())
				{
					nodeKey = (i++).ToString(); // leads to nodes not being merged by target
				}

				if (!result.ContainsKey(nodeKey))
				{
					result.Add(nodeKey, new MergedNode{Target = connection});
				}
				
				result[nodeKey].Datas.Add(new VisualizationConnection.Data(connection.DependencyType, connection.PathSegments));
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

		private void CreateNodeHierarchyRec(HashSet<string> addedVisualizationNodes, Stack<VisualizationNode> visualizationNodeStack, VisualizationNode visualizationNode, Connection connection, int depth, RelationType relationType, NodeDisplayOptions nodeDisplayOptions, ref int iterations)
		{
			visualizationNode.SetKey(connection.Node.Key);
			bool containedNode = addedVisualizationNodes.Contains(connection.Node.Key);
			
			bool nodeLimitReached = iterations > 0xFFF;
			
			if (depth == nodeDisplayOptions.MaxDepth || (containedNode && (nodeDisplayOptions.ShowHierarchyOnce || nodeLimitReached)))
			{
				return;
			}

			if (!nodeDisplayOptions.ConnectionTypesToDisplay.Contains(connection.DependencyType) && connection.DependencyType != "Root")
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
				pathVisualizationNode.AddRelation(NodeDependencyLookupUtility.InvertRelationType(relationType), new VisualizationConnection(datas, node, false));

				node = pathVisualizationNode;
			}
			
			node.AddRelation(relationType, new VisualizationConnection(datas, target, isRecursion));
			target.AddRelation(NodeDependencyLookupUtility.InvertRelationType(relationType), new VisualizationConnection(datas, node, isRecursion));
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
			if (_nodeFilterTokens.Length == 0 && _typeFilterTokens.Length == 0)
			{
				return false;
			}
			
			return !IsNodeMatchingFilter(GetOrCreateSearchDataForNode(node), _nodeFilterTokens, _typeFilterTokens);
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