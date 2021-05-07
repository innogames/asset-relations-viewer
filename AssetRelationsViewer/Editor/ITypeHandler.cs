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

		bool HasFilter();
		bool IsFiltered(string id);
			
		string GetName(string id);
		VisualizationNodeData CreateNodeCachedData(string id);

		void SelectInEditor(string id);

		void OnGui();

		void OnSelectAsset(string id, string type);
		
		void InitContext(NodeDependencyLookupContext nodeDependencyLookupContext, AssetRelationsViewerWindow viewerWindow);

		bool HandlesCurrentNode();
	}
}