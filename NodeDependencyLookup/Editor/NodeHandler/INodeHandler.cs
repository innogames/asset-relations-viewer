namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	public enum NodeSizeCalculationStep
	{
		/// <summary>
		/// Step to calculate necessary data for the ParallelThreadSave step than can not be executed in multiple threads.
		/// This can be for example calls to the AssetDatabase or any other Unity specific function
		/// </summary>
		Initial,
		/// <summary>
		/// Calculations that can be done on multiple threads. For this the implementation needs to be threadsave!
		/// For example this is currently used for calculating the estimated compressed size of file nodes.
		/// </summary>
		ParallelThreadSave,
		/// <summary>
		/// Calculations that can be done after the ParallelThreadSave step.
		/// </summary>
		Final,
	}

	/// <summary>
	/// Provides additional functions for a given Nodetype, for example "Asset"
	/// </summary>
	public interface INodeHandler
	{
		// Returns which node types the handler handlers. For example "Asset"
		string GetHandledNodeType();
		// Updates the filesize of the node. In case of an asset it would be the serialized filesize
		void CalculateOwnFileSize(Node node, NodeDependencyLookupContext stateContext, NodeSizeCalculationStep step);
		// Returns if a node is packed to the app or not. Helpful to find out if an asset is actually used in the final game or not
		bool IsNodePackedToApp(Node node, bool alwaysExcluded = false);
		// Returns if a node it just used within the editor. For assets this would be case if its in an editor folder
		bool IsNodeEditorOnly(string id, string type);
		Node CreateNode(string id, string type, bool update, out bool wasCached);
		void InitNodeDataInformation();
		void SaveCaches();
	}
}