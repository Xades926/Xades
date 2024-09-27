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
    public class JavaWrapperAnalyzer
    {
        public static Dictionary<SyntaxNode, List<(SyntaxNode, CallgraphEdge)>> JavaWrapperCalls = new Dictionary<SyntaxNode, List<(SyntaxNode, CallgraphEdge)>>();
        public static List<string> CallSyntax;

        private static HashSet<string> JavaAPIs = new HashSet<string>();
       
        private SyntaxNode root;
        private List<(SyntaxNode, CallgraphEdge)> edges;
        public static List<CallgraphEdge> JavaWrapper_Edges = new List<CallgraphEdge>();
        public static HashSet<string> JavaWrapper_Nodes = new HashSet<string>();
        public static int javaCustom = 0;
        private string jclass;

        private Dictionary<string, CallgraphNode> MethodIDs = new Dictionary<string, CallgraphNode>();
        public static List<string> InitCallFuctions()
        {
            List<string> CallFunctions = new List<string>();
            string[] ManagedType = { "Void", "Boolean", "SByte", "Char", "Int16", "Int32", "Int64", "Single", "Double", "Object" };
            string[] JNIENVManagedType = { "Object", "Boolean", "Byte", "Char", "Short", "Int", "Long", "Float", "Double", "Void" };

            foreach (string s in ManagedType)
            {
                CallFunctions.Add($"Invoke{s}Method");
                CallFunctions.Add($"TryInvoke{s}StaticRedirect");
                CallFunctions.Add($"InvokeAbstract{s}Method");

                CallFunctions.Add($"InvokeVirtual{s}Method");
                CallFunctions.Add($"InvokeNonvirtual{s}Method");
            }

            foreach (string s in JNIENVManagedType)
            {
                // JNIEnv Entries
                CallFunctions.Add($"Call{s}Method");
                CallFunctions.Add($"Call{s}MethodA");

                CallFunctions.Add($"CallNonvirtual{s}Method");
                CallFunctions.Add($"CallNonvirtual{s}MethodA");

                CallFunctions.Add($"CallStatic{s}Method");
                CallFunctions.Add($"CallStatic{s}MethodA");
            }

            CallFunctions.Add("FinishCreateInstance");
            return CallFunctions;
        }
        public JavaWrapperAnalyzer(SyntaxNode root, List<(SyntaxNode, CallgraphEdge)> edges)
        {
            this.root = root;
            this.edges = edges;
        }
        public static bool Is_JavaWrapper(string srcUnit)
        {
            foreach (string syntax  in CallSyntax)
            {
                if (srcUnit.Contains(syntax)) return true;
            }
            return false;
        }

        public static bool InitializeJavaAPIs(string javaApiPath)
        {
            Console.WriteLine($"Initailize Java Api lists from \'{javaApiPath}\'... ");
            
            try
            {
                string[] lines = System.IO.File.ReadAllLines(javaApiPath);
             //   string[] separatingStrings = { "->", "<", ">" };
                foreach (string line in lines)
                {
                    if (!line.StartsWith("%") && line.Length > 0)
                    {
                        JavaAPIs.Add(line);
                    }
                }
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Finishied Initialization {JavaWrapperAnalyzer.JavaAPIs.Count} Java APIs.\n");
                Console.ResetColor();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
                return false;
            }
        }
        public static bool IsJavaAPI(string clazz)
        {
            foreach (string api in JavaWrapperAnalyzer.JavaAPIs.ToList())
            {
                if (clazz.StartsWith(api))
                {
                    return true;
                }
            }

            return false;
        }
        public void AnalyzeJavaWrapper()
        {
            jclass = FindJavaClazz(root);
            if (jclass != "")
            {
                string JavaFunc = "";
                CollectMethodIDs();

                foreach (var edge in edges)
                {
                    CallgraphEdge ACWEdge = null;
                    if (edge.Item2.srcUnit.ToString().Contains("Invoke"))
                    {
                        // Invoke, TryInvoke 함수는 이름 들어가서 symbol 검사 x.
                    
                        InvocationExpressionSyntax ExtractSyntax = FindACWChain(edge.Item1);
                        if (ExtractSyntax == null)
                        {
                            JavaFunc = (edge.Item1 as InvocationExpressionSyntax).ArgumentList.Arguments[0].ToString();
                        }
                        else
                        {
                            JavaFunc = ExtractSyntax.ArgumentList.Arguments[0].ToString();
                        }

                        ACWEdge = AnalyzeInvokeFunc(edge.Item2, JavaFunc);
                    }
                    else if (edge.Item2.srcUnit.ToString().Contains("JNIEnv.Call"))
                    {
                        ACWEdge = AnalyzeCallFunc(edge.Item2, edge.Item1);
                    }
                    else if (edge.Item2.srcUnit.ToString().Contains("FinishCreateInstance"))
                    {
                        ACWEdge = AnalyzeConstructor(edge.Item2);
                    }

                    if (ACWEdge != null)
                    {
                        if (!IsJavaAPI(ACWEdge.tgt.Clazz))
                        {
                            string nodeKey = ACWEdge.tgt.GetKey();
                            TaintAnalyzer.Sources.Add(nodeKey);
                            TaintAnalyzer.Sinks.Add(nodeKey);
                            JavaWrapperAnalyzer.javaCustom++;
                            
                        }
                        JavaWrapper_Nodes.Add(ACWEdge.tgt.GetKey());
                        JavaWrapper_Edges.Add(ACWEdge);
                    }
                }
            }
        }
        public static void Add_JavaWrapperEdge(Graph<CallgraphNode, CallgraphEdge> cg)
        {
            foreach(CallgraphEdge wrapperEdge in JavaWrapper_Edges)
            {
          
                string[] splits = wrapperEdge.tgt.Clazz.Split('.');
                string UpperClass = "";
                foreach(string splitsStr in splits)
                {
                    if (!string.IsNullOrEmpty(splitsStr))
                    {
                        char[] charArray = splitsStr.ToCharArray();
                        charArray[0] = char.ToUpper(charArray[0]);

                        UpperClass += $"{new string(charArray)}.";
                    }
                }
                UpperClass = UpperClass.Substring(0, UpperClass.Length-1);
                wrapperEdge.tgt.IsWrapper = true;
                wrapperEdge.tgt.IsJava = true;

                CallgraphNode wrapperSrc = new CallgraphNode();
                wrapperSrc.Clazz = UpperClass;
                wrapperSrc.Function = wrapperEdge.src.Function;
                wrapperSrc.parameters = wrapperEdge.src.parameters.Replace("/",".");
                wrapperSrc.ReturnType = wrapperEdge.src.ReturnType;
                wrapperSrc.Signature = wrapperEdge.src.Signature;
                wrapperSrc.syntax = wrapperEdge.src.syntax;
                wrapperSrc.IsWrapper = true;
                wrapperSrc.isSink = wrapperEdge.src.isSink;
                wrapperSrc.isSource = wrapperEdge.src.isSource;
                wrapperSrc.IsJava = true;
           
                cg.AddNode(wrapperEdge.tgt.GetKey(), wrapperEdge.tgt);
                if (!cg.ContainNode(wrapperSrc.GetKey()))
                {
                    cg.AddNode(wrapperSrc.GetKey(), wrapperSrc);
                    cg.AddEdge(wrapperSrc.GetKey(), wrapperEdge.tgt.GetKey(), new CallgraphEdge(wrapperSrc, wrapperEdge.tgt, wrapperEdge.srcUnit));
                }
                else
                {
                    cg.GetNode(wrapperSrc.GetKey()).IsWrapper = true;
                    cg.AddEdge(wrapperSrc.GetKey(), wrapperEdge.tgt.GetKey(), new CallgraphEdge(wrapperSrc, wrapperEdge.tgt, wrapperEdge.srcUnit));
                }

                cg.GetNode(wrapperEdge.src.GetKey()).IsWrapper = true;
                cg.AddEdge(wrapperEdge.src.GetKey(), wrapperEdge.tgt.GetKey(), wrapperEdge);
            }
        }
        public static JObject JavaWrapper2Json()
        {
            JArray csharp2java_edges = new JArray();
            foreach (CallgraphEdge ACWEdge in JavaWrapper_Edges)
            {
                JObject acwUnit = new JObject(
                        new JProperty("srcClass", ACWEdge.src.Clazz),
                        new JProperty("srcFunc", ACWEdge.src.Function),
                        new JProperty("srcSignature", ACWEdge.src.GetSignature()),
                        new JProperty("srcUnit", ACWEdge.srcUnit.ToString()),
                        new JProperty("tgtClass", ACWEdge.tgt.Clazz),
                        new JProperty("tgtFunc", ACWEdge.tgt.Function),
                        new JProperty("tgtSignature", ACWEdge.tgt.GetSignature())
                        );
                csharp2java_edges.Add(acwUnit);
            }
            return new JObject(new JProperty("edges", csharp2java_edges));
        }
        private InvocationExpressionSyntax FindACWChain(SyntaxNode syntax)
        {
            IEnumerable<SyntaxNode> subInvocations;
            subInvocations = syntax.DescendantNodes().OfType<InvocationExpressionSyntax>().ToList();

            foreach (var invoke in subInvocations)
            {
                if (Is_JavaWrapper(invoke.ToString()))
                    return (InvocationExpressionSyntax)invoke;
            }
            return null;
        }
        private string FindJavaClazz(SyntaxNode rootSyntax)
        {
            string ret = "";
            if(rootSyntax is ClassDeclarationSyntax classDeclarationSyntax)
            {
                var fields = classDeclarationSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
                foreach (FieldDeclarationSyntax field in fields)
                {
                    if (field.ToString().Contains("XAPeerMembers"))
                    {
                        var XAPeerMembers = field.Declaration.Variables[0].Initializer;
                        if (XAPeerMembers != null)
                        {
                            ret = (XAPeerMembers.Value as ObjectCreationExpressionSyntax).ArgumentList.Arguments[0].ToString();
                        }

                    }
                    if (field.ToString().Contains("JniPeerMembers"))
                    {
                        var JniPeerMembers = field.Declaration.Variables[0].Initializer;
                        if (JniPeerMembers != null)
                        {
                            ret = (JniPeerMembers.Value as ObjectCreationExpressionSyntax).ArgumentList.Arguments[0].ToString();
                        }
                    }
                }
            }
            else if(rootSyntax is InterfaceDeclarationSyntax interfaceDeclarationSyntax)
            {
                var fields = interfaceDeclarationSyntax.DescendantNodes().OfType<FieldDeclarationSyntax>().ToArray();
                foreach (FieldDeclarationSyntax field in fields)
                {
                    if (field.ToString().Contains("XAPeerMembers"))
                    {
                        var XAPeerMembers = field.Declaration.Variables[0].Initializer;
                        if (XAPeerMembers != null)
                        {
                            ret = (XAPeerMembers.Value as ObjectCreationExpressionSyntax).ArgumentList.Arguments[0].ToString();
                        }

                    }
                    if (field.ToString().Contains("JniPeerMembers"))
                    {
                        var JniPeerMembers = field.Declaration.Variables[0].Initializer;
                        if (JniPeerMembers != null)
                        {
                            ret = (JniPeerMembers.Value as ObjectCreationExpressionSyntax).ArgumentList.Arguments[0].ToString();
                        }
                    }
                }
            }
                   

            return ret.Replace("\"", string.Empty).Replace("/", "."); ;
        }
        private void CollectMethodIDs()
        {
            var expresions = root.DescendantNodes().OfType<ExpressionStatementSyntax>();

            foreach (var expresion in expresions)
            {
                if (expresion.ToString().Contains("GetMethodID"))
                {
                    if(expresion.Expression is AssignmentExpressionSyntax assign)
                    {
                        string methodID = assign.Left.ToString();

                        if (MethodIDs.ContainsKey(methodID))
                        {
                            MethodIDs.Remove(methodID);
                        }

                        CallgraphNode tgtNode = new CallgraphNode();

                        tgtNode.Clazz = jclass;
                        tgtNode.Function = (assign.Right as InvocationExpressionSyntax).ArgumentList.Arguments[1].ToString().Replace("\"", string.Empty);
                        tgtNode.IsWrapper = true;
                        tgtNode.Signature = (assign.Right as InvocationExpressionSyntax).ArgumentList.Arguments[2].ToString().Replace("\"", string.Empty).Replace("/", ".");
                        tgtNode.ReturnType = GetReturnType(tgtNode.Signature);
                        MethodIDs.Add(methodID, tgtNode);
                    }
                    else
                    {
                     
                    }
                }
            }
        }
        private CallgraphEdge AnalyzeInvokeFunc(CallgraphEdge acwEdge, string JavaFunc)
        {
            acwEdge.src.IsWrapper = true;
            acwEdge.tgt = new CallgraphNode();

            acwEdge.tgt.Clazz = jclass;
            acwEdge.tgt.IsWrapper = true;

            JavaFunc = JavaFunc.Replace("\"", string.Empty);
            string[] JavaFuncSplit = JavaFunc.Split('.');

            acwEdge.tgt.Function = JavaFuncSplit[0];
            acwEdge.tgt.Signature = JavaFuncSplit[1].Replace("/", ".");
            acwEdge.tgt.ReturnType = GetReturnType(acwEdge.tgt.GetSignature());
            return acwEdge;
        }
        private CallgraphEdge AnalyzeCallFunc(CallgraphEdge acwEdge, SyntaxNode syntax)
        {
      
            InvocationExpressionSyntax ExtractSyntax = FindACWChain(syntax);
            if (ExtractSyntax == null)
                ExtractSyntax = syntax as InvocationExpressionSyntax;

            var methodId = ExtractSyntax.ArgumentList.Arguments[1].ToString();

            if (MethodIDs.ContainsKey(methodId))
            {
                acwEdge.tgt = MethodIDs[methodId];
            }
            acwEdge.src.IsWrapper = true;

            return null;
        }

        private CallgraphEdge AnalyzeConstructor(CallgraphEdge acwEdge)
        {
            acwEdge.tgt = new CallgraphNode();
            acwEdge.tgt.Clazz = jclass;
            acwEdge.tgt.Function = "<init>";
            acwEdge.tgt.IsWrapper = true;
            acwEdge.tgt.Signature = (acwEdge.srcUnit as InvocationExpressionSyntax).ArgumentList.Arguments[0].ToString().Replace("\"", string.Empty).Replace("/", ".");
            acwEdge.tgt.ReturnType = GetReturnType(acwEdge.tgt.Signature);
            acwEdge.src.IsWrapper = true;
            return acwEdge;
        }
        private string GetReturnType(string signature)
        {
            string returnType = "";
            Dictionary<char, string> java_primitive = new Dictionary<char, string>()
            {
                {'V', "void"},
                {'Z', "boolean"},
                {'B', "byte"},
                {'C', "char"},
                {'S', "short"},
                {'I', "int"},
                {'J', "long"},
                {'F', "float"},
                {'D', "double" }
            };

            string[] separatingStrings = { ";", " ", "(", ")" };
            string[] parts = signature.Split(separatingStrings, StringSplitOptions.RemoveEmptyEntries);

            for(int i=0; i<parts.Last().Length; i++)
            {
                if (java_primitive.ContainsKey(parts.Last()[i])){
                    returnType += java_primitive[parts.Last()[i]];
                }
                else
                {
                    returnType += parts.Last().Substring(1);
                    break;
                }
            }
            return returnType;
        }
    }
}