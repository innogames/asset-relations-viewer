using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Provides additional functions for a given Nodetype, for example "Asset"
	/// </summary>
	public interface INodeHandler
	{
		/// Returns which node types the handler handlers. For example "Asset"
		string GetHandledNodeType();

		/// Initialized the filesize calculation of the node. Since the <see cref="CalculateOwnFileSize"/> function is running on multiple threads do calls to Unity functions here.
		void InitializeOwnFileSize(Node node, NodeDependencyLookupContext context, bool updateNodeData);

		/// Updates the filesize of the node. This is running in parallel on multiple threads
		void CalculateOwnFileSize(Node node, NodeDependencyLookupContext context, bool updateNodeData);

		/// After <see cref="CalculateOwnFileSize"/> has been run do any node dependency related filesize calculations here
		void CalculateOwnFileDependencies(Node node, NodeDependencyLookupContext context, HashSet<Node> calculatedNodes);

		/// Returns if a node is packed to the app or not. Helpful to find out if an asset is actually used in the final game or not
		bool IsNodePackedToApp(Node node, bool alwaysExcluded = false);

		/// Returns if a node it just used within the editor. For assets this would be case if its in an editor folder
		bool IsNodeEditorOnly(string id, string type);

		/// Is run before any Node in <see cref="CreateNode"/> is created. Use this to load any caches, create mappings, etc.
		void InitNodeCreation();

		/// Creates a node and will return it. Each implementation if the <see cref="INodeHandler"/> Is responsible for creating a node and using cached data for speed up.
		Node CreateNode(string id, string type, bool update, out bool wasCached);

		/// Safe any caches that we used for speeding up the Node creation.
		void SaveCaches();
	}
}