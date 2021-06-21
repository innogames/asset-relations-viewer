using System;
using System.Collections.Generic;
using System.Linq;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;
using UnityEngine;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	public interface ITypeColorProvider
	{
		Color GetConnectionColorForType(string typeId);
	}
	
	public interface ISelectionChanger
	{
		void ChangeSelection(string id, string type, bool addUndoStep = true);
	}

	/// <summary>
	/// Editor window for the dependency viewer.
	/// 
	/// </summary>
	public class AssetRelationsViewerWindow : EditorWindow, ITypeColorProvider, ISelectionChanger
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
		private string _selectedId;
		private string _selectedType;

		private int _maxHierarchyDepth = 64;

		private VisualizationNode _nodeStructure = null;
		private NodeDependencyLookupContext _nodeDependencyLookupContext = new NodeDependencyLookupContext();
		private Dictionary<string, VisualizationNodeData> _cachedVisualizationNodeDatas = new Dictionary<string, VisualizationNodeData>();
		private Dictionary<string, AssetCacheData> _cachedNodes = new Dictionary<string, AssetCacheData>();

		private Stack<UndoStep> _undoSteps = new Stack<UndoStep>();
		
		private ViewAreaData _viewAreaData = new ViewAreaData();

		private bool _nodeStructureDirty = true;
		private bool _visualizationDirty = true;
		private bool _selectionDirty = true;
		private bool _showThumbnails = false;

		private NodeDisplayOptions _nodeDisplayOptions = new NodeDisplayOptions();

		private List<CacheState> _cacheStates = new List<CacheState>();

		private List<NodeHandlerState> _nodeHandlerStates = new List<NodeHandlerState>();
		private List<ITypeHandler> _typeHandlers = new List<ITypeHandler>();
		
		private PrefValueBool MergeRelations = new PrefValueBool("MergeRelations", true);
		
		private PrefValueBool Showthumbnails = new PrefValueBool("Showthumbnails", false);
		
		private Vector2 m_cachesScrollPosition;
		private Vector2 m_handlersScrollPosition;

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

			window.LoadDependencyCache();
			return window;
		}

		public void OnEnable()
		{
			HandleFirstStartup();
			
			CreateCacheStates();
			CreateNodeHandlers();
			CreateTypeHandlers();
			
			SetHandlerSelection();

			SetNodeHandlerContext();
		}

		private void LoadDependencyCache(bool update = true)
		{
			_nodeDependencyLookupContext.Reset();

			ResolverUsageDefinitionList resolverUsageDefinitionList = CreateCacheUsageList();

			ProgressBase progress = new ProgressBase(null);
			progress.SetProgressFunction((title, info, value) => EditorUtility.DisplayProgressBar(title, info, value));
			
			NodeDependencyLookupUtility.LoadDependencyLookupForCaches(_nodeDependencyLookupContext, resolverUsageDefinitionList, progress, true, update);
			
			SetNodeHandlerContext();
		}

		private void SetNodeHandlerContext()
		{
			foreach (ITypeHandler handler in _typeHandlers)
			{
				handler.InitContext(_nodeDependencyLookupContext, this);
			}
		}

		private ResolverUsageDefinitionList CreateCacheUsageList()
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
							string[] connectionTypes = resolverState.Resolver.GetConnectionTypes();
							
							foreach (string connectionType in connectionTypes)
							{
								if (resolverState.ActiveConnectionTypes.Contains(connectionType))
									activeConnectionTypes.Add(connectionType);
							}
							
							resolverUsageDefinitionList.Add(state.Cache.GetType(), resolverState.Resolver.GetType(), activeConnectionTypes);
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
			
			
			ChangeSelection(assetId, "Asset");
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
			EditorGUILayout.BeginVertical();
			DrawBasicOptions();
			DisplayMiscOptions();
			EditorGUILayout.EndVertical();
			DisplayNodeDisplayOptions();
			DisplayCaches();
			
			EditorGUILayout.BeginVertical("Box");
			
			m_handlersScrollPosition = EditorGUILayout.BeginScrollView(m_handlersScrollPosition, GUILayout.Width(300));

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
			GUILayout.BeginVertical("Box");
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
			if (GUILayout.Button("Save and Refresh"))
			{
				AssetDatabase.SaveAssets();
				ReloadContext();
			}
			
			if (GUILayout.Button("Clear and refresh"))
			{
				AssetDatabase.SaveAssets();
				NodeDependencyLookupUtility.ClearCachedContexts();
				NodeDependencyLookupUtility.ClearCacheFiles();
				_nodeDependencyLookupContext.CreatedCaches.Clear();
				ReloadContext();
			}

			GUILayout.EndVertical();
		}

		private void RefreshNodeStructure()
		{
			InvalidateNodeStructure();
			Refresh();
		}

		private void RefreshNodeVisualizationData()
		{
			InvalidateNodePositionData(_nodeStructure, RelationType.DEPENDENCY);
			InvalidateNodePositionData(_nodeStructure, RelationType.REFERENCER);
			
			PrepareSubTree(RelationType.DEPENDENCY);
			PrepareSubTree(RelationType.REFERENCER);
		}
		
		private void DisplayNodeDisplayOptions()
		{
			EditorGUILayout.BeginVertical("Box");

			TogglePref(_nodeDisplayOptions.ShowNodesOnce, "Show Nodes Once", b => InvalidateNodeStructure());
			TogglePref(_nodeDisplayOptions.ShowHierarchyOnce, "Show Hierarchy Once", b => InvalidateNodeStructure());
			TogglePref(_nodeDisplayOptions.DrawReferencerNodes, "Show Referencers", b => InvalidateNodeStructure());
			TogglePref(_nodeDisplayOptions.ShowPropertyPathes, "Show Property Pathes", b => InvalidateNodeStructure());
			TogglePref(_nodeDisplayOptions.AlignNodes, "Align Nodes", b => InvalidateTreeVisualization());
			TogglePref(_nodeDisplayOptions.HideFilteredNodes, "Hide Filtered Nodes", b => InvalidateNodeStructure());
			
			TogglePref(DisplayData.HighlightPackagedAssets, "Highlight packaged assets", b => InvalidateNodeStructure());
			TogglePref(MergeRelations, "Merge Relations", b => InvalidateNodeStructure());
			
			EditorGUILayout.EndVertical();
		}

		public static void TogglePref(PrefValue<bool> pref, string label, Action<bool> onChange = null)
		{
			pref.DirtyOnChange(EditorGUILayout.ToggleLeft(label, pref, GUILayout.Width(250)), onChange);
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
				var cacheState = new CacheState(cache);

				List<Type> resolverTypes = NodeDependencyLookupUtility.GetTypesForBaseType(cache.GetResolverType());
				
				foreach (Type rtype in resolverTypes)
				{
					IDependencyResolver dependencyResolver = NodeDependencyLookupUtility.InstantiateClass<IDependencyResolver>(rtype);
					cacheState.ResolverStates.Add(new ResolverState(dependencyResolver));
				}
				
				_cacheStates.Add(cacheState);
			}
		}

		private void CreateNodeHandlers()
		{
			_nodeHandlerStates.Clear();

			List<Type> types = NodeDependencyLookupUtility.GetTypesForBaseType(typeof(INodeHandler));

			foreach (Type type in types)
			{
				INodeHandler nodeHandler = NodeDependencyLookupUtility.InstantiateClass<INodeHandler>(type);
				_nodeHandlerStates.Add(new NodeHandlerState(nodeHandler));
			}
		}

		private INodeHandler GetNodeHandlerForType(string type)
		{
			foreach (NodeHandlerState state in _nodeHandlerStates)
			{
				if (state.NodeHandler.GetHandledNodeTypes().Contains(type))
				{
					return state.NodeHandler;
				}
			}

			return null;
		}

		private Dictionary<string, INodeHandler> GetNodeHandlerLookup()
		{
			Dictionary<string, INodeHandler> result = new Dictionary<string, INodeHandler>();

			foreach (NodeHandlerState state in _nodeHandlerStates)
			{
				foreach (string handledType in state.NodeHandler.GetHandledNodeTypes())
				{
					result[handledType] = state.NodeHandler;
				}
			}
			
			return result;
		}
		
		private ITypeHandler GetTypeHandlerForType(string type)
		{
			foreach (ITypeHandler state in _typeHandlers)
			{
				if (state.GetHandledType() == type)
				{
					return state;
				}
			}

			return null;
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

		private void DisplayCaches()
		{
			EditorGUILayout.BeginVertical("Box");
			
			m_cachesScrollPosition = EditorGUILayout.BeginScrollView(m_cachesScrollPosition, GUILayout.Width(280));
			Color origColor = GUI.contentColor;
			
			bool stateChanged = false;
			bool connectionTypeChanged = false;

			foreach (CacheState cacheState in _cacheStates)
			{
				GUI.contentColor = origColor;
				
				string s = cacheState.Cache.GetType().Name;
				
				ChangeValue(ref cacheState.IsActive, EditorGUILayout.ToggleLeft(s, cacheState.IsActive), ref stateChanged);

				if (cacheState.IsActive)
				{
					foreach (ResolverState state in cacheState.ResolverStates)
					{
						GUI.contentColor = origColor;
					
						EditorGUI.indentLevel = 1;
					
						string id = state.Resolver.GetId();

						ChangeValue(ref state.IsActive, EditorGUILayout.ToggleLeft(id, state.IsActive), ref stateChanged);

						if (!state.IsActive)
							continue;
					
						EditorGUI.indentLevel = 2;

						string[] connectionTypes = state.Resolver.GetConnectionTypes();

						foreach (string connectionType in connectionTypes)
						{
							GUI.contentColor = state.Resolver.GetDependencyTypeForId(connectionType).Colour;
							bool isActive = state.ActiveConnectionTypes.Contains(connectionType);
							bool newIsActive = isActive;
							
							ChangeValue(ref newIsActive, EditorGUILayout.ToggleLeft(connectionType, isActive), ref connectionTypeChanged);

							if (newIsActive && !isActive)
							{
								state.ActiveConnectionTypes.Add(connectionType);
							}
							else if (isActive && !newIsActive)
							{
								state.ActiveConnectionTypes.Remove(connectionType);
							}
						}
					}
				}
				
				if (stateChanged || connectionTypeChanged)
				{
					cacheState.SaveState();
					ReloadContext(stateChanged);
					InvalidateNodeStructure();
				}
				
				EditorGUI.indentLevel = 0;
			}

			GUI.contentColor = origColor;

			EditorGUILayout.Space(10);
			EditorGUILayout.EndScrollView();
			EditorGUILayout.EndVertical();
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
			EditorGUILayout.BeginVertical("Box");
			
			TogglePref(DisplayData.ShowAdditionalInformation, "Show Size Information", b => RefreshNodeStructure());
			TogglePref(Showthumbnails, "Show thumbnails", b => RefreshNodeStructure());

			EditorGUILayout.Space();

			EditorGUILayout.EndVertical();
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
			ChangeSelection(_selectedId, _selectedType);
		}

		private void PrepareDrawTree(Node rootNode)
		{
			if (_nodeStructureDirty || _nodeStructure == null)
			{
				EditorUtility.DisplayProgressBar("Updating tree", "Updating tree", 0.0f);

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

			if (_selectedId == null)
			{
				DrawNoNodeSelectedError();
				return;
			}

			Node entry = _nodeDependencyLookupContext.RelationsLookup.GetNode(_selectedId, _selectedType);

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

		private VisualizationNodeData AddNodeCacheForNode(string id, string type)
		{
			string key = NodeDependencyLookupUtility.GetNodeKey(id, type);
			
			if (!_cachedVisualizationNodeDatas.ContainsKey(key))
			{
				INodeHandler nodeHandler = GetNodeHandlerForType(type);
				ITypeHandler typeHandler = GetTypeHandlerForType(type);
				
				VisualizationNodeData data = typeHandler.CreateNodeCachedData(id);
				_nodeDependencyLookupContext.NodeHandlerLookup = GetNodeHandlerLookup();

				if (_showThumbnails || DisplayData.ShowAdditionalInformation)
				{
					data.OwnSize = NodeDependencyLookupUtility.GetNodeSize(true, false, id, type, new HashSet<string>(), _nodeDependencyLookupContext);
					data.HierarchySize = NodeDependencyLookupUtility.GetNodeSize(true, true, id, type, new HashSet<string>(), _nodeDependencyLookupContext);
				}

				data.Id = id;
				data.Type = type;

				data.NodeHandler = nodeHandler;
				data.TypeHandler = typeHandler;
				
				data.Name = typeHandler.GetName(id);
				data.IsEditorAsset = nodeHandler.IsNodeEditorOnly(id, type);
				data.IsPackedToApp = NodeDependencyLookupUtility.IsNodePackedToApp(id, type, _nodeDependencyLookupContext, new HashSet<string>());

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

			if (id != _selectedId || _undoSteps.Count == 0)
			{
				if (addUndoStep)
				{
					_undoSteps.Push(new UndoStep
					{
						Id =  id,
						Type = type
					});
				}
	
				_selectionDirty = true;
				InvalidateNodeStructure();
			}

			_selectedId = id;
			_selectedType = type;

			SetHandlerSelection();
		}

		private void SetHandlerSelection()
		{
			foreach (ITypeHandler typeHandler in _typeHandlers)
			{
				typeHandler.OnSelectAsset(_selectedId, _selectedType);
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
			
			_nodeStructure = GetVisualizationNode(rootConnection.Node.Id, rootConnection.Node.Type);

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

				if (!MergeRelations.GetValue())
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
			foreach (VisualizationNode node in visualizationNodeStack)
			{
				if (node.Key == key)
					return node;
			}

			return null;
		}

		public void CreateNodeHierarchyRec(HashSet<string> addedVisualizationNodes, Stack<VisualizationNode> visualizationNodeStack, VisualizationNode visualizationNode, Connection connection, int depth, RelationType relationType, NodeDisplayOptions nodeDisplayOptions, ref int iterations)
		{
			// prevent the tree from becoming too big
			if (iterations >= 0xFFFF)
			{
				return;
			}

			iterations++;
			
			visualizationNode.Key = NodeDependencyLookupUtility.GetNodeKey(connection.Node.Id, connection.Node.Type);
			bool containedNode = addedVisualizationNodes.Contains(visualizationNode.Key);
			
			if (depth == nodeDisplayOptions.MaxDepth || (containedNode && nodeDisplayOptions.ShowHierarchyOnce))
			{
				return;
			}

			if (!nodeDisplayOptions.ConnectionTypesToDisplay.Contains(connection.Type) && connection.Type != "Root")
			{
				return;
			}

			addedVisualizationNodes.Add(visualizationNode.Key);
			visualizationNodeStack.Push(visualizationNode);

			List<MergedNode> mergedNodes = GetMergedNodes(connection.Node.GetRelations(relationType));

			foreach (MergedNode mergedNode in mergedNodes)
			{
				Node childNode = mergedNode.Target.Node;
				string childNodeKey = NodeDependencyLookupUtility.GetNodeKey(childNode.Id, childNode.Type);

				if (addedVisualizationNodes.Contains(childNodeKey) && _nodeDisplayOptions.ShowNodesOnce)
				{
					continue;
				}

				VisualizationNode recursionNode = HasRecursion(childNodeKey, visualizationNodeStack);
				bool isRecursion = recursionNode != null;
				VisualizationNode visualizationChildNode = isRecursion ? recursionNode : GetVisualizationNode(childNode.Id, childNode.Type);

				visualizationChildNode.IsFiltered = IsNodeFiltered(childNode.Id, childNode.Type);

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

		private bool IsNodeFiltered(string id, string type)
		{
			string key = NodeDependencyLookupUtility.GetNodeKey(id, type);
			
			VisualizationNodeData nodeData = _cachedVisualizationNodeDatas[key];
			ITypeHandler typeHandler = nodeData.TypeHandler;

			return typeHandler.HasFilter() && !typeHandler.IsFiltered(nodeData.Id);
		}

		private void SortChildNodes(VisualizationNode visualizationNode, RelationType relationType)
		{
			visualizationNode.SetRelations(visualizationNode.GetRelations(relationType, true, true).OrderBy(p =>
			{
				return p.VNode.GetSortingKey(relationType);

			}).ToList(), relationType);
		}

		private VisualizationNode GetVisualizationNode(string id, string type)
		{
			return new VisualizationNode{NodeData = AddNodeCacheForNode(id, type)};
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