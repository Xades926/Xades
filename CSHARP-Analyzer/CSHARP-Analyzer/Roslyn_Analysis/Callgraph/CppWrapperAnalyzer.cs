using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;
using Roslyn_Analysis;
using Roslyn_Analysis.DataFlowAnalysis;
using Roslyn_Analysis.Graph;
using Roslyn_Analysis.TaintAnalysis;
using System.Dynamic;

namespace Roslyn_Analysis.Callgraph
{
    public class CppWrapper
    {
        public CppWrapper(string lib, string func)
        {
            this.libName = lib;
            this.functionName = func;
        }
        public string libName = "";
        public string functionName = "";
    }
    public class CppWrapperAnalyzer
    {
        public static Dictionary<ISymbol, CppWrapper> CppWrapper_Edges = new Dictionary<ISymbol, CppWrapper>();
        public static List<CallgraphEdge> edges = new List<CallgraphEdge>();
        public static HashSet<string > nodes = new HashSet<string>();
        public static void Add_CppWrapper(ISymbol srcSymbol,  CppWrapper cppWrapper)
        {
            if (!CppWrapper_Edges.ContainsKey(srcSymbol)){
                CppWrapper_Edges.Add(srcSymbol, cppWrapper);
            }
        }
        public CppWrapperAnalyzer() { }

        public static void Add_CppWrapperEdge(Graph<CallgraphNode, CallgraphEdge> cg)
        {
            foreach(IMethodSymbol originSymbol in CppWrapperAnalyzer.CppWrapper_Edges.Keys)
            {
                var tgtWrapper = CppWrapperAnalyzer.CppWrapper_Edges[originSymbol];

                string srcNodeKey = TaintAnalyzer.GetCallgraphKey(originSymbol);
                CallgraphNode srcNode = cg.GetNode(srcNodeKey);
                srcNode.IsWrapper = true;
                srcNode.isSource = true;
                srcNode.isSink = true;
                srcNode.IsCpp = true;

                CallgraphNode tgtNode = new CallgraphNode();
                tgtNode.Clazz = tgtWrapper.libName;
                tgtNode.Function = tgtWrapper.functionName;
                tgtNode.parameters = "[]";
                tgtNode.ReturnType = "";
                tgtNode.IsCpp = true;
                tgtNode.IsWrapper = true;
         
                string tgtNodeKey = tgtNode.GetKey();
                cg.AddNode(tgtNodeKey, tgtNode);


                if (!DataFlowAnalyzer.Is_XamarinAPI(originSymbol.ContainingSymbol.ToString()))
                {
                    tgtNode.isSource = true;
                    tgtNode.isSink = true;

                    TaintAnalyzer.Sources.Add(srcNodeKey);
                    TaintAnalyzer.Sources.Add(tgtNodeKey);

                    TaintAnalyzer.Sinks.Add(srcNodeKey);
                    TaintAnalyzer.Sinks.Add(tgtNodeKey);


                    Program.cppCustom++;
                    //  Console.WriteLine($"{srcNodeKey} -> {tgtNodeKey}");
                }


                CallgraphEdge edge = new CallgraphEdge();
                edge.src = srcNode;
                edge.tgt = tgtNode;

                cg.AddEdge(srcNodeKey, tgtNodeKey, edge);
                CppWrapperAnalyzer.nodes.Add(tgtNode.GetKey());
                CppWrapperAnalyzer.edges.Add(edge);



            }
        }
        public static JObject CppWrapper2Json()
        {
            JArray csharp2cpp_edges = new JArray();
            foreach (CallgraphEdge edge in CppWrapperAnalyzer.edges)
            {
                JObject edgeUnit = new JObject(
                    new JProperty("srcClass", edge.src.Clazz),
                    new JProperty("srcFunc", edge.src.Function),
                    new JProperty("srcSignature", edge.src.GetSignature()),
                    new JProperty("tgtClass", edge.tgt.Clazz),
                    new JProperty("tgtFunc", edge.tgt.Function),
                    new JProperty("tgtSignature", edge.tgt.GetSignature())
                );
                csharp2cpp_edges.Add(edgeUnit);
            }
            return new JObject(new JProperty("edges", csharp2cpp_edges));
        }
    }
}