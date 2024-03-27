using System.Collections.Generic;
using System.Threading;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
	/// <summary>
	/// Thread to calculate the hierarchy size of nodes in the background.
	/// The reason for this is that calculating the treesize can get very slow if the tree depth is very high.
	/// For example in the case of bidirectional AddressableAssetGroup->AddressableAssetGroup dependencies.
	/// </summary>
	public class NodeSizeThread
	{
		private readonly Stack<VisualizationNodeData> _stack = new Stack<VisualizationNodeData>();
		private Thread _thread;
		private NodeDependencyLookupContext _context;

		public NodeSizeThread(NodeDependencyLookupContext context)
		{
			_context = context;
		}

		public void Start()
		{
			_thread = new Thread(ThreadProc)
			{
				Name = "HierarchySizeThread"
			};
			_thread.Start();
		}

		public void Kill()
		{
			_thread?.Abort();
			_stack.Clear();
		}

		public void EnqueueNodeData(VisualizationNodeData nodeData)
		{
			_stack.Push(nodeData);
		}

		private void ThreadProc()
		{
			var flattedHierarchy = new HashSet<Node>();

			while (true)
			{
				while (_stack.Count > 0)
				{
					var visualizationNodeData = _stack.Pop();
					if (visualizationNodeData != null)
					{
						visualizationNodeData.HierarchySize =
							NodeDependencyLookupUtility.GetTreeSize(visualizationNodeData.Node, _context,
								flattedHierarchy);
					}
				}

				Thread.Sleep(5);
			}
		}
	}
}