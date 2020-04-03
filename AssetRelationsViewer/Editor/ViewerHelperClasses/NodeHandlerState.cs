using Com.Innogames.Core.Frontend.NodeDependencyLookup;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public class NodeHandlerState
    {
        public bool IsActive = true;
        public readonly INodeHandler NodeHandler;

        public NodeHandlerState(INodeHandler nodeHandler)
        {
            NodeHandler = nodeHandler;
        }
    }
}