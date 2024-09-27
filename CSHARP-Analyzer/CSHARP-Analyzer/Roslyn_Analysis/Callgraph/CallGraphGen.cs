using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyn_Analysis.Graph;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using Roslyn_Analysis.DataFlowAnalysis;
using System.Runtime.InteropServices;

namespace Roslyn_Analysis.Callgraph
{
    public class CallGraph
    {
        public static Graph<CallgraphNode, CallgraphEdge> callgraph = new Graph<CallgraphNode, CallgraphEdge>();

        private Compilation compilation;
        private HashSet<SyntaxTree> syntaxTrees;
        public CallGraph(HashSet<SyntaxTree> syntaxTrees)
        {
            this.compilation = Program.Compilation;
            this.syntaxTrees = syntaxTrees;
        }
        public void AnalyzeCallGraph()
        {
            foreach (var tree in syntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree, false);
                var walker = new CallGraphWalker(semanticModel);
                walker.Visit(tree.GetRoot());
            }
            foreach (var tree in syntaxTrees)
            {
                AnalyzeObjectCreation(tree);
                AnalazeMemberAccess(tree);
            }
        }
        private void AnalyzeObjectCreation(SyntaxTree tree)
        {
            var semanticModel = compilation.GetSemanticModel(tree, false);

            var creations = tree.GetRoot().DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
            foreach (var creationSyntax in creations)
            {
                CallgraphNode srcNode = SrcNodeExtract(semanticModel, creationSyntax);


                IMethodSymbol ctor = semanticModel.GetSymbolInfo(creationSyntax).Symbol as IMethodSymbol;
                CallgraphNode tgtNode = new CallgraphNode();

                if(ctor != null)
                {
                    tgtNode.Clazz = ctor.ContainingSymbol.ToString();
                    tgtNode.Function = ctor.Name;
                    List<string> srcParameterList = new List<string>();
                    for (int i = 0; i < ctor.Parameters.Length; i++)
                    {
                        srcParameterList.Add(ctor.Parameters[i].ToString());
                    }
                    tgtNode.parameters = "[" + string.Join(",", srcParameterList) + "]";
                    tgtNode.ReturnType = ctor.ReturnType.ToString();



                    SyntaxNode node = creationSyntax;
                    while (node != null)
                    {
                        if (node is StatementSyntax)
                        {
                            break;
                        }
                        if (node.Parent is ClassDeclarationSyntax)
                        {
                            break;
                        }
                        if (node.Parent is StructDeclarationSyntax)
                        {
                            break;
                        }

                        node = node.Parent;
                    }


                    callgraph.AddNode(srcNode.GetKey(), srcNode);
                    callgraph.AddNode(tgtNode.GetKey(), tgtNode);

                    CallgraphEdge edge = new CallgraphEdge(srcNode, tgtNode, node);
                    callgraph.AddEdge(edge.src.GetKey(), edge.tgt.GetKey(), edge);
                }
                else
                {
                    // TODO :: Delegeate new, ... creationSymbol 못 찾음.
                     //Console.WriteLine($"DEBUG :: is Mashal? {creationSyntax.Parent.ToFullString()}");
                }
       

            }
        }
        private void AnalazeMemberAccess(SyntaxTree tree)
        {
            var semanticModel = compilation.GetSemanticModel(tree, false);

            var members = tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>();
            foreach (var memberAccessSyntax in members)
            {
                var symbol = semanticModel.GetSymbolInfo(memberAccessSyntax.Name).Symbol;
                
                CallgraphNode srcNode = SrcNodeExtract(semanticModel, memberAccessSyntax);
                CallgraphNode tgtNode = new CallgraphNode();
                tgtNode.parameters = "[]";
                tgtNode.ReturnType = "";
                IMethodSymbol tgtSymbol = null;
                if (symbol is IPropertySymbol propertySymbol)
                {
                    if (memberAccessSyntax.Parent is AssignmentExpressionSyntax assignmentExpressionSyntax)
                    {
                        if (memberAccessSyntax == assignmentExpressionSyntax.Left)
                        {
                            tgtSymbol = propertySymbol.SetMethod;
                        }
                        if (memberAccessSyntax == assignmentExpressionSyntax.Right)
                        {
                            tgtSymbol = propertySymbol.GetMethod;
                        }
                    }
                    else
                    {
                        tgtSymbol = propertySymbol.GetMethod;
                    }
                    if (memberAccessSyntax.Parent is EqualsValueClauseSyntax)
                    {
                         tgtSymbol = propertySymbol.GetMethod;
                    }
                }
               
                if (tgtSymbol != null)
                {
                    
                    tgtNode.Clazz = tgtSymbol.ContainingSymbol.ToString();
                    tgtNode.Function = tgtSymbol.Name;
                    List<string> srcParameterList = new List<string>();
                    for (int i = 0; i < tgtSymbol.Parameters.Length; i++)
                    {
                        srcParameterList.Add(tgtSymbol.Parameters[i].ToString());
                    }
                    tgtNode.parameters = "[" + string.Join(",", srcParameterList) + "]";
                    tgtNode.ReturnType = tgtSymbol.ReturnType.ToString();

                    SyntaxNode node = memberAccessSyntax;
                    while (node != null)
                    {
                        if (node is StatementSyntax)
                        {
                            break;
                        }
                        if (node is PropertyDeclarationSyntax)
                        {
                            break;
                        }
                        if (node is FieldDeclarationSyntax)
                        {
                            break;
                        }
                        node = node.Parent;
                    }

                    if (node != null)
                    {
                      
                      
                        callgraph.AddNode(srcNode.GetKey(), srcNode);
                        callgraph.AddNode(tgtNode.GetKey(), tgtNode);

                        CallgraphEdge edge = new CallgraphEdge(srcNode, tgtNode, node);

                        if (JavaWrapperAnalyzer.Is_JavaWrapper(memberAccessSyntax.ToString()))
                        {
                            var classSyntaxNode = ExtractClassSyntax(memberAccessSyntax);
                            try
                            {
                                JavaWrapperAnalyzer.JavaWrapperCalls[classSyntaxNode].Add((memberAccessSyntax, edge));
                            }
                            catch (Exception)
                            {
                                JavaWrapperAnalyzer.JavaWrapperCalls.Add(classSyntaxNode, new List<(SyntaxNode, CallgraphEdge)>());
                                JavaWrapperAnalyzer.JavaWrapperCalls[classSyntaxNode].Add((memberAccessSyntax, edge));
                            }
                        }
                        else
                        {
                            callgraph.AddEdge(edge.src.GetKey(), edge.tgt.GetKey(), edge);
                        }
                    }

                }
            }
        }
        public static (ISymbol, SyntaxNode) ExtractSrcSymbol(SemanticModel semanticModel, SyntaxNode node)
        {
            while (node != null)
            {
                
                if (node is DelegateDeclarationSyntax delegateDeclaration)
                {
                    ISymbol srcSymbol = semanticModel.GetDeclaredSymbol(delegateDeclaration);
                    return (srcSymbol, delegateDeclaration);
                }
                else if (node is EnumMemberDeclarationSyntax enumMemberDeclaration)
                {
                    ISymbol srcSymbol = semanticModel.GetDeclaredSymbol(enumMemberDeclaration);
                    return (srcSymbol, enumMemberDeclaration);
                }
                else if (node is GlobalStatementSyntax globalStatement)
                {
                    ISymbol srcSymbol = semanticModel.GetDeclaredSymbol(globalStatement);
                    return (srcSymbol, globalStatement);
                }
                else if (node is IncompleteMemberSyntax incompleteMember)
                {
                    ISymbol srcSymbol = semanticModel.GetDeclaredSymbol(incompleteMember);
                    return (srcSymbol, incompleteMember);
                }
                else if (node is MethodDeclarationSyntax methodDeclaration)
                {
                    ISymbol srcSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);
                    return (srcSymbol, methodDeclaration);
                }
                else if (node is PropertyDeclarationSyntax propertyDeclaration)
                {
                    ISymbol srcSymbol = semanticModel.GetDeclaredSymbol(propertyDeclaration);
                    return (srcSymbol, propertyDeclaration);
                }
                else if (node is AccessorDeclarationSyntax accessorDeclarationSyntax)
                {
                    ISymbol srcSymbol = semanticModel.GetDeclaredSymbol(accessorDeclarationSyntax);
                    return (srcSymbol, accessorDeclarationSyntax);
                }
                else if (node is BaseMethodDeclarationSyntax baseMethodDeclaration)
                {
                    ISymbol srcSymbol = semanticModel.GetDeclaredSymbol(baseMethodDeclaration);
                    return (srcSymbol, baseMethodDeclaration);
                }
                else if (node is BasePropertyDeclarationSyntax basePropertyDeclaration)
                {
                    ISymbol srcSymbol = semanticModel.GetDeclaredSymbol(basePropertyDeclaration.Parent.Parent);

                    return (srcSymbol, basePropertyDeclaration);
                }
                else if (node is BaseTypeDeclarationSyntax baseTypeDeclaration)
                {
                    ISymbol srcSymbol = semanticModel.GetDeclaredSymbol(baseTypeDeclaration);

                    return (srcSymbol, baseTypeDeclaration);
                }
                node = node.Parent;
            }

            return (null, null); // -> using another DeclarationSyntax class?
        }
        public static CallgraphNode SrcNodeExtract(SemanticModel semanticModel, SyntaxNode CallSyntaxNode)
        {
            var srcExtract = ExtractSrcSymbol(semanticModel, CallSyntaxNode);
            ISymbol srcSymbol = srcExtract.Item1;
            SyntaxNode srcSyntaxNode = srcExtract.Item2;

            CallgraphNode node = new CallgraphNode();

            if (srcSyntaxNode is PropertyDeclarationSyntax propertyDeclarationSyntax)
            {
                if (propertyDeclarationSyntax.ExpressionBody != null)
                {
                    node.Function = $"get_{propertyDeclarationSyntax.Identifier.ValueText}";
                }

                node.Clazz = semanticModel.GetDeclaredSymbol(propertyDeclarationSyntax.Parent).ToString();
                node.IsWrapper = false;
                node.parameters = "[]";
                if (semanticModel.GetTypeInfo(propertyDeclarationSyntax.Type).Type == null)
                {
                    //DEBUG
                    node.ReturnType = "null";
                }
                else
                {
                    node.ReturnType = semanticModel.GetTypeInfo(propertyDeclarationSyntax.Type).Type.ToString();
                }
                
                node.syntax = srcSyntaxNode;
            }
            else
            {
                if(srcSymbol != null)
                {
                    node.Clazz = srcSymbol.ContainingSymbol.ToString();
                    node.Function = srcSymbol.Name.ToString();
                    node.IsWrapper = false;

                    IMethodSymbol methodSymbol = srcSymbol as IMethodSymbol;
                    if (methodSymbol != null)
                    {
                        List<string> srcParameterList = new List<string>();
                        for (int i = 0; i < methodSymbol.Parameters.Length; i++)
                        {
                            srcParameterList.Add(methodSymbol.Parameters[i].ToString());
                        }
                        node.parameters = "[" + string.Join(",", srcParameterList) + "]";

                        node.ReturnType = methodSymbol.ReturnType.ToString();
                    }

                    node.syntax = srcSyntaxNode;
                }
            }
            return node;
        }
        private SyntaxNode ExtractClassSyntax(SyntaxNode node)
        {
            while (node != null)
            {
                if (node is ClassDeclarationSyntax classDeclarationSyntax)
                {
                    return classDeclarationSyntax;
                }
                else if (node is InterfaceDeclarationSyntax interfaceDeclarationSyntax)
                {
                    return interfaceDeclarationSyntax;
                }

                node = node.Parent;
            }

            return null; // -> using another DeclarationSyntax class?
        }
        public void writeJSON()
        {
            var jsonPath = Path.GetFullPath($"{Program.outPath}\\csharp-callgraph.json");

            Console.WriteLine($"\nWriting '{jsonPath}'.");
            JObject jsonData = new JObject(new JProperty("LANG", "CSHARP"));

            JArray nodesJSON = new JArray();
            foreach (CallgraphNode node in callgraph.GetNodes())
            {
                JObject nodeUnit = new JObject(
                        new JProperty("class", node.Clazz),
                        new JProperty("func", node.Function),
                        new JProperty("parameters", node.parameters),
                        new JProperty("return", node.ReturnType),
                        new JProperty("isNative", node.IsWrapper ? true : false)
                        );
                nodesJSON.Add(nodeUnit);
            }
            jsonData.Add("nodes", nodesJSON);

            JArray edgesJSON = new JArray();
            foreach (CallgraphEdge edge in callgraph.GetEdges())
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


            Console.WriteLine($"\tnodeCnt = {callgraph.GetNodes().Count}");
            Console.WriteLine($"\tedgeCnt = {callgraph.GetEdges().Count}");
        }
    }


    public class CallGraphWalker : CSharpSyntaxWalker   
    {
        private readonly SemanticModel semanticModel;
        public CallGraphWalker(SemanticModel semanticModel)
        {
            this.semanticModel = semanticModel;
        }
        private SyntaxNode ExtractClassSyntax(SyntaxNode node)
        {
            while (node != null)
            {
                if (node is ClassDeclarationSyntax classDeclarationSyntax)
                {
                    return classDeclarationSyntax;
                }
                else if (node is InterfaceDeclarationSyntax interfaceDeclarationSyntax)
                {
                    return interfaceDeclarationSyntax;
                }

                node = node.Parent;
            }

            return null; // -> using another DeclarationSyntax class?
        }
        private List<CallgraphNode> TgtNodeExtract(InvocationExpressionSyntax CallSyntaxNode)
        {
       
            List<CallgraphNode> res = new List<CallgraphNode>();
            
            var tgtSymbol = semanticModel.GetSymbolInfo(CallSyntaxNode).Symbol;



            if (tgtSymbol != null)
            {
                var originSymbol = tgtSymbol.OriginalDefinition;
                var attributes = tgtSymbol.OriginalDefinition.GetAttributes();
                foreach (AttributeData attribute in attributes)
                {
                    if (attribute.AttributeClass.Name.Equals("DllImportAttribute"))
                    {
                        string lib = "";

                        if (attribute.ToString().Equals("System.Runtime.InteropServices.DllImportAttribute"))
                        {
                            //   Console.WriteLine();
                        }

                        if (attribute.ConstructorArguments.Length > 0)
                            lib = attribute.ConstructorArguments[0].Value.ToString();
                        string entry = "";
                        string callingConvention = "";
                        foreach (var NamedArg in attribute.NamedArguments)
                        {
                            if (NamedArg.Key.Equals("EntryPoint"))
                            {
                                entry = NamedArg.Value.Value.ToString();
                            }
                            if (NamedArg.Key.Equals("CallingConvention"))
                            {
                                if (!NamedArg.Value.IsNull)
                                {
                                    callingConvention = NamedArg.Value.Value.ToString();
                                }
                                else
                                {
                                    callingConvention = "2";
                                }
                            }
                        }
                        if (entry.Length == 0)
                            entry = originSymbol.Name;


                        CppWrapperAnalyzer.Add_CppWrapper(tgtSymbol, new CppWrapper(lib, entry));
                    }
                }
                CallgraphNode node = new CallgraphNode();

                if (tgtSymbol.ContainingSymbol == null)
                {
                    // delegate 
                    Console.WriteLine($"\tDEBUG: {CallSyntaxNode.ToFullString()}");
                }
                else
                {
                    node.Clazz = tgtSymbol.ContainingSymbol.ToString();
                    node.Function = tgtSymbol.Name.ToString();
                    node.IsWrapper = false;

                    List<string> tgtParameterList = new List<string>();

                    if (tgtSymbol is IMethodSymbol methodSymbol)
                    {
                        foreach (var parameter in methodSymbol.Parameters)
                        {
                            tgtParameterList.Add(parameter.ToString());
                        }

                        node.parameters = "[" + string.Join(",", tgtParameterList) + "]";

                        node.ReturnType = methodSymbol.ReturnType.ToString();
                    }
                    else
                    {
                        node.parameters = "[" + string.Join(",", tgtParameterList) + "]";

                        node.ReturnType = "event";
                        Console.WriteLine();
                    }
                    try
                    {
                        SyntaxNode syntaxNode = tgtSymbol.DeclaringSyntaxReferences[0].GetSyntax();
                        MethodDeclarationSyntax methodSyntax = syntaxNode as MethodDeclarationSyntax;
                        node.syntax = methodSyntax;
                    }
                    catch
                    {
                        Console.WriteLine();
                    }
                    res.Add(node);
                }
            }
            else
            {
                CallgraphNode node = new CallgraphNode();

                if (CallSyntaxNode.Expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax)
                {
                    var symbols = semanticModel.GetSymbolInfo(memberAccessExpressionSyntax.Name).CandidateSymbols;
                    if (symbols.Length > 0)
                    {
                        tgtSymbol = symbols.First();
                        node.Clazz = tgtSymbol.ContainingSymbol.ToString();
                        node.Function = tgtSymbol.Name.ToString();
                        node.IsWrapper = false;

                        List<string> tgtParameterList = new List<string>();

                        if(tgtSymbol is IMethodSymbol methodSymbol)
                        {
                            foreach (var parameter in methodSymbol.Parameters)
                            {
                                tgtParameterList.Add(parameter.ToString());
                            }

                            node.parameters = "[" + string.Join(",", tgtParameterList) + "]";

                            node.ReturnType = methodSymbol.ReturnType.ToString();
                        }
                        else
                        {
                            node.parameters = "[" + string.Join(",", tgtParameterList) + "]";

                            node.ReturnType = "event";
                            Console.WriteLine();
                        }
                      

                        try
                        {
                            SyntaxNode syntaxNode = tgtSymbol.DeclaringSyntaxReferences[0].GetSyntax();
                            MethodDeclarationSyntax methodSyntax = syntaxNode as MethodDeclarationSyntax;
                            node.syntax = methodSyntax;
                        }
                        catch
                        {

                        }
                        res.Add(node);
                    }
                }
            }

            var Invokes = CallSyntaxNode.DescendantNodes().OfType<InvocationExpressionSyntax>();
            Invokes.Append(CallSyntaxNode);
            foreach (var Invoke in Invokes)
            {
                CallgraphNode node = new CallgraphNode();
                node.Clazz = "NRE in NodeDataExtract()";
                node.Function = "NRE in NodeDataExtract()";
                node.parameters = "[]";
                node.ReturnType = "";
                IMethodSymbol tgtMethodSymbol = semanticModel.GetSymbolInfo(Invoke).Symbol as IMethodSymbol;
                if (tgtMethodSymbol != null)
                {
                    node.Clazz = tgtMethodSymbol.ContainingType.ToString();
                    node.Function = tgtMethodSymbol.Name.ToString();
                    List<string> nodeParameterList = new List<string>();
                    foreach (var parameter in ((IMethodSymbol)tgtMethodSymbol).Parameters)
                    {
                        nodeParameterList.Add(parameter.ToString());
                    }
                    node.parameters = "[" + string.Join(",", nodeParameterList) + "]";

                    node.ReturnType = ((IMethodSymbol)tgtMethodSymbol).ReturnType.ToString();
                   

                    try
                    {
                        SyntaxNode syntaxNode = tgtMethodSymbol.DeclaringSyntaxReferences[0].GetSyntax();
                        MethodDeclarationSyntax methodSyntax = syntaxNode as MethodDeclarationSyntax;
                        node.syntax = methodSyntax;
                    }
                    catch
                    {

                    }
                    res.Add(node);
                }
                else
                {
                    // 객체 생성 후, 객체 함수 호출 못 잡나..? StartCreateInstance .. 
                    // Console.WriteLine("  DEBUG :: NRE ;;");
                    if (CallSyntaxNode.Expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax)
                    {
                        var symbols = semanticModel.GetSymbolInfo(memberAccessExpressionSyntax.Name).CandidateSymbols;
                        if (symbols.Length > 0)
                        {
                            tgtSymbol = symbols.First();
                            node.Clazz = tgtSymbol.ContainingSymbol.ToString();
                            node.Function = tgtSymbol.Name.ToString();
                            node.IsWrapper = false;

                            List<string> tgtParameterList = new List<string>();
                            foreach (var parameter in ((IMethodSymbol)tgtSymbol).Parameters)
                            {
                                tgtParameterList.Add(parameter.ToString());
                            }

                            node.parameters = "[" + string.Join(",", tgtParameterList) + "]";

                            node.ReturnType = ((IMethodSymbol)tgtSymbol).ReturnType.ToString();

                            try
                            {
                                SyntaxNode syntaxNode = tgtSymbol.DeclaringSyntaxReferences[0].GetSyntax();
                                MethodDeclarationSyntax methodSyntax = syntaxNode as MethodDeclarationSyntax;
                                node.syntax = methodSyntax;
                            }
                            catch
                            {

                            }
                            res.Add(node);
                        }
                    }
                }

            }
            
        
            return res;
        }
        private List<CallgraphEdge> EdgeDataExtract(CallgraphNode srcNode, List<CallgraphNode> tgtNodes, SyntaxNode tgtSyntaxNode)
        {
            List<CallgraphEdge> res = new List<CallgraphEdge>();

            foreach (CallgraphNode tgtNode in tgtNodes)
            {
                CallgraphEdge edge = new CallgraphEdge(srcNode, tgtNode, tgtSyntaxNode);
                res.Add(edge);
            }
            return res;
        }
        public override void VisitInvocationExpression(InvocationExpressionSyntax invocationSyntax)
        {
            CallgraphNode srcNode = CallGraph.SrcNodeExtract(semanticModel, invocationSyntax);
         
            List<CallgraphNode> tgtNodes = TgtNodeExtract(invocationSyntax);
            List<CallgraphEdge> edges = EdgeDataExtract(srcNode, tgtNodes, invocationSyntax);

          
            if (edges.Count == 0)
                return;

            foreach (CallgraphEdge edge in edges)
            {
                if (JavaWrapperAnalyzer.Is_JavaWrapper(edge.srcUnit.ToString()))
                {
                    var classSyntaxNode = ExtractClassSyntax(invocationSyntax);

                    if (JavaWrapperAnalyzer.JavaWrapperCalls.ContainsKey(classSyntaxNode))
                    {
                        JavaWrapperAnalyzer.JavaWrapperCalls[classSyntaxNode].Add((invocationSyntax, edge));
                    }
                    else
                    {
                        JavaWrapperAnalyzer.JavaWrapperCalls.Add(classSyntaxNode, new List<(SyntaxNode, CallgraphEdge)>());
                        JavaWrapperAnalyzer.JavaWrapperCalls[classSyntaxNode].Add((invocationSyntax, edge));
                    }
                  
                }
            }
          



            foreach (CallgraphEdge edge in edges)
            {
                CallGraph.callgraph.AddNode(edge.src.GetKey(), edge.src);
                CallGraph.callgraph.AddNode(edge.tgt.GetKey(), edge.tgt);
                CallGraph.callgraph.AddEdge(edge.src.GetKey(), edge.tgt.GetKey(), edge);
            }
        }
    }
}