namespace Roslyn_Analysis.Graph
{
    public class Graph<T1, T2>
    {
        private Dictionary<string, T1> nodes = new Dictionary<string, T1>();
        private HashSet<T2> edges = new HashSet<T2>();

        private Dictionary<string, HashSet<T2>> incoming_edges = new Dictionary<string, HashSet<T2>>();
        private Dictionary<string, HashSet<T2>> outgoing_edges = new Dictionary<string, HashSet<T2>>();

        public void AddNode(string key, T1 node)
        {
            if (!nodes.ContainsKey(key))
            {
                nodes.Add(key, node);
            }
        }

        public bool AddEdge(string src, string tgt, T2 edge)
        {
            if (!nodes.ContainsKey(src) || !nodes.ContainsKey(tgt))
            {
                //Console.WriteLine("DEBUG :: ADD Edge Failed.");
                return false;
            }

            if (!outgoing_edges.ContainsKey(src))
            {
                outgoing_edges[src] = new HashSet<T2>();
            }
            outgoing_edges[src].Add(edge);
       

            if (!incoming_edges.ContainsKey(tgt))
            {
                incoming_edges[tgt] = new HashSet<T2>();
            }
            incoming_edges[tgt].Add(edge);
      
            edges.Add(edge);
            return true;
        }

        public HashSet<T2> GetOutgoingEdges(string src)
        {
            if (!outgoing_edges.ContainsKey(src))
            {
                return null;
            }
            return outgoing_edges[src];
        }
        public HashSet<T2> GetIncomingEdges(string tgt)
        {
            if (!incoming_edges.ContainsKey(tgt))
            {
                return null;
            }
            return incoming_edges[tgt];
        }

        public List<T1> GetNodes()
        {
            return nodes.Values.ToList();
        }
        public List<T1> GetNodes(string Key)
        {
            List<T1> result = new List<T1>();
            foreach(var nodeKey in nodes.Keys)
            {
                if (nodeKey.StartsWith(Key))
                {
                    result.Add(nodes[nodeKey]);
                }
            }
            return result;
        }

        public T1 GetNode(string nodeKey)
        {
            if (nodes.ContainsKey(nodeKey))
            {
                return nodes[nodeKey];
            }
            return default(T1);
        }

        public bool ContainNode(string nodeKey)
        {
            return nodes.ContainsKey(nodeKey);
        }

        public List<T2> GetEdges()
        {
            return edges.ToList();
        }
    }
}