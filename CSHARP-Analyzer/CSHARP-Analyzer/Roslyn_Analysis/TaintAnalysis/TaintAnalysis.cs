using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.VisualBasic;
using Newtonsoft.Json.Linq;
using Roslyn_Analysis.Callgraph;
using Roslyn_Analysis.DataFlowAnalysis;
using Roslyn_Analysis.Graph;
using Roslyn_Analysis.Jimple;
using Roslyn_Analysis.Soot;
using System;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;

namespace Roslyn_Analysis.TaintAnalysis
{
    public class TaintAnalyzer
    { 
        public int AcrivityCnt = 0;
        public static List<TaintResult> sourceCalls = new List<TaintResult>();
        public static List<TaintResult> sinkCalls = new List<TaintResult>();
        public static List<TaintResult> bothCalls = new List<TaintResult>();

        public static List<TaintResult> Leaks = new List<TaintResult>();


        public static Graph<CallgraphNode, CallgraphEdge> taintCallgraph = new Graph<CallgraphNode, CallgraphEdge>();

        public static HashSet<string> Sources = new HashSet<string>();
        public static HashSet<string> Sinks = new HashSet<string>();

        private HashSet<string> found_sources = new HashSet<string>();
        private HashSet<string> found_sinks = new HashSet<string>();

        private Dictionary<string, int> VisitCount = new Dictionary<string, int>();
        private HashSet<String> targets = new HashSet<string>();
        public TaintAnalyzer(){}
        public void Analyze()
        {
            Console.WriteLine("\nStarting Taint-Analysis.");

            AddSourcePath();
            AddSinkPath();
            Write_CG_JSON();

            foreach (var dic in VisitCount)
            {
                if (dic.Value > 1 && taintCallgraph.GetIncomingEdges(dic.Key) == null)
                {
                    targets.Add(dic.Key);
                }
            }
            //Write_CG_JSON();      
            
            Dictionary<string, SootClass> SootClasses = new Dictionary<string, SootClass>();
            foreach(string target in targets)
            {
                CallgraphNode targetNode = CallGraph.callgraph.GetNode(target);

                MethodDeclarationSyntax targetMethod = CallGraph.callgraph.GetNode(target).syntax as MethodDeclarationSyntax;
                HashSet<DataFlowAnalysisValue> parameters = new HashSet<DataFlowAnalysisValue>();

                if (targetMethod != null && !DataFlowAnalyzer.Is_XamarinAPI(target) && !targetNode.IsWrapper)
                {
                    Stack<string> stackTrace = new Stack<string>();
                    stackTrace.Push(target);
                    DataFlowAnalyzer dataFlowAnalyzer = new DataFlowAnalyzer(stackTrace, targetMethod, parameters);
                    var returnValue = dataFlowAnalyzer.AnalyzeMethod();
                    stackTrace.Clear();
                    DataFlowAnalysisOperationResult result = dataFlowAnalyzer.GetResult();

                    List<DataFlowAnalysisValue> leaks = CollectLeaks(result);
                   
                    SootClass res = Convert_Jimple(targetNode, leaks);
                  
                    if (res.HasStmts())
                    {
                        if (SootClasses.ContainsKey(res.clazz))
                        {
                            if (SootClasses.TryGetValue(res.clazz, out SootClass find))
                            {
                                find.JoinSootClass(res);
                            }
                        }
                        else
                        {
                            SootClasses.Add(res.clazz, res);
                        }
                    }
                }
                else
                {
                //    Console.WriteLine("Debug: is not activity.");
                }
                
            }

            JArray classes = new JArray();
            foreach (SootClass sootClass in SootClasses.Values)
            {
                if(sootClass.HasStmts())
                    classes.Add(sootClass.ToJson());
            }


            var jsonPath = Path.GetFullPath($"{Program.outPath}\\csharp2jimple.json");
            Console.WriteLine($"Writing Jimple IR Json file {jsonPath}.");
            File.WriteAllText(jsonPath, classes.ToString());

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Finished Taint-Analysis.");
            Console.ResetColor();

            Print_Results();
            Write_Results2Json();
        }
        private void Print_Results()
        {
            Console.WriteLine($" Taint-Analysis Result: Xamarin.Android Activity = {AcrivityCnt}");
            Console.WriteLine($" Taint-Analysis Result: Found Source count = {found_sources.Count}");
            foreach(string a in found_sources)
            {
                Console.Write($"  {a}");
            }
            Console.Write($"\n");
            Console.WriteLine($" Taint-Analysis Result: Found Sink count = {found_sinks.Count}");
            foreach (string a in found_sinks)
            {
                Console.Write($"  {a}");
            }
            Console.Write($"\n");
            int javaCnt = 0;
            int cppCnt = 0;
            int csharpCnt = 0;
            Console.WriteLine("============== Found SOURCE ==============");
            foreach (TaintResult source in sourceCalls)
            {
                var calleeNode = TaintAnalyzer.taintCallgraph.GetNode(TaintAnalyzer.GetCallgraphKey(source.calleeSymbol as IMethodSymbol));
                if (calleeNode.IsCpp)
                {
                    source.Print("Source", "Cpp");
                    cppCnt++;
                }
                else if (calleeNode.IsJava)
                {
                    source.Print("Source", "Java");
                    javaCnt++;
                }
                else
                {
                    source.Print("Source", "Csharp");
                    csharpCnt++;
                }
            }
            Console.WriteLine($"  Taint-Analysis Result: Found Sources to Csharp = {csharpCnt}");
            Console.WriteLine($"  Taint-Analysis Result: Found Sources to Java = {javaCnt}");
            Console.WriteLine($"  Taint-Analysis Result: Found Sources to Cpp = {cppCnt}\n");


            javaCnt = 0;
            cppCnt = 0;
            csharpCnt = 0;
            Console.WriteLine("============== Found SINK ==============");
            foreach (TaintResult sink in sinkCalls)
            {
                var calleeNode = TaintAnalyzer.taintCallgraph.GetNode(TaintAnalyzer.GetCallgraphKey(sink.calleeSymbol as IMethodSymbol));
                if (calleeNode.IsCpp)
                {
                    sink.Print("Sink", "Cpp");
                    cppCnt++;
                }
                else if (calleeNode.IsJava)
                {
                    sink.Print("Sink", "Java");
                    javaCnt++;
                }
                else
                {
                    sink.Print("Sink", "Csharp");
                    csharpCnt++;
                }

            }
            Console.WriteLine($"  Taint-Analysis Result: Found Sinks to Csharp = {csharpCnt}");
            Console.WriteLine($"  Taint-Analysis Result: Found Sinks to Java = {javaCnt}");
            Console.WriteLine($"  Taint-Analysis Result: Found Sinks to Cpp = {cppCnt}\n");

            javaCnt = 0;
            cppCnt = 0;
            csharpCnt = 0;
            Console.WriteLine("============== Found BOTH ==============");
            foreach (TaintResult both in bothCalls)
            {
                var calleeNode = TaintAnalyzer.taintCallgraph.GetNode(TaintAnalyzer.GetCallgraphKey(both.calleeSymbol as IMethodSymbol));
                if (calleeNode.IsCpp)
                {
                    both.Print("Both", "Cpp");
                    cppCnt++;
                }
                else if (calleeNode.IsJava)
                {
                    both.Print("Both", "Java");
                    javaCnt++;
                }
                else
                {
                    both.Print("Both", "Csharp");
                    csharpCnt++;
                }
            }
            Console.WriteLine($"  Taint-Analysis Result: Found Boths to Csharp = {csharpCnt}");
            Console.WriteLine($"  Taint-Analysis Result: Found Boths to Java = {javaCnt}");
            Console.WriteLine($"  Taint-Analysis Result: Found Boths to Cpp = {cppCnt}\n");


            javaCnt = 0;
            cppCnt = 0;
            csharpCnt= 0;
            Console.WriteLine("============== Found LEAK ==============");
            foreach (TaintResult leak in Leaks)
            {
                
                var calleeNode = TaintAnalyzer.taintCallgraph.GetNode(TaintAnalyzer.GetCallgraphKey(leak.calleeSymbol as IMethodSymbol));
                if (calleeNode.IsCpp)
                {
                    leak.PrintLeak("Cpp");
                    cppCnt++;
                }
                else if (calleeNode.IsJava)
                {
                    leak.PrintLeak("Java");
                    javaCnt++;
                }
                else
                {
                    leak.PrintLeak("Csharp");
                    csharpCnt++;
                }
            }
            Console.WriteLine($"  Taint-Analysis Result: Found Leaks to Csharp = {csharpCnt}");
            Console.WriteLine($"  Taint-Analysis Result: Found Leaks to Java = {javaCnt}");
            Console.WriteLine($"  Taint-Analysis Result: Found Leaks to Cpp = {cppCnt}\n");
        }

        private void Write_Results2Json()
        {
          
            JObject results = new JObject();

            JArray sourceArray = new JArray(); 
            foreach (TaintResult source in sourceCalls)
            {
                sourceArray.Add(source.ToJson());
            }

            JArray sinkArray = new JArray();
            foreach (TaintResult sink in sinkCalls)
            {
                sinkArray.Add(sink.ToJson());
            }

            JArray bothArray = new JArray();
            foreach (TaintResult both in bothCalls)
            {
                bothArray.Add(both.ToJson());
            }
            results.Add("source", sourceArray);
            results.Add("sink", sinkArray);
            results.Add("both", bothArray);
            var resultsPath = Path.GetFullPath($"{Program.outPath}\\results.json");
            File.WriteAllText(resultsPath, results.ToString());

            // Write leaks

            JArray leakArray = new JArray();
            foreach (TaintResult leak in Leaks)
            {
                leakArray.Add(leak.ToLeakJson());
            }
            var leaksPath = Path.GetFullPath($"{Program.outPath}\\leaks.json");
            File.WriteAllText(leaksPath, leakArray.ToString());

        }
        private SootClass Convert_Jimple(CallgraphNode srcNode, List<DataFlowAnalysisValue> leaks)
        {
            SootClass sootClass = new SootClass(srcNode.Clazz);
            SootMethod srcMethod = new SootMethod(srcNode.Function, srcNode.GetSignature(), SootMethod.GetSootModifier(srcNode));

            sootClass.AddSootMethod(srcMethod);
            JimpleBody activeBody = srcMethod.ActiveBody;

            foreach (DataFlowAnalysisValue leak in leaks)
            {
                activeBody.Extract_JimpleUnits(leak);
            }

            return sootClass;
        }

        public void AddSourcePath()
        {
            HashSet<string> visited = new HashSet<string>();
            foreach (var source in TaintAnalyzer.Sources)
            {
                Queue<CallgraphNode> queue = new Queue<CallgraphNode>();
                List<CallgraphNode> findNodes = null;
                if (source.StartsWith("#"))
                {
                    findNodes = CallGraph.callgraph.GetNodes(source.Substring(1));
                }
                else
                {
                    var tmp = CallGraph.callgraph.GetNode(source);
                    findNodes = new List<CallgraphNode>();
                    if(tmp != null)
                        findNodes.Add(tmp);
                }

                if (findNodes.Count > 0)
                {
                    foreach (var node in findNodes)
                    {
                        found_sources.Add(node.GetKey());
                        node.isSource = true;
                        taintCallgraph.AddNode(node.GetKey(), node);
                        queue.Enqueue(node);
                    }
                }

                CallgraphNode nowNode = null;

                while (queue.Count > 0)
                {
                    nowNode = queue.First();
                    queue.Dequeue();
                    visited.Add(nowNode.GetKey());

                    var incomingEdges = CallGraph.callgraph.GetIncomingEdges(nowNode.GetKey());
                    if (incomingEdges != null)
                    {
                        foreach (CallgraphEdge edge in incomingEdges)
                        {
                            CallgraphNode src = edge.src;
                                                        
                            if (!src.GetKey().Contains("Java.Interop"))
                            {
                                if (src.IsWrapper)
                                {
                                    src.isSource = true;
                                }
                                taintCallgraph.AddNode(src.GetKey(), src);
                                taintCallgraph.AddEdge(src.GetKey(), edge.tgt.GetKey(), edge);
                                if(!visited.Contains(src.GetKey()))
                                    queue.Enqueue(src);
                            }
                            if (VisitCount.ContainsKey(src.GetKey()))
                            {
                                VisitCount[src.GetKey()] += 1;
                            }
                            else
                            {
                                VisitCount[src.GetKey()] = 1;
                            }
                        }
                    }
                
                }
            }
        }
        public void AddSinkPath()
        {
            HashSet<string> visited = new HashSet<string>();
            foreach (var sink in TaintAnalyzer.Sinks)
            {
                Queue<CallgraphNode> queue = new Queue<CallgraphNode>();

                List<CallgraphNode> findNodes = null;
                if (sink.StartsWith("#"))
                {
                    findNodes = CallGraph.callgraph.GetNodes(sink.Substring(1));
                }
                else
                {
                    var tmp = CallGraph.callgraph.GetNode(sink);
                    findNodes = new List<CallgraphNode>();
                    if (tmp != null)
                        findNodes.Add(tmp);
                }
          
                if (findNodes.Count > 0)
                {
                    foreach(var node in findNodes)
                    {   
                        found_sinks.Add(node.GetKey());
               
                        node.isSink = true;
                        taintCallgraph.AddNode(node.GetKey(), node);
                        queue.Enqueue(node);
                    }
                }
                CallgraphNode nowNode = null;
                while (queue.Count > 0)
                {
                    nowNode = queue.First();
                    queue.Dequeue();
                    visited.Add(nowNode.GetKey());

                    var incomingEdges = CallGraph.callgraph.GetIncomingEdges(nowNode.GetKey());
                    if (incomingEdges != null)
                    {
                        foreach (var edge in incomingEdges)
                        {
                            var src = edge.src;
                         
                            if (!src.GetKey().Contains("Java.Interop"))
                            {
                                if (src.IsWrapper) {                                 
                                    src.isSink = true;
                                }
                                taintCallgraph.AddNode(src.GetKey(), src);
                                taintCallgraph.AddEdge(src.GetKey(), edge.tgt.GetKey(), edge);
                                if(!visited.Contains(src.GetKey()))
                                    queue.Enqueue(src);
                            }

                            if (VisitCount.ContainsKey(src.GetKey()))
                            {
                                VisitCount[src.GetKey()] += 1;
                            }
                            else
                            {
                                VisitCount[src.GetKey()] = 1;
                            }
                        }
                    } 
                }
            }   
        }
        private List<DataFlowAnalysisValue> CollectLeaks(DataFlowAnalysisOperationResult res)
        {
               
            List<DataFlowAnalysisValue> leaks = new List<DataFlowAnalysisValue>();
            var lastOperationResult = res;

            foreach (var key in lastOperationResult.dataValueMap.Keys)
            {
                var data = lastOperationResult.dataValueMap[key];
                if (data.IsInvocation == true)
                {
                    if (IsLeak(data))
                    {
                        leaks.Add(data);
                        Leaks.Add(new TaintResult(data.TargetSymbol, data.Operation, data));
                        
                    }
                }
            }
            return leaks;
        }
        private bool IsLeak(DataFlowAnalysisValue val)
        {
            if (TaintAnalyzer.IsSink(val.TargetSymbol))
            {
                if (TaintAnalyzer.IsSource(val.TargetSymbol))
                {
                    return true;
                }
                else
                {
                    HashSet<DataFlowAnalysisValue> parameters = val.Parameters;
                    foreach (DataFlowAnalysisValue param in parameters)
                    {
                        if (param.IsTainted == true)
                            return true;
                    }
                }
            }
            return false;
        }
        public static bool IsSource(IMethodSymbol methodSymbol)
        {
            if (methodSymbol != null)
            {
                var method = TaintAnalyzer.taintCallgraph.GetNode(TaintAnalyzer.GetCallgraphKey(methodSymbol));
                if(method != null) 
                    return method.isSource;
            }
            return false;
        }
        public static bool IsSink(IMethodSymbol methodSymbol)
        {
            if (methodSymbol != null)
            {
                var method = TaintAnalyzer.taintCallgraph.GetNode(TaintAnalyzer.GetCallgraphKey(methodSymbol));
                if(method != null) return method.isSink;
            }
            return false;
        }
        public static bool InitializeSourcesAndSinks(string configurePath)
        {
            try
            {
                string csharp_sourcesinkPath = $"{configurePath}\\Csharp_SourcesAndSinks.txt";

                Console.WriteLine($"Initailize Java_SourcesAndSinks from \'{csharp_sourcesinkPath}\'...");
                string[] lines = System.IO.File.ReadAllLines(csharp_sourcesinkPath);
                string[] separatingStrings1 = { "->" };
                foreach (string line in lines)
                {
                    if (!line.StartsWith("%") && line.Length > 0)
                    {
                        string[] parts = line.Split(separatingStrings1, StringSplitOptions.RemoveEmptyEntries);

                        string kind = parts[1].Trim();  // _SOURCE_, _SINK_, _BOTH_
                        string signature = parts[0].Trim();

                        if (kind.Equals("_SOURCE_"))
                        {
                            TaintAnalyzer.Sources.Add(signature);
                        }
                        if (kind.Equals("_SINK_"))
                        {
                            TaintAnalyzer.Sinks.Add(signature);
                        }
                        if (kind.Equals("_BOTH_"))
                        {
                            TaintAnalyzer.Sources.Add(signature);
                            TaintAnalyzer.Sinks.Add(signature);
                        }
                    }

                }
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Finishied Initialization C# {TaintAnalyzer.Sources.Count} Soruces And {TaintAnalyzer.Sinks.Count} Sinks.\n");
                Console.ResetColor();

                string java_sourcesinkPath = $"{configurePath}\\Java_SourcesAndSinks.txt";
                Console.WriteLine($"Initailize Java_SourcesAndSinks from \'{java_sourcesinkPath}\'... ");


                lines = System.IO.File.ReadAllLines(java_sourcesinkPath);
                string[] separatingStrings2 = { "->", "<", ">" };
                foreach (string line in lines)
                {

                    if (!line.StartsWith("%") && line.Length > 0)
                    {
                        string[] parts = line.Split(separatingStrings2, StringSplitOptions.RemoveEmptyEntries);

                        string kind = parts[2].Trim();  // _SOURCE_, _SINK_, _BOTH_
                        string signature = Convert2JavaSignature(parts[0]);

                        if (kind.Equals("_SOURCE_"))
                        {
                            TaintAnalyzer.Sources.Add(signature);
                        }
                        if (kind.Equals("_SINK_"))
                        {
                            TaintAnalyzer.Sinks.Add(signature);
                        }
                        if (kind.Equals("_BOTH_"))
                        {
                            TaintAnalyzer.Sources.Add(signature);
                            TaintAnalyzer.Sinks.Add(signature);
                        }
                    }

                }
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Finishied Initialization Java {TaintAnalyzer.Sources.Count} Soruces And {TaintAnalyzer.Sinks.Count} Sinkes.\n");
                Console.ResetColor();

                return true;
            }

            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
                return false;
            }    
        }
        public static string Convert2JavaSignature(string sig)
        {
             Dictionary<string, char> java_primitive = new Dictionary<string, char>()
            {
                {"void", 'V'},
                {"boolean", 'Z'},
                {"byte", 'B'},
                {"char", 'C'},
                {"short", 'S'},
                {"int", 'I'},
                {"long", 'J'},
                {"float", 'F'},
                {"double", 'D'}
            };
       
            string[] separatingStrings = { ":", " ","(", ")" };
            string[] parts = sig.Split(separatingStrings, StringSplitOptions.RemoveEmptyEntries);

            string klass = parts[0];
            string method = parts[2];
            string ret = "";
            if (java_primitive.ContainsKey(parts[1]))
            {
                ret = java_primitive[parts[1]].ToString();
            }
            else
            {
                ret += $"L{parts[1]};"; 
            }

            string[] parameters;
            string paramString = "";
            if (parts.Length > 3)
            {
                parameters = parts[3].Split(',');
                
                foreach (var param in parameters)
                {
                    string tmp = param.Trim();
                    if (param.EndsWith("[]"))
                    {
                        paramString += "[";
                        tmp = tmp.Substring(0, param.Length - 2);
                    }
                    if (java_primitive.ContainsKey(tmp))
                    {
                        paramString += java_primitive[tmp].ToString();
                    }
                    else
                    {
                        paramString += $"L{tmp};";
                    }
                }
            }
                       
            return $"{klass}/{method}({paramString}){ret}";
        }
        public static string GetCallgraphKey(IMethodSymbol methodSymbol)
        {
            if(methodSymbol == null)
            {
                return "";
            }
            string clazz = methodSymbol.ContainingSymbol.ToString();
            string func = methodSymbol.Name;
            List<string> parameterList = new List<string>();
            for (int i = 0; i < methodSymbol.Parameters.Length; i++)
            {
                parameterList.Add(methodSymbol.Parameters[i].ToString());
            }
            string parameters = "[" + string.Join(",", parameterList) + "]";
            string returntype = methodSymbol.ReturnType.ToString();
            
            string signature = $"({parameters.Substring(1, parameters.Length - 2)}){returntype}";
            return $"{clazz}/{func}{signature}";
        }
        private void Write_CG_JSON()
        {
            var jsonPath = Path.GetFullPath($"{Program.outPath}\\csharp-tainted-callgraph.json");

            Console.WriteLine($"\nWriting '{jsonPath}'.");
            JObject jsonData = new JObject(new JProperty("LANG", "CSHARP"));


            JArray targetsJSON = new JArray();
            foreach (var target in targets)
            {
                targetsJSON.Add(target);
            }
            jsonData.Add("targets", targetsJSON);

            JArray nodesJSON = new JArray();
            foreach (CallgraphNode node in taintCallgraph.GetNodes())
            {
                JObject nodeUnit = new JObject(
                        new JProperty("class", node.Clazz),
                        new JProperty("func", node.Function),
                        new JProperty("parameters", node.parameters),
                        new JProperty("return", node.ReturnType),
                        new JProperty("isWrapper", node.IsWrapper ? true : false)
                        );
                nodesJSON.Add(nodeUnit);
            }
            jsonData.Add("nodes", nodesJSON);

            JArray edgesJSON = new JArray();
            foreach (CallgraphEdge edge in taintCallgraph.GetEdges())
            {
                JObject edgeUnit = new JObject(
                        new JProperty("srcClass", edge.src.Clazz),
                        new JProperty("srcFunc", edge.src.Function),
                        new JProperty("srcSignature", edge.src.GetSignature()),
                        new JProperty("srcUnit", edge.Get_srcUnit()),
                        new JProperty("tgtClass", edge.tgt.Clazz),
                        new JProperty("tgtFunc", edge.tgt.Function),
                        new JProperty("tgtSignature", edge.tgt.GetSignature())
                        );
                edgesJSON.Add(edgeUnit);
            }
            jsonData.Add("edges", edgesJSON);

            File.WriteAllText(jsonPath, jsonData.ToString());


            Console.WriteLine($"\tTainted nodeCnt = {taintCallgraph.GetNodes().Count}");
            Console.WriteLine($"\tTainted edgeCnt = {taintCallgraph.GetEdges().Count}");
        }
    }

}
