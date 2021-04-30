namespace Com.Innogames.Core.Frontend.NodeDependencyLookup
{
    public class FileNode : IResolvedNode
    {
        public string FileId;

        public string Id => FileId;
        public string Type => "File";
        public bool Existing => true;
    }
}