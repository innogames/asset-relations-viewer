using Com.Innogames.Core.Frontend.NodeDependencyLookup;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	/// <summary>
	/// The typeHandler interface contains the functionality for a specific Node type
	/// </summary>
	public interface ITypeHandler
	{
		string GetHandledType();
		string GetSortingKey(string name);
		void ApplyFilterString(string filterString);
		bool IsFiltered(string id, string nameFilter, string typeFilter);
		VisualizationNodeData CreateNodeCachedData(string id);
		void SelectInEditor(string id);
		void OnGui();
		void OnSelectAsset(string id, string type);
		void InitContext(NodeDependencyLookupContext nodeDependencyLookupContext, AssetRelationsViewerWindow viewerWindow, INodeHandler nodeHandler);
		bool HandlesCurrentNode();
	}
}