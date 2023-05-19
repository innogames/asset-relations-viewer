﻿using System.Collections.Generic;

namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
	/// <summary>
	/// Provides additional functions for a given Nodetype, for example "Asset"
	/// </summary>
	public interface INodeHandler
	{
		// Returns which node types the handler handlers. For example "Asset"
		string GetHandledNodeType();
		// Returns the filesize if the node. In case of an asset it would be the serialized filesize
		Node.NodeSize GetOwnFileSize(Node node, NodeDependencyLookupContext stateContext);
		// Returns if a node is packed to the app or not. Helpful to find out if an asset is actually used in the final game or not
		bool IsNodePackedToApp(Node node, bool alwaysExcluded = false);
		// Returns if a node it just used within the editor. For assets this would be case if its in an editor folder
		bool IsNodeEditorOnly(string id, string type);
		// Returns if the assets contributes to the overall tree size (size of all dependencies together)
		void GetNameAndType(string id, out string name, out string type);
		long GetChangedTimeStamp(string id);
		void InitNameAndTypeInformation();
		void SaveCaches();
	}
}