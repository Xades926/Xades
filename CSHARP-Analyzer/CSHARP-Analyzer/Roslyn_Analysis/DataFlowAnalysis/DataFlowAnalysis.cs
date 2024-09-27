using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json.Linq;
using Roslyn_Analysis.Callgraph;
using Roslyn_Analysis.TaintAnalysis;

namespace Roslyn_Analysis.DataFlowAnalysis
{

    class DataFlowAnalyzer
    {
        private SemanticModel semanticModel;

        public static HashSet<string> XamarinAndroidAPI = new HashSet<string>();

        public Stack<string> stackTrace;
        private HashSet<DataFlowAnalysisValue> parameters;
        public static bool InitializeXamarinAPI(string xamarinApiPath)
        {
            Console.WriteLine($"Initailize Xamarin Apis from \'{xamarinApiPath}\'... ");

            try
            {
                string[] lines = System.IO.File.ReadAllLines(xamarinApiPath);
                string[] separatingStrings = { "->", "<", ">" };
                foreach (string line in lines)
                {
                    if (!line.StartsWith("%") && line.Length > 0)
                    {
                        XamarinAndroidAPI.Add(line);
                    }

                }
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Finishied Initialization {XamarinAndroidAPI.Count} Xamarin APIs.\n");
                Console.ResetColor();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
                return false;
            }

        }
        public static bool Is_XamarinAPI(string clazz)
        {
            foreach (string api in DataFlowAnalyzer.XamarinAndroidAPI.ToList())
            {
                if (clazz.StartsWith(api))
                {
                    return true;
                }
            }


            return false;
        }
        public static bool IsWrapper(IMethodSymbol methodSymbol)
        {
            string key = TaintAnalyzer.GetCallgraphKey(methodSymbol);
            var cgNode = Callgraph.CallGraph.callgraph.GetNode(key);

            if (cgNode == null)
                return false;

            return cgNode.IsWrapper;
        }
   
        private MethodDeclarationSyntax targetMethod = null;
        private ConstructorDeclarationSyntax targetConstructor = null;
        private AccessorDeclarationSyntax targetAccessor = null;

        private ControlFlowGraph cfg;
        private HashSet<int> visitedOrdinal = new HashSet<int>();
        private SortedSet<int> worklist;
        private PooledDictionary<int, DataFlowAnalysisBlockResult> blockResultMap = PooledDictionary<int, DataFlowAnalysisBlockResult>.GetInstance();

        public DataFlowAnalyzer(Stack<string> stackTrace, MethodDeclarationSyntax targetMethod, HashSet<DataFlowAnalysisValue> parameters)
        {
            //GC.Collect();
            this.stackTrace = stackTrace;
            this.targetMethod = targetMethod;
            this.parameters = parameters;
            this.semanticModel = Program.Compilation.GetSemanticModel(this.targetMethod.SyntaxTree);
            try
            {
                this.cfg = ControlFlowGraph.Create(this.targetMethod, semanticModel);
                this.SetParmeters();
            }
            catch
            {
                Console.WriteLine("Can not compute Control Flow Graph.");
            }

        }
        public DataFlowAnalyzer(Stack<string> stackTrace, ConstructorDeclarationSyntax targetConstructor, HashSet<DataFlowAnalysisValue> parameters)
        {
            //GC.Collect();
            this.stackTrace = stackTrace;
            this.targetConstructor = targetConstructor;
            this.parameters = parameters;
            this.semanticModel = Program.Compilation.GetSemanticModel(targetConstructor.SyntaxTree);
            try
            {
                this.cfg = ControlFlowGraph.Create(targetConstructor, semanticModel);

                ISymbol constructorSymbol = semanticModel.GetDeclaredSymbol(targetConstructor);
                string instanceClazz = TaintAnalyzer.GetCallgraphKey(constructorSymbol as IMethodSymbol);

                if (DataFlowAnalyzer.Is_XamarinAPI(instanceClazz) || IsWrapper(constructorSymbol as IMethodSymbol))
                {

                }
                else
                {
                    this.SetParmeters();
                }
            }
            catch
            {
                Console.WriteLine("Can not compute Control Flow Graph.");
            }
        }
        public DataFlowAnalyzer(Stack<string> stackTrace, AccessorDeclarationSyntax targetAccessor, HashSet<DataFlowAnalysisValue> parameters)
        {
            ///GC.Collect();
            this.stackTrace = stackTrace;
            this.targetAccessor = targetAccessor;
            this.parameters = parameters;
            this.semanticModel = Program.Compilation.GetSemanticModel(targetAccessor.SyntaxTree);
            try
            {
                this.cfg = ControlFlowGraph.Create(targetAccessor, semanticModel);
                this.SetParmeters();
            }
            catch
            {
                Console.WriteLine("Can not compute Control Flow Graph.");
            }

        }
        public void SetParmeters()
        {
            if (cfg != null && parameters.Count > 0)
            {
                var entry = new DataFlowAnalysisBlockResult(cfg.Blocks.Single(b => b.Kind == BasicBlockKind.Entry));
                var args = new DataFlowAnalysisOperationResult();
                if (targetMethod != null)
                {
                    for (int i = 0; targetMethod.ParameterList.Parameters.Count > i; i++)
                    {
                        var type = this.semanticModel.GetTypeInfo(targetMethod.ParameterList.Parameters[i].Type).Type;
                        var name = targetMethod.ParameterList.Parameters[i].Identifier.ToString();
                        var value = parameters.ToArray()[i];
                        value.Name = name;
                        value.Type = type;
                        args.dataValueMap.Add(name, value);
                    }
                    /*
                    var list = parameters.ToArray();
                    for (int i = targetMethod.ParameterList.Parameters.Count; i < parameters.Count; i++)
                    {
                        if(args.dataValueMap.Contains)
                        args.dataValueMap.Add(list[i].Name, list[i]);
                    }
                    */

                    entry.SetParametersOnEntry(args);
                    blockResultMap.Add(entry.basicBlock.Ordinal, entry);

                }
                if (targetConstructor != null)
                {
                    for (int i = 0; i < targetConstructor.ParameterList.Parameters.Count; i++)
                    {
                        var type = this.semanticModel.GetTypeInfo(targetConstructor.ParameterList.Parameters[i].Type).Type;
                        var name = targetConstructor.ParameterList.Parameters[i].Identifier.ToString();
                        var value = parameters.ToArray()[i];
                        value.Name = name;
                        value.Type = type;
                        args.dataValueMap.Add(name, value);
                    }

                    entry.SetParametersOnEntry(args);
                    blockResultMap.Add(entry.basicBlock.Ordinal, entry);
                }
                if (targetAccessor != null)
                {
                    foreach (var val in parameters)
                    {
                        args.dataValueMap.Add(val.Name, val);
                    }

                    entry.SetParametersOnEntry(args);
                    blockResultMap.Add(entry.basicBlock.Ordinal, entry);
                }
            }

        }
        public DataFlowAnalysisValue AnalyzeMethod()
        {
            DataFlowAnalysisValue returnValue = null;  // return type and return returnValue (res)
            if (cfg != null)
            {
                ISymbol symbol = semanticModel.GetDeclaredSymbol(targetMethod);
                Console.WriteLine($"================ DataFlowAnalysis AnalyzeMethod :: {symbol}");

                worklist = new SortedSet<int>(cfg.Blocks.Select(b => b.Ordinal));
                int idx = 0;
                while (worklist.Count > 0)
                {
                    idx = worklist.Min;

                    if (cfg.Blocks[idx].Kind == BasicBlockKind.Entry)
                    {
                        worklist.Remove(idx);
                        continue;
                    }


                    blockResultMap.Add(idx, new DataFlowAnalysisBlockResult(stackTrace, blockResultMap, cfg.Blocks[idx]));
                    returnValue = blockResultMap[idx].AnalyzeOperations();

                    worklist.Remove(idx);
                }
                blockResultMap[idx].GetLastOperationResult().dataValueMap.Add("return", returnValue);
                Console.WriteLine();
                //returnValue.PrintNow();
            }

            return returnValue;
        }
        public (DataFlowAnalysisValue, DataFlowAnalysisValue) AnalyzeInstanceMethod(DataFlowAnalysisValue Instance)
        {
            DataFlowAnalysisValue modifiedInstance = new DataFlowAnalysisValue();
            if (Instance.Type != null)
            {
                modifiedInstance.Type = Instance.Type;
            }
            if (Instance.Name != null)
            {
                modifiedInstance.Name = Instance.Name;
            }
            modifiedInstance.IsInstance = true;

            DataFlowAnalysisValue returnValue = null;  // return type and return returnValue (res)
            if (cfg != null)
            {
                ISymbol symbol = semanticModel.GetDeclaredSymbol(targetMethod);
                Console.WriteLine($"================ DataFlowAnalysis AnalyzeInstanceMethod :: {symbol}");

                worklist = new SortedSet<int>(cfg.Blocks.Select(b => b.Ordinal));
                while (worklist.Count > 0)
                {
                    int idx = worklist.Min;
                    visitedOrdinal.Add(idx);

                    if (cfg.Blocks[idx].Kind == BasicBlockKind.Entry)
                    {
                        worklist.Remove(idx);
                        continue;
                    }

                    blockResultMap.Add(idx, new DataFlowAnalysisBlockResult(stackTrace, blockResultMap, cfg.Blocks[idx]));
                    returnValue = blockResultMap[idx].AnalyzeOperations();

                    worklist.Remove(idx);
                }


                try
                {
                    var res = blockResultMap[cfg.Blocks.Single(b => b.Kind == BasicBlockKind.Exit).Ordinal].GetLastOperationResult();
                    foreach (ParameterSyntax parameter in targetMethod.ParameterList.Parameters)
                    {

                        var name = parameter.Identifier.Text;
                        if (res.dataValueMap.ContainsKey(name))
                            res.dataValueMap.Remove(name);
                    }
                    foreach (var data in res.dataValueMap)
                    {
                        if (!data.Value.IsLocal)
                            modifiedInstance.AddMember(data.Value);
                    }
                    res.dataValueMap.Add("return", returnValue);
                    ////

                    //returnValue.PrintNow();
                }
                catch
                {
                    Console.WriteLine();
                }
            }

            return (modifiedInstance, returnValue);
        }
        public (DataFlowAnalysisValue, DataFlowAnalysisValue) AnalyzeAccessor(DataFlowAnalysisValue Instance)
        {
            DataFlowAnalysisValue modifiedInstance = new DataFlowAnalysisValue(Instance.Type, Instance.Name);
            modifiedInstance.IsInstance = true;

            DataFlowAnalysisValue returnValue = null;  // return type and return returnValue (res)

            if (cfg != null)
            {
                ISymbol symbol = semanticModel.GetDeclaredSymbol(targetAccessor);
                Console.WriteLine($"================ DataFlowAnalysis AnalyzeAccessor :: {symbol}");

                worklist = new SortedSet<int>(cfg.Blocks.Select(b => b.Ordinal));
                while (worklist.Count > 0)
                {
                    int idx = worklist.Min;


                    if (cfg.Blocks[idx].Kind == BasicBlockKind.Entry)
                    {
                        worklist.Remove(idx);
                        continue;
                    }

                    blockResultMap.Add(idx, new DataFlowAnalysisBlockResult(stackTrace, blockResultMap, cfg.Blocks[idx]));
                    returnValue = blockResultMap[idx].AnalyzeOperations();

                    visitedOrdinal.Add(idx);
                    worklist.Remove(idx);
                }



                var res = blockResultMap[cfg.Blocks.Single(b => b.Kind == BasicBlockKind.Exit).Ordinal].GetLastOperationResult();
                res.dataValueMap.Remove("value");

                foreach (var data in res.dataValueMap)
                {
                    if (!data.Value.IsLocal)
                        modifiedInstance.AddMember(data.Value);
                }
                res.dataValueMap.Add("return", returnValue);

            }

            return (modifiedInstance, returnValue);
        }
        public DataFlowAnalysisValue AnalyzeConstructor(IOperation operation)
        {
            DataFlowAnalysisValue instance = null;

            if (targetConstructor != null && cfg != null)
            {
                SyntaxNode node = targetConstructor;
                ISymbol constructorSymbol = semanticModel.GetDeclaredSymbol(targetConstructor);
                string instanceClazz = TaintAnalyzer.GetCallgraphKey(constructorSymbol as IMethodSymbol);


                if (DataFlowAnalyzer.Is_XamarinAPI(instanceClazz) || IsWrapper(constructorSymbol as IMethodSymbol))
                {
                    string invocation = $"new {constructorSymbol.ContainingSymbol.ToString()}()";
                    instance = new DataFlowAnalysisValue(constructorSymbol.ContainingType, invocation, parameters);
                    instance.IsInstance = true;

                    if (TaintAnalyzer.IsSource(constructorSymbol as IMethodSymbol) && TaintAnalyzer.IsSink(constructorSymbol as IMethodSymbol))
                    {
                        TaintResult Both = new TaintResult(constructorSymbol, operation, instance);
                        TaintAnalyzer.bothCalls.Add(Both);

                        instance.IsTainted = true;
                        instance.sourceFrom = Both;
                    }
                    else if (TaintAnalyzer.IsSource(constructorSymbol as IMethodSymbol))
                    {
                        TaintResult source = new TaintResult(constructorSymbol, operation);
                        TaintAnalyzer.sourceCalls.Add(source);

                        instance.IsTainted = true;
                        instance.sourceFrom = source;
                    }
                    else if (TaintAnalyzer.IsSink(constructorSymbol as IMethodSymbol))
                    {
                        TaintResult sink = new TaintResult(constructorSymbol, operation, instance);
                        TaintAnalyzer.sinkCalls.Add(sink);
                    }


                    instance.TargetSymbol = constructorSymbol as IMethodSymbol;



                    return instance;
                }

                Console.WriteLine($"================ DataFlowAnalysis AnalyzeConstructor :: {constructorSymbol}");

                instance = new DataFlowAnalysisValue();
                instance.Type = constructorSymbol.ContainingType;
                instance.IsInstance = true;
                // private public 검사가 필요할지 말지

                while (true)
                {
                    if (node is ClassDeclarationSyntax || node is StructDeclarationSyntax || node is InterfaceDeclarationSyntax)
                        break;
                    node = node.Parent;
                }

                SyntaxList<MemberDeclarationSyntax> members;
                if (node is ClassDeclarationSyntax classDeclaration)
                {
                    members = classDeclaration.Members;
                }
                else if (node is StructDeclarationSyntax structDeclaration)
                {
                    members = structDeclaration.Members;
                }
                else if (node is InterfaceDeclarationSyntax interfaceDeclaration)
                {
                    members = interfaceDeclaration.Members;
                }

                foreach (var mem in members)
                {
                    if (mem is FieldDeclarationSyntax field)
                    {
                        var name = field.Declaration.Variables.Single().Identifier.ToString();

                        if (field.Declaration.Variables.Single().Initializer != null)
                        {
                            var value = this.semanticModel.GetConstantValue(field.Declaration.Variables.Single().Initializer.Value).Value;
                            var type = this.semanticModel.GetTypeInfo(field.Declaration.Variables.Single().Initializer.Value).Type;
                            if (value == null)
                            {
                                IMethodSymbol symbol = this.semanticModel.GetSymbolInfo(field.Declaration.Variables.Single().Initializer.Value).Symbol as IMethodSymbol;
                                if (symbol != null)
                                {
                                    string clazz = symbol.ToString();
                                    if (DataFlowAnalyzer.Is_XamarinAPI(clazz))
                                    {
                                        value = field.Declaration.Variables.Single().Initializer.Value;
                                    }
                                }
                                else
                                {
                                    value = field.Declaration.Variables.Single().Initializer.Value;
                                }
                            }

                            DataFlowAnalysisValue member = new DataFlowAnalysisValue(type, name);
                            member.AddValue(value);
                            instance.AddMember(member);
                        }
                    }
                    else if (mem is PropertyDeclarationSyntax property)
                    {
                        if (property.ExpressionBody != null)
                        {
                            IOperation test2 = this.semanticModel.GetOperation(property.ExpressionBody);
                            if (property.ExpressionBody != null)
                            {
                                var name = property.Identifier.Text;

                                if (property.ExpressionBody.Expression != null)
                                {
                                    var value = this.semanticModel.GetConstantValue(property.ExpressionBody.Expression).Value;
                                    var type = this.semanticModel.GetTypeInfo(property.ExpressionBody.Expression).Type;
                                    if (value == null)
                                    {
                                        value = property.ExpressionBody.Expression;
                                    }
                                    DataFlowAnalysisValue member = new DataFlowAnalysisValue(type, name);
                                    member.AddValue(value);
                                    instance.AddMember(member);
                                }
                            }
                        }
                    }

                }

                if (blockResultMap.ContainsKey(cfg.Blocks.Single(b => b.Kind == BasicBlockKind.Entry).Ordinal))
                {
                    var entry = blockResultMap[cfg.Blocks.Single(b => b.Kind == BasicBlockKind.Entry).Ordinal];

                    foreach (var member in instance.GetMembers())
                    {
                        if (entry.operationResultMap[-1].dataValueMap.ContainsKey(member.Name))
                        {
                            entry.operationResultMap[-1].dataValueMap.TryGetValue(member.Name, out DataFlowAnalysisValue val);
                            val.Join(member);
                        }
                        else
                        {
                            entry.operationResultMap[-1].dataValueMap.Add(member.Name, member);
                        }


                    }
                }
                else
                {
                    var entry = new DataFlowAnalysisBlockResult(cfg.Blocks.Single(b => b.Kind == BasicBlockKind.Entry));
                    var args = new DataFlowAnalysisOperationResult();
                    foreach (var member in instance.GetMembers())
                    {
                        args.dataValueMap.Add(member.Name, member);
                    }
                    entry.SetParametersOnEntry(args);
                    blockResultMap.Add(entry.basicBlock.Ordinal, entry);
                }

                // constructor dataflow analysis
                worklist = new SortedSet<int>(cfg.Blocks.Select(b => b.Ordinal));
                while (worklist.Count > 0)
                {
                    int idx = worklist.Min;

                    if (cfg.Blocks[idx].Kind == BasicBlockKind.Entry)
                    {
                        worklist.Remove(idx);
                        continue;
                    }

                    blockResultMap.Add(idx, new DataFlowAnalysisBlockResult(stackTrace, blockResultMap, cfg.Blocks[idx]));
                    blockResultMap[idx].AnalyzeOperations();

                    worklist.Remove(idx);
                }
                //

                foreach (var valuemap in blockResultMap.Last().Value.GetLastOperationResult().dataValueMap)
                {
                    instance.AddMember(valuemap.Value);
                }

                for (int i = 0; targetConstructor.ParameterList.Parameters.Count > i; i++)
                {
                    var name = targetConstructor.ParameterList.Parameters[i].Identifier.ToString();
                    instance.RemoveMember(name);
                }


                if (TaintAnalyzer.IsSource(constructorSymbol as IMethodSymbol) && TaintAnalyzer.IsSink(constructorSymbol as IMethodSymbol))
                {
                    TaintResult Both = new TaintResult(constructorSymbol, operation, instance);
                    TaintAnalyzer.sourceCalls.Add(Both);

                    instance.IsTainted = true;
                    instance.sourceFrom = Both;
                }
                else if (TaintAnalyzer.IsSource(constructorSymbol as IMethodSymbol))
                {
                    TaintResult source = new TaintResult(constructorSymbol, operation);
                    TaintAnalyzer.sourceCalls.Add(source);

                    instance.IsTainted = true;
                    instance.sourceFrom = source;
                }
                else if (TaintAnalyzer.IsSink(constructorSymbol as IMethodSymbol))
                {
                    TaintResult sink = new TaintResult(constructorSymbol, operation, instance);
                    TaintAnalyzer.sinkCalls.Add(sink);
                }


                instance.TargetSymbol = constructorSymbol as IMethodSymbol;


            }

            return instance;
        }
        public DataFlowAnalysisOperationResult GetResult()
        {
            int lastBlock = blockResultMap.Count;
            this.blockResultMap.TryGetValue(lastBlock, out DataFlowAnalysisBlockResult lastBlockResult);
            var lastOperationResult = lastBlockResult.GetLastOperationResult();

            return lastOperationResult;
        }
    }
}
