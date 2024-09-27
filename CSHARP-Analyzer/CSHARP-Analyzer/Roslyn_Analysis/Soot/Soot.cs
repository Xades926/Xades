using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Roslyn_Analysis.Graph;
using Roslyn_Analysis.Jimple;
using Roslyn_Analysis.TaintAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace Roslyn_Analysis.Soot
{
    public class SootClass
    {
        public string clazz { get; private set; }
        public string superClass { get; private set; }
        public List<SootMethod> methodList { get; private set; } = new List<SootMethod>();

        public SootClass(string clazz)
        {
            this.clazz = clazz;
        }

        public SootClass(string clazz, string superClass)
        {
            this.clazz = clazz;
            this.superClass = superClass;
        }

        public void AddSootMethod(SootMethod method)
        {
            this.methodList.Add(method);
        }

        public SootClass JoinSootClass(SootClass copy)
        {
            bool check = false;
            foreach (SootMethod method1 in copy.methodList)
            {
                SootMethod copyTgt = null;
                check = false;
                foreach(SootMethod method2 in this.methodList)
                {
                    if (method1.Signature.Equals(method2.Signature)) {
                        copyTgt = method2;
                        check = true;
                        break;
                    }
                }
                if (check)
                {
                    methodList.Add(method1);
                }

            }
        
            return this;
        }

        public bool HasStmts()
        {
            bool res = false;

            foreach(SootMethod method in methodList)
            {
                if (method.ActiveBody.Stmts.Count != 0)
                    return true;
            }
            return res;
        }


        public JObject ToJson()
        {
            JArray methods = new JArray();

            JObject json = new JObject(
                new JProperty("Class", clazz),
                new JProperty("SuperClass", superClass),
                new JProperty("Methods", methods)
                );
            foreach (SootMethod method in methodList)
            {
                // Console.WriteLine($"Writing Jimple IR about {clazz} {superClass}");
                JObject methodJSON = method.ToJSON();
                methods.Add(methodJSON);
            }
            return json;
        }
    }

    public class SootMethod
    {
        public string Name { get; private set; }
       // public List<string> paremeterTypes { get; private set; } = new List<string>();
        public string Signature { get; private set; }
        public int Modifier { get; private set; }
        public JimpleBody ActiveBody { get; private set; } = new JimpleBody();

        public SootMethod(string name, string signature, int modifier)
        {
            this.Name = name;
            this.Signature = signature;
            this.Modifier = modifier;
        }

        public JObject ToJSON()
        {
            var res = this.ActiveBody.ToJSonArray();
            JArray localArray = res.Item1;
            JArray stmtArray = res.Item2;

            JObject jsonUnit = new JObject(
                         new JProperty("Name", Name),
                         new JProperty("Signature", Signature),
                         new JProperty("Modifier", Modifier),
                         new JProperty("Locals", localArray),
                         new JProperty("Stmts", stmtArray)
                    );

            return jsonUnit;
        }
        public static int GetSootModifier(CallgraphNode targetNode)
        {
            MethodDeclarationSyntax tgtSyntax = targetNode.syntax as MethodDeclarationSyntax;
            SyntaxTree root = targetNode.syntax.SyntaxTree;
            SemanticModel semanticModel = Program.Compilation.GetSemanticModel(root);

            IMethodSymbol methodSymbol = (IMethodSymbol)semanticModel.GetDeclaredSymbol(targetNode.syntax);

            int SootModifer = 0;
            var modifier = methodSymbol.DeclaredAccessibility;
            if (methodSymbol.IsStatic == true)
            {
                SootModifer |= 0x0008;
            }
            if (methodSymbol.IsAbstract == true)
            {
                SootModifer |= 0x0400;
            }

            if (modifier == Accessibility.Public)
            {
                SootModifer |= 0x0001;
            }
            else if (modifier == Accessibility.Private)
            {
                SootModifer |= 0x0002;
            }
            else if (modifier == Accessibility.Protected)
            {
                SootModifer |= 0x0004;
            }

            return SootModifer;
        }
    }

}
