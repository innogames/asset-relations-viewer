using System.Collections.Generic;
using System.IO;
using Com.Innogames.Core.Frontend.NodeDependencyLookup;
using UnityEditor;

namespace Com.Innogames.Core.Frontend.AssetRelationsViewer
{
    public class NodeDataCache
    {
        public class Entry
        {
            public long TimeStamp;
            public string Name;
            public string Type;
            public int Size;
        }
        
        private const string Version = "1.40";
        private const string FileName = "AssetDependencyNodeCacheData" + Version + ".cache";

        private readonly Dictionary<string, Entry> _nodeDataLookup = new Dictionary<string, Entry>();
        private Dictionary<string, INodeHandler> _nodeHandlerLookup = new Dictionary<string, INodeHandler>();
        
        public void Initialize(Dictionary<string, INodeHandler> nodeHandlerLookup)
        {
            _nodeHandlerLookup = nodeHandlerLookup;
        }

        public void LoadCache(string directory)
        {
            string path = Path.Combine(directory, FileName);
                
            if (File.Exists(path))
            {
                byte[] bytes = File.ReadAllBytes(path);
                Deserialize(bytes);
            }
        }

        public void Update(List<Node> nodes, bool updateCacheOnTimeStampChange)
        {
            bool isEmpty = _nodeDataLookup.Count == 0;

            for (var i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                Entry entry;
                long lastTimeStamp = -1;
                INodeHandler nodeHandler = _nodeHandlerLookup[node.Type];
                bool isNew = true;

                if (!isEmpty && _nodeDataLookup.TryGetValue(node.Key, out entry))
                {
                    lastTimeStamp = entry.TimeStamp;
                    isNew = false;
                }
                else
                {
                    entry = new Entry();
                }

                if (!isNew && !updateCacheOnTimeStampChange)
                {
                    return;
                }
                
                long currentTimeStamp = nodeHandler.GetChangedTimeStamp(node.Id);

                if (currentTimeStamp != lastTimeStamp || currentTimeStamp == -1)
                {
                    nodeHandler.GetNameAndType(node.Id, out string nodeName, out string nodeType);
                    entry.Name = nodeName; 
                    entry.Type = nodeType;
                    entry.TimeStamp = currentTimeStamp;
                }

                if (isNew)
                {
                    _nodeDataLookup.Add(node.Key, entry);
                }
                
                if (i % 100 == 0)
                {
                    EditorUtility.DisplayProgressBar("Getting node search information", entry.Name, (float)i / nodes.Count);
                }
            }
        }

        public void SaveCache(string directory = NodeDependencyLookupUtility.DEFAULT_CACHE_SAVE_PATH)
        {
            string path = Path.Combine(directory, FileName);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
			
            File.WriteAllBytes(path, Serialize());
        }

        public Entry GetEntryForId(string key)
        {
            if (_nodeDataLookup.TryGetValue(key, out Entry entry))
            {
                return entry;
            }
            
            return null;
        }

        private byte[] Serialize()
        {
            int offset = 0;
            byte[] bytes = new byte[32*1024];

            CacheSerializerUtils.EncodeLong(_nodeDataLookup.Count, ref bytes, ref offset);
            
            foreach (KeyValuePair<string,Entry> pair in _nodeDataLookup)
            {
                Entry entry = pair.Value;
                CacheSerializerUtils.EncodeString(pair.Key, ref bytes, ref offset);
                CacheSerializerUtils.EncodeLong(entry.TimeStamp, ref bytes, ref offset);
                CacheSerializerUtils.EncodeLong(entry.Size, ref bytes, ref offset);
                CacheSerializerUtils.EncodeString(entry.Name, ref bytes, ref offset);
                CacheSerializerUtils.EncodeString(entry.Type, ref bytes, ref offset);
                
                bytes = CacheSerializerUtils.EnsureSize(bytes, offset);
            }

            return bytes;
        }

        private void Deserialize(byte[] bytes)
        {
            int offset = 0;
            long arraySize = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);

            _nodeDataLookup.Clear();

            for (int i = 0; i < arraySize; ++i)
            {
                string key = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
                long timeStamp = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);
                long size = CacheSerializerUtils.DecodeLong(ref bytes, ref offset);
                string name = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
                string type = CacheSerializerUtils.DecodeString(ref bytes, ref offset);
                Entry newEntry = new Entry {TimeStamp = timeStamp, Name = name, Type = type, Size = (int) size};
                
                _nodeDataLookup.Add(key, newEntry);
            }
        }
    }
}