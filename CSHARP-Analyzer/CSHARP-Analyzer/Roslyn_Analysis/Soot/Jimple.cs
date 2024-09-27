using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;
using Roslyn_Analysis.DataFlowAnalysis;
using Roslyn_Analysis.Graph;
using Roslyn_Analysis.TaintAnalysis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Net;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace Roslyn_Analysis.Jimple
{
    public class JimpleBody
    {
        private int LocalCnt = 0;
        public Dictionary<int, Local> Locals { get; private set; } = new Dictionary<int, Local>();
        public List<Stmt> Stmts { get; private set; } = new List<Stmt>();


        public void AddLocal(int hashcode, Local local)
        {
            if(Locals.ContainsKey(hashcode)) return;
            Locals.Add(hashcode, local);   
        }
        public void AddStmtUnit(Stmt stmt) =>  this.Stmts.Add(stmt);
        
        public JimpleBody()
        {
            Locals.Add(0, new Local("this", "this"));
        }
        public JimpleBody(JimpleBody original)
        {
            this.Locals = original.Locals;
            this.Stmts = original.Stmts;
        }
        public bool Validate()
        {
            //TODO
            return true;
        }
        public void Extract_JimpleUnits(DataFlowAnalysisValue value)
        {
            int hashcode = value.GetHashCode();

            if (value.Name == "this")
            {
                // Do Nothing?
            }
            else if (value.IsInvocation == true)
            {
                string tgtKey = TaintAnalyzer.GetCallgraphKey(value.TargetSymbol);
               
                if (DataFlowAnalyzer.IsWrapper(value.TargetSymbol) && TaintAnalyzer.taintCallgraph.GetNode(tgtKey).IsJava)
                {
                    List<Local> parameters = ResolveParameters(value);
                    HashSet<CallgraphEdge> outEdges = Callgraph.CallGraph.callgraph.GetOutgoingEdges(tgtKey);
                    CallgraphNode tgtWrapper = null;

                    foreach (CallgraphEdge outEdge in outEdges)
                    {
                        if (outEdge.tgt.IsWrapper)
                        {
                            tgtWrapper = outEdge.tgt;
                            break;
                        }
                    }
        
                    if (value.IsInstance == true)
                    {
                        // value is Constructor.
                        Local local = new Local($"$r{LocalCnt++}", tgtWrapper.Clazz);
                        this.AddLocal(hashcode, local);
                        this.AddStmtUnit(new AssignmentStmt(local, new NewStmt(tgtWrapper.Clazz)));
                        this.AddStmtUnit(new InvokeStmt(InvokeStmt._Special, local, tgtWrapper.GetSootSignatrue(), parameters));
                    }
                    else
                    {
                        // value is instance's member function Invocation.
                        if (value.BaseInstance != null)
                        {
                            Extract_JimpleUnits(value.BaseInstance);
                            if (TryGetLocal(value.BaseInstance, out Local @base) == true)
                            {
                                InvokeStmt invokeStmt = new InvokeStmt(InvokeStmt._Virtual, @base, tgtWrapper.GetSootSignatrue(), parameters);

                                if (value.Name.Contains("$invoke"))
                                {
                                    this.AddStmtUnit(invokeStmt);
                                }
                                else
                                {
                                    Local local = new Local($"$r{LocalCnt++}", tgtWrapper.ReturnType);
                                    this.AddLocal(hashcode, local);
                                    this.AddStmtUnit(new AssignmentStmt(local, invokeStmt));
                                }
                            }
                            else
                            {
                                Local baseInst = new Local($"$r{LocalCnt++}", tgtWrapper.Clazz);
                                this.AddLocal(hashcode, baseInst);
                                this.AddStmtUnit(new AssignmentStmt(baseInst, new NewStmt(tgtWrapper.Clazz)));
                     
                                InvokeStmt invokeStmt = new InvokeStmt(InvokeStmt._Virtual, baseInst, tgtWrapper.GetSootSignatrue(), parameters);

                                if (value.Name.Contains("$invoke"))
                                {
                                    this.AddStmtUnit(invokeStmt);
                                }
                                else
                                {
                                    Local local = new Local($"$r{LocalCnt++}", tgtWrapper.ReturnType);
                                    this.AddLocal(hashcode, local);
                                    this.AddStmtUnit(new AssignmentStmt(local, invokeStmt));
                                }
                            }
                        }
                        else
                        {
                            // value is static function invocation?
                            InvokeStmt invokeStmt = new InvokeStmt(InvokeStmt._Static, null, tgtWrapper.GetSootSignatrue(), parameters);
                            if (value.IsLocal == true)
                            {
                                if (value.TargetSymbol.IsStatic)
                                {
                                    Local local = new Local($"$r{LocalCnt++}", tgtWrapper.ReturnType);
                                    this.AddLocal(hashcode, local);
                                    this.AddStmtUnit(new AssignmentStmt(local, invokeStmt));
                                }
                                else
                                {
                                    Console.WriteLine("DEBUG");
                                }
                            }
                            else
                            {
                                this.AddStmtUnit(invokeStmt);
                            }
                        }
                    }
                }
                else
                {
                    // csharp
                     List<Local> parameters = ResolveParameters(value);

                    string sootSignature = null; 
                    if (TaintAnalyzer.IsSource(value.TargetSymbol))
                    {
                        sootSignature = "<dummyMainClass_csharp: java.lang.Object csSource()>";
                    }

                    if (TaintAnalyzer.IsSink(value.TargetSymbol))
                    {
                        sootSignature = "<dummyMainClass_csharp: void csSink(java.lang.Object)>";
                    }


                    InvokeStmt invokeStmt = new InvokeStmt(InvokeStmt._Static, null, sootSignature, parameters);

                    if (value.Name.Contains("$invoke"))
                    {
                        this.AddStmtUnit(invokeStmt);
                    }
                    else
                    {
                        Local local = new Local($"$r{LocalCnt++}", "java.lang.Object");
                        this.AddLocal(hashcode, local);
                        this.AddStmtUnit(new AssignmentStmt(local, invokeStmt));
                    }
                }
            }
            return;
        }
  
        private List<Local> ResolveParameters(DataFlowAnalysisValue value)
        {
            List<Local> parameters = new List<Local>();
            List<Stmt> stmts = new List<Stmt>();

            Stack<DataFlowAnalysisValue> stack = new Stack<DataFlowAnalysisValue>();
            List<DataFlowAnalysisValue> refs = new List<DataFlowAnalysisValue>();
            stack.Push( value );

            while(stack.Count > 0 )
            {
                DataFlowAnalysisValue now = stack.Pop();
                if( now.BaseInstance != null )
                {
                    stack.Push(now.BaseInstance);
                    refs.Add(now.BaseInstance);
                }
                if(now.Parameters != null ) {
                    foreach (DataFlowAnalysisValue parm in now.Parameters)
                    {
                        stack.Push(parm);
                        if (parm.IsTainted)
                        {
                            refs.Add(parm);
                        }
                    }
                }
            }
            refs.Reverse();

            
            foreach (DataFlowAnalysisValue _ref in refs)
            { 
                if (_ref.Name != null && !TryGetLocal(_ref, out Local tmp))
                {
                    Extract_JimpleUnits(_ref);    
                }
            }

            
            if (value.Parameters != null)
            {
                foreach (DataFlowAnalysisValue parameter in value.Parameters)
                {
                    if(parameter.IsTainted)
                    {
                        if (TryGetLocal(parameter, out Local tmp))
                        {
                            parameters.Add(tmp);
                        }
                    }
                    else
                    {
                        parameters.Add(null);
                    }
                }
            }
            return parameters;
        }

        private bool TryGetLocal(DataFlowAnalysisValue value, out Local res)
        {
          //s  Console.WriteLine($"\nTryGetLocal : \"{value.Name}\" hash code: {value.GetHashCode()}");\
        
            if(value.Name == null)
            {
                res = null;
                return false;
            }
            int hashcode = value.GetHashCode();
            if (value.Name.Equals("this"))
            {
                this.Locals.TryGetValue(0, out res);
                return true;
            }
            else
            {
                if (this.Locals.TryGetValue(hashcode, out res))
                    return true;
            }
            return false;
        }
        public (JArray, JArray) ToJSonArray()
        {   
            JArray localArray = new JArray();
            foreach(Local local in this.Locals.Values)
            {
                JObject jsonUnit = new JObject(
                    new JProperty("Name", local.Name),
                    new JProperty("Type", local.Type)
               );
                localArray.Add(jsonUnit);
            }


            JArray stmtArray = new JArray();
            foreach (Stmt st in this.Stmts)
            {
                stmtArray.Add(st.ToJSonUnit()); 
            }

            return (localArray, stmtArray);
        }
    
    }

    public abstract class Stmt
    {
        protected Stmt() { }

        public override string ToString()
        {
            return "test";
        }
        public virtual JObject ToJSonUnit()
        {
            JObject jsonUnit = new JObject(
                new JProperty("test", "test")
                );
            return jsonUnit;
        }
    }


    public class Local
    {
        public string Name { get; private set; }
        public string Type { get; private set; }

        public Local(string name, string type)
        {
            this.Name = name;
            this.Type = type;
        }
    }

    public class NewStmt : Stmt
    {
        public string Type { get; private set; }
        
        public NewStmt(string type) => this.Type = type;

        public override string ToString()
        {
            return $"new {Type}";
        }
        public override JObject ToJSonUnit()
        {
            JObject jsonUnit = new JObject(
                new JProperty("StmtKind", "new"),
                new JProperty("Type", this.Type)
                );
            return jsonUnit;
        }
    }
    public class AssignmentStmt : Stmt
    {
        public Local Local { get; private set; }
        public Stmt Stmt{ get; private set; }
        public AssignmentStmt(Local local, Stmt stmt)
        {
            Local = local;
            this.Stmt = stmt;
        }
        public override string ToString()
        {
            return $"{Local.Name} = {Stmt.ToString()}";
        }
        public override JObject ToJSonUnit()
        {
            JObject jsonUnit = new JObject(
                new JProperty("StmtKind", "assign"),
                new JProperty("Local", this.Local.Name),
                new JProperty("Stmt", Stmt.ToJSonUnit())
                );
            return jsonUnit;
        }
    }
    public class InvokeStmt : Stmt
    {
        public static readonly int _Virtual = 0x0001;
        public static readonly int _Interface = 0x0002;
        public static readonly int _Static = 0x0004;
        public static readonly int _Special = 0x0008;
        public static readonly int _Direct = 0x0010;

        public int Kind { get; private set; }
        public Local Base { get; private set; }
        public string TgtMethod { get; private set; }
        public List<Local> Parameters { get; private set; }

        public InvokeStmt(int kind, Local @base, string tgtMethod, List<Local> parameters)
        {
            Kind = kind;
            Base = @base;
            TgtMethod = tgtMethod;
            Parameters = parameters;
        }

        public override string ToString()
        {
            string parameter = "";
            foreach (Local param in Parameters)
            {
                if(param == null)
                {
                    parameter += $"null,";
                }
                else
                {
                    parameter += $"{param.Name},";
                }  
            }
            if (parameter.Length > 0)
            {
                parameter = parameter.Substring(0, parameter.Length - 1);
            }
            
            string res = "";
            if(Kind == _Virtual)
            {
                res = $"virtualinvoke {Base.Name}.{TgtMethod} ({parameter})";
            }
            if (Kind == _Static)
            {
                res = $"staticinvoke {TgtMethod} ({parameter})";
            }
            if (Kind == _Special)
            {
                res = $"specialinvoke {Base.Name}.{TgtMethod} ({parameter})";
            }
            if (Kind == _Direct)
            {
                res = $"directinvoke {Base.Name}.{TgtMethod} ({parameter})";

            }
            return res;
        }

        public override JObject ToJSonUnit()
        {
            JArray paramaters = new JArray();
     
            foreach (Local param in Parameters)
            {
                if (param == null)
                {
                    paramaters.Add("null");
                }
                else
                {
                    paramaters.Add(param.Name);
                }
            }
 
            
            JObject jsonUnit = null;
            if (Kind == _Virtual)
            {
                jsonUnit = new JObject(
                    new JProperty("StmtKind", "invoke"),
                    new JProperty("Kind", "Virtual"),
                    new JProperty("Base", Base.Name),
                    new JProperty("Tgt", TgtMethod),
                    new JProperty("Parameter", paramaters)
                    );
            }
            if (Kind == _Static)
            {
                jsonUnit = new JObject(
                    new JProperty("StmtKind", "invoke"),
                    new JProperty("Kind", "Static"),
                    new JProperty("Base", null),
                    new JProperty("Tgt", TgtMethod),
                    new JProperty("Parameter", paramaters)
                    );
            }
            if (Kind == _Special)
            {
                jsonUnit = new JObject(
                 new JProperty("StmtKind", "invoke"),
                 new JProperty("Kind", "Special"),
                 new JProperty("Base", Base.Name),
                 new JProperty("Tgt", TgtMethod),
                 new JProperty("Parameter", paramaters)
                 );
            }
            if (Kind == _Direct)
            {
                jsonUnit = new JObject(
                new JProperty("StmtKind", "invoke"),
                new JProperty("Kind", "Direct"),
                new JProperty("Base", Base.Name),
                new JProperty("Tgt", TgtMethod),
                new JProperty("Parameter", paramaters)
                );
            }
         
            return jsonUnit;
        }
    }
}
