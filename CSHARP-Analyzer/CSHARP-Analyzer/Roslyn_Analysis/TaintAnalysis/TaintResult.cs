using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;
using Roslyn_Analysis.Callgraph;
using Roslyn_Analysis.DataFlowAnalysis;
using Roslyn_Analysis.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Roslyn_Analysis.TaintAnalysis
{
    public class TaintResult
    {
        public ISymbol calleeSymbol { get; set; }
        public ISymbol callerSymbol { get; set; }
        public IOperation operation { get; set; }
        public DataFlowAnalysisValue data { get; set; }

        public TaintResult(ISymbol source, IOperation operation)
        {
            this.calleeSymbol = source;
            this.operation = operation;

            if (operation != null)
            {
                SyntaxTree syntaxTree = operation.Syntax.SyntaxTree;

                callerSymbol = CallGraph.ExtractSrcSymbol(Program.Compilation.GetSemanticModel(syntaxTree), operation.Syntax).Item1;
            }
            
        }

        public TaintResult(ISymbol source, IOperation operation, DataFlowAnalysisValue data)
        {
            this.calleeSymbol = source;
            this.operation = operation;
            this.data = data;
            
            SyntaxTree syntaxTree = operation.Syntax.SyntaxTree;

            callerSymbol = CallGraph.ExtractSrcSymbol(Program.Compilation.GetSemanticModel(syntaxTree), operation.Syntax).Item1;
        }

        public CallgraphNode GetCalleeNode()
        {
            string key = GetSignature(calleeSymbol as IMethodSymbol);

            return CallGraph.callgraph.GetNode(key);
        }
        public string GetSignature(IMethodSymbol methodSymbol)
        {
            if (methodSymbol == null)
                return "";
            string clazz = methodSymbol.ContainingSymbol.ToString();
            string func = methodSymbol.Name;

            string parameterTypes = "(";
            foreach (var param in methodSymbol.Parameters)
            {
                parameterTypes += $"{param.Type.ToString()},";
            }
            if (parameterTypes.Length > 1)
            {
                parameterTypes = parameterTypes.Substring(0, parameterTypes.Length - 1);
            }
            parameterTypes += ")";

            string returnType = methodSymbol.ReturnType.ToString();

            return $"{clazz}/{func} {parameterTypes}{returnType}";
        }

        public void Print(string kind, string Lang)
        {
            string calleeSignature = GetSignature(calleeSymbol as IMethodSymbol);
            string callerSignature = GetSignature(callerSymbol as IMethodSymbol);

            Console.Write($" {kind}: ");
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write($"{calleeSignature}");
            Console.ResetColor();
            Console.Write($" at ");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            if(operation != null)
                Console.Write($"{operation.Syntax.ToString()}");
            Console.ResetColor();
            Console.Write($" to {Lang} fuction ");
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write($"{callerSignature}");
            Console.ResetColor();
            Console.Write(".\n");
            //  string srcSignature = GetSignature(sourceSymbol as IMethodSymbol);
            // string callerSignature = GetSignature(callerSymbol as IMethodSymbol);
            //Console.WriteLine($"  Found source \"{srcSignature}\" at \"{operation.Syntax.ToString()}\" in \"{callerSignature}\"");
        }
        public void PrintLeak(string LANG)
        {
            string leak_calleeSignature = GetSignature(calleeSymbol as IMethodSymbol);
            string leak_callerSignature = GetSignature(callerSymbol as IMethodSymbol);

            Console.Write($" Leak: ");
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write($"{leak_calleeSignature}");
            Console.ResetColor();
            Console.Write($" at ");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write($"{operation.Syntax.ToString()}");
            Console.ResetColor();
            Console.Write($" to {LANG} function ");
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write($"{leak_callerSignature}");
            Console.ResetColor();
            Console.Write(".\n");

            Console.Write($"\tfrom Source: ");
         
            if (data.Parameters != null)
            {
                foreach (DataFlowAnalysisValue param in data.Parameters)
                {
                    if (param.IsTainted == true)
                    {
                        PrintFromSource(param.sourceFrom);
                    }
                }
            }
            Console.WriteLine();
        }
        private void PrintFromSource(TaintResult source)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write($"{GetSignature(source.calleeSymbol as IMethodSymbol)}");
            Console.ResetColor();
            Console.Write($" at ");
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.Write($"{source.operation.Syntax.ToString()}");
            Console.ResetColor();
            Console.Write($" in method ");
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Write($"{GetSignature(source.callerSymbol as IMethodSymbol)}");
            Console.ResetColor();
            Console.Write(".\n");
        }

        public JObject ToJson()
        {
            //Console.WriteLine($"  Found source \"{srcSignature}\" at \"{operation.Syntax.ToString()}\" in \"{callerSignature}\"");
            string calleeSignature = GetSignature(calleeSymbol as IMethodSymbol);
            string callerSignature = GetSignature(callerSymbol as IMethodSymbol);
            string operationSyntax = "";
            if (this.operation != null)
                operationSyntax = operation.Syntax.ToString();

            JObject jsonUnit = new JObject(
                    new JProperty("callee", calleeSignature),
                    new JProperty("caller", callerSignature),
                    new JProperty("operation", operationSyntax)
                    );

            return jsonUnit;
        }
        public JObject ToLeakJson()
        {

            string leak_calleeSignature = GetSignature(calleeSymbol as IMethodSymbol);
            string leak_callerSignature = GetSignature(callerSymbol as IMethodSymbol);

            JArray FromSources = new JArray();
            if (data.Parameters != null)
            {
                foreach (DataFlowAnalysisValue param in data.Parameters)
                {
                    if (param.IsTainted == true)
                    {
                        FromSources.Add(param.sourceFrom.ToJson());
                    }
                }
            }



            JObject leakUnit = new JObject(
                    new JProperty("callee", leak_calleeSignature),
                    new JProperty("caller", leak_callerSignature),
                    new JProperty("operation", operation.Syntax.ToString()),
                    new JProperty("from_sources", FromSources)
                   
                    );

            return leakUnit;
        }
    }

}

