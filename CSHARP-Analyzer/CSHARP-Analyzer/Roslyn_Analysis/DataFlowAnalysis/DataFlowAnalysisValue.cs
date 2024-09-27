using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.VisualStudio.Utilities;
using Roslyn_Analysis.TaintAnalysis;

namespace Roslyn_Analysis.DataFlowAnalysis
{
    public class DataFlowAnalysisValue
    {
        public ITypeSymbol Type{ get; set; }
        public string Name { get; set; }
        public IOperation Operation { get; set; }
        public HashSet<Object> ValuePool = new HashSet<Object>();
        public bool IsTainted { get; set; } = false;
        public TaintResult sourceFrom { get; set; } = null;
        public bool IsArgs { get; set; } = false;
        public bool IsLocal { get; set; } = false;
        public bool IsInstance { get; set; } = false;
        public bool IsInvocation { get; set; } = false;
        public IMethodSymbol TargetSymbol { get; set; } = null;
        public DataFlowAnalysisValue BaseInstance { get; set; } = null;
        public HashSet<DataFlowAnalysisValue> Parameters { get; set; }

        private PooledDictionary<string, DataFlowAnalysisValue> memberValueMap = PooledDictionary<string, DataFlowAnalysisValue>.GetInstance();

     
        public DataFlowAnalysisValue()
        {

        }
        public DataFlowAnalysisValue(ITypeSymbol type, string name)
        {
            this.Type = type;
            this.Name = name;
        }
        public DataFlowAnalysisValue(Object value)
        {
            this.AddValue(value);
        }
        public DataFlowAnalysisValue(ITypeSymbol returnType, string invocation, HashSet<DataFlowAnalysisValue> parameters)
        {
            this.Type = returnType;
            this.Name = "None";
            this.Parameters = parameters;
            this.IsInvocation = true;
            this.ValuePool.Add(invocation);
            
        }
        public DataFlowAnalysisValue(DataFlowAnalysisValue original)
        {
            if(original != null)
            {
                this.Type = original.Type;
                this.Name = original.Name;
                this.IsInstance = original.IsInstance;
                this.IsInvocation = original.IsInvocation;
                this.IsLocal = original.IsLocal;
                this.IsTainted = original.IsTainted;
                this.BaseInstance = original.BaseInstance;
                this.TargetSymbol = original.TargetSymbol;
                foreach (var value in original.ValuePool)
                {
                    this.ValuePool.Add(value);
                }
                foreach (var member in original.memberValueMap)
                {
                    this.memberValueMap.Add(member.Key, member.Value);
                }
            } 
        }
        public bool AddValue(Object value)
        {
            try
            {
                if(value is DataFlowAnalysisValue dfaValue)
                {
                    foreach (var val in dfaValue.ValuePool)
                    {
                        ValuePool.Add(val);
                    }
                }
                else
                {
                    ValuePool.Add(value);
                }   
            }
            catch
            {
                return false;
            }
            return true;
        }
        public bool AddMember(DataFlowAnalysisValue member)
        {
            try
            {
                if (this.IsInstance)
                {
                    if (this.memberValueMap.ContainsKey(member.Name))
                    {
                        this.memberValueMap[member.Name].Join(member);
                    }
                    else
                    {
                        this.memberValueMap.Add(member.Name, member);
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
        public bool RemoveMember(string memberName)
        {
            if (this.IsInstance)
            {
                if (this.memberValueMap.ContainsKey(memberName))
                {
                    this.memberValueMap.Remove(memberName);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        public void SetMemberValue(string name, DataFlowAnalysisValue value)
        {
            if (memberValueMap.ContainsKey(name))
            {
                memberValueMap.Remove(name);
            }
            memberValueMap.Add(name, value);
        }
        public DataFlowAnalysisValue GetMemberValue(string name)
        {
            if (IsInstance && memberValueMap.TryGetValue(name, out DataFlowAnalysisValue member))
            {
                return member;
            }
            else
            {
                DataFlowAnalysisValue res = new DataFlowAnalysisValue();
                res.Name = name;
                foreach(var value in ValuePool)
                {
                    res.AddValue($"{value.ToString()}.name");
                }
                
                return res;
            }
        }
        public HashSet<DataFlowAnalysisValue> GetMembers()
        {
            HashSet<DataFlowAnalysisValue> res = new HashSet<DataFlowAnalysisValue>();
            if (IsInstance)
            {
                foreach (var value in memberValueMap.Values)
                {
                    res.Add(value);
                }
            }
            return res; 
        }
        public DataFlowAnalysisValue InvokeSkipAnalysis(IMethodSymbol targetSymbol, HashSet<DataFlowAnalysisValue> parameters, IOperation rootOperation)
        {
            ITypeSymbol ret = targetSymbol.ReturnType;
            DataFlowAnalysisValue result = null;
            string invocation = "";
            
            List<string> paramStrs = new List<string>();
            string parameterStr = "";
            if (this.Parameters != null)
            {
                foreach (var parameter in this.Parameters)
                {
                    paramStrs.Add($"[{String.Join(",", parameter.ValuePool)}]");
                }
                parameterStr = String.Join(", ", paramStrs);
            }
            
            if (this.ValuePool.Count != 0)
            {
                foreach (var val in this.ValuePool)
                {
                    if (this.IsInvocation)
                    {
                        if (val is IOperation op)
                        {
                            invocation = $"{op.Syntax.ToString().Substring(0, op.Syntax.ToString().Length - 1)}{parameterStr}).{targetSymbol.Name}()";
                        }
                        else
                        {
                            invocation = $"{val.ToString().Substring(0, val.ToString().Length - 1)}{parameterStr}).{targetSymbol.Name}()";
                        }
                        result = new DataFlowAnalysisValue(ret, invocation, parameters);
                    }
                    else
                    {
                        if (val is IOperation op)
                        {
                            invocation = $"{op.Syntax.ToString()}.{targetSymbol.Name}()";
                        }
                        else
                        {
                            if (val == null)
                            {
                                invocation = $"{targetSymbol.Name}()";
                            }
                            else
                            {
                                invocation = $"{val.ToString()}.{targetSymbol.Name}()";

                            }
                        }
                        result = new DataFlowAnalysisValue(ret, invocation, parameters);
                    }
                }
            }
            else
            {
                invocation = $"{targetSymbol.ContainingSymbol}/{targetSymbol.Name}({parameterStr})";
                result = new DataFlowAnalysisValue(ret, invocation, parameters);
            }


            if (targetSymbol != null && result != null)
            {
                if (TaintAnalyzer.IsSource(targetSymbol) && TaintAnalyzer.IsSink(targetSymbol))
                {
                    TaintResult both = new TaintResult(targetSymbol, rootOperation);
                    TaintAnalyzer.bothCalls.Add(both);
                    result.IsTainted = true;
                    result.sourceFrom = both;
                }
                else if (TaintAnalyzer.IsSource(targetSymbol))
                {
                    TaintResult source = new TaintResult(targetSymbol, rootOperation);
                    TaintAnalyzer.sourceCalls.Add(source);
                    result.IsTainted = true;
                    result.sourceFrom = source;
                }
                else if (TaintAnalyzer.IsSink(targetSymbol))
                {
                    TaintResult sink = new TaintResult(targetSymbol, rootOperation, result);
                    TaintAnalyzer.sinkCalls.Add(sink);
                }


                if (this.IsTainted && !TaintAnalyzer.IsSource(targetSymbol))
                {
                    result.IsTainted = true;
                    result.sourceFrom = this.sourceFrom;
                }
                result.TargetSymbol = targetSymbol;
            }
            return result;
        }
        public DataFlowAnalysisValue InvokeMemberMethod(Stack<string> stackTrace,IMethodSymbol targetSymbol, MethodDeclarationSyntax targetMethod, HashSet<DataFlowAnalysisValue> parameters, IOperation rootOperation) 
        {
            var clazz = targetSymbol.ContainingNamespace.ToString() + targetSymbol.ContainingSymbol.ToString();
            this.TargetSymbol = targetSymbol;
            DataFlowAnalysisValue result = null;
            if (DataFlowAnalyzer.Is_XamarinAPI(clazz) || DataFlowAnalyzer.IsWrapper(targetSymbol))
            {
                result = this.InvokeSkipAnalysis(targetSymbol, parameters, rootOperation);
            }
            else
            {
                if (targetMethod != null)
                {
                    foreach (var mem in this.memberValueMap)
                    {
                        parameters.Add(mem.Value);
                    }


                    DataFlowAnalyzer subDataFlowAnalyzer = new DataFlowAnalyzer(stackTrace, targetMethod, parameters);
                    var methodResult = subDataFlowAnalyzer.AnalyzeInstanceMethod(new DataFlowAnalysisValue(this));
                    foreach (var modifiedMember in methodResult.Item1.memberValueMap)
                    {
                        if (this.memberValueMap.ContainsKey(modifiedMember.Key))
                        {
                            memberValueMap[modifiedMember.Key] = modifiedMember.Value;
                        }
                    }
                    result = methodResult.Item2;
                }
                else
                {
                    return null;
                }
            }
            if(result != null)
            {

                if (TaintAnalyzer.IsSource(targetSymbol) && TaintAnalyzer.IsSink(targetSymbol))
                {
                    TaintResult both = new TaintResult(targetSymbol, rootOperation);
                    TaintAnalyzer.bothCalls.Add(both);
                    result.IsTainted = true;
                    result.sourceFrom = both;
                }
                else if (TaintAnalyzer.IsSource(targetSymbol))
                {
                    TaintResult source = new TaintResult(targetSymbol, rootOperation);
                    TaintAnalyzer.sourceCalls.Add(source);
                    result.IsTainted = true;
                    result.sourceFrom = source;
                }
                else if (TaintAnalyzer.IsSink(targetSymbol))
                {
                    TaintResult sink = new TaintResult(targetSymbol, rootOperation, result);
                    TaintAnalyzer.sinkCalls.Add(sink);
                }


                if (this.IsTainted && !TaintAnalyzer.IsSource(targetSymbol))
                {
                    result.IsTainted = true;
                    result.sourceFrom = this.sourceFrom;
                }
                result.TargetSymbol = targetSymbol;



            }
           
            return result;
        }
        public DataFlowAnalysisValue InvokeMemberAccessor(Stack<string> stackTrace, string member, AccessorDeclarationSyntax targetAccessor, DataFlowAnalysisValue value)
        {
            if(value == null)
            {
                if (targetAccessor == null)
                {
                    var res = this.GetMemberValue(member);

                    return res;
                }
                else
                {
                    DataFlowAnalyzer subDataFlowAnalyzer = new DataFlowAnalyzer(stackTrace, targetAccessor, this.GetMembers());
          
                    (DataFlowAnalysisValue, DataFlowAnalysisValue) AccesorResult = subDataFlowAnalyzer.AnalyzeAccessor(this);
                    this.memberValueMap = AccesorResult.Item1.memberValueMap;
                    return AccesorResult.Item2;
                }
            }
            else
            {
                if (targetAccessor == null)
                {
                    this.SetMemberValue(member, value);
                }
                else
                {
                    HashSet<DataFlowAnalysisValue> parameters = this.GetMembers();
                    value.Name = "value";
                    parameters.Add(value);
                    DataFlowAnalyzer subDataFlowAnalyzer = new DataFlowAnalyzer(stackTrace, targetAccessor, parameters);
      
                    (DataFlowAnalysisValue, DataFlowAnalysisValue) AccesorResult = subDataFlowAnalyzer.AnalyzeAccessor(this);
                    this.memberValueMap = AccesorResult.Item1.memberValueMap;
                }
                return null;
            }   
        }
        public void PrintNow(int depth)
        {   
            Console.ResetColor();
            string tap = "\t";
            for(int i=0; i<depth; i++)
            {
                tap += "\t";
            }


            string res = $"{tap}";
            if (this.IsTainted)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }

            res += $"{this.Type} {this.Name} = [ ";

            if (IsInstance && !IsInvocation)
            {
                if (this.Name == "this") { }
                else
                {
                    string instance = $"{tap}";
                    foreach (var val in ValuePool)
                    {
                        if (val != null)
                        {
                            if (val is IOperation operation)
                            {
                                instance += $"{operation.Syntax.ToString()}, ";
                            }
                            else
                            {
                                instance += $"{val.ToString()}, ";
                            }
                        }
                    }
                    instance = instance.Substring(0, instance.Length - 2);
                    instance += " ]";
                    Console.WriteLine($"{tap}=========== Instance ===========");


                    foreach (var member in memberValueMap)
                    {
                        member.Value.PrintNow(depth + 1);
                    }
                    Console.WriteLine($"{tap}======== End of Instance ========");
                }
            }



            if (IsInvocation == true)
            {
                string invoke = $"{tap}{this.Type} {this.Name} = [ ";
                foreach (var val in ValuePool)
                {
                    if (val != null)
                    {
                        invoke += $"{val.ToString()}, ";
                    }
                }
                invoke = invoke.Substring(0, invoke.Length - 2);
                invoke += " ]";
                Console.WriteLine($"{tap}=========== Invocation ===========");
                Console.WriteLine($"{invoke}");
                Console.ResetColor();
                Console.WriteLine($"{tap}  Parameters :: ");
                int paramCnt = 0;
                if (Parameters != null)
                {
                    foreach (var parameter in Parameters)
                    {
                        parameter.Name = $"parameter{paramCnt++}";
                        parameter.PrintNow(depth + 1);
                    }
                }
                Console.WriteLine($"{tap}  End of parmeters ");
                Console.WriteLine($"{tap}======== End of Invocation ========");
            }
            else
            {
                if (this.ValuePool.Count == 0)
                {
                    return;
                }
                foreach (var val in ValuePool)
                {
                    if (val != null)
                        res += $"{val.ToString()}, ";
                }
                res = res.Substring(0, res.Length - 2);
                res += " ]";
                Console.WriteLine(res);
            }

            Console.ResetColor();


        }
        public bool TryConvertToInt64(out HashSet<ulong> convertedValuePool)
        {
            convertedValuePool = new HashSet<ulong>();

            try
            {
                foreach (var value in ValuePool)
                {
                    switch (Type.SpecialType)
                    {
                        case SpecialType.System_Int16:
                            convertedValuePool.Add(unchecked((ulong)(short)value));
                            break;
                        case SpecialType.System_Int32:
                            convertedValuePool.Add(unchecked((ulong)(int)value));
                            break;
                        case SpecialType.System_Int64:
                            convertedValuePool.Add(unchecked((ulong)(long)value));
                            break;
                        case SpecialType.System_UInt16:
                            convertedValuePool.Add((ushort)value);
                            break;
                        case SpecialType.System_UInt32:
                            convertedValuePool.Add((uint)value);
                            break;
                        case SpecialType.System_UInt64:
                            convertedValuePool.Add((ulong)value);
                            break;
                        case SpecialType.System_Byte:
                            convertedValuePool.Add((byte)value);
                            break;
                        case SpecialType.System_SByte:
                            convertedValuePool.Add(unchecked((ulong)(sbyte)value));
                            break;
                        case SpecialType.System_Char:
                            convertedValuePool.Add((char)value);
                            break;
                        case SpecialType.System_Boolean:
                            convertedValuePool.Add((ulong)((bool)value ? 1 : 0));
                            break;
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        public bool TryConvertToDouble(out HashSet<double> convertedValuePool)
        {
            
            convertedValuePool = new HashSet<double>();

            try
            {
                foreach (var value in ValuePool)
                {
                    double convertValue = Convert.ToDouble(value);
                    convertedValuePool.Add(convertValue);
                }
            }
            catch
            {
                return false;
            }
            return true;
        }
        public void Join(DataFlowAnalysisValue joinValue)
        {
            this.BaseInstance = joinValue.BaseInstance;
            this.IsLocal = joinValue.IsLocal;
            this.IsInstance = joinValue.IsInstance;
            this.IsInvocation = joinValue.IsInvocation;
            this.IsTainted = joinValue.IsTainted;
            this.TargetSymbol = joinValue.TargetSymbol;
            this.Operation = joinValue.Operation;
            this.sourceFrom = joinValue.sourceFrom;
            this.Parameters = joinValue.Parameters;

            if (joinValue == null)
                return;

            foreach (var data in joinValue.ValuePool)
            {
                this.ValuePool.Add(data);
            }

            if (IsInstance)
            {
                foreach (var member in joinValue.memberValueMap)
                {
                    if (this.memberValueMap.ContainsKey(member.Key))
                    {
                        memberValueMap.TryGetValue(member.Key, out DataFlowAnalysisValue value1);
                        joinValue.memberValueMap.TryGetValue(member.Key, out DataFlowAnalysisValue value2);

                        value1.ValuePool.Add(value2);
                    }
                    else
                    {
                        joinValue.memberValueMap.TryGetValue(member.Key, out DataFlowAnalysisValue value);
                        this.memberValueMap.Add(value.Name, value);
                    }
                }
            }

        }
        public static DataFlowAnalysisValue CalculateBinaryOperation(IBinaryOperation binary, DataFlowAnalysisValue value1, DataFlowAnalysisValue value2)
        {
            // Caclulate not constant values in right side
            DataFlowAnalysisValue res = new DataFlowAnalysisValue();
            res.Type = binary.Type;

            if (value1 != null && value2 != null && value1.Type != null && value2.Type != null)
            {
                if (value1.TryConvertToInt64(out HashSet<ulong> convertedValuePool1) && value2.TryConvertToInt64(out HashSet<ulong> convertedValuePool2))
                {
                    foreach (var val1 in convertedValuePool1)
                    {
                        foreach (var val2 in convertedValuePool2)
                        {
                            switch (binary.OperatorKind)
                            {
                                case BinaryOperatorKind.Add:
                                    res.AddValue(val1 + val2);
                                    break;
                                case BinaryOperatorKind.Subtract:
                                    res.AddValue(val1 - val2);
                                    break;
                                case BinaryOperatorKind.Multiply:
                                    res.AddValue(val1 * val2);
                                    break;
                                case BinaryOperatorKind.Divide:
                                    if (val2 != 0)
                                        res.AddValue(val1 / val2);
                                    break;
                            }
                        }
                    }
                }
                else if (value1.TryConvertToDouble(out HashSet<double> convertedValueDouble1) && value2.TryConvertToDouble(out HashSet<double> convertedValueDouble2))
                {
                    foreach (double convertVal1 in convertedValueDouble1)
                    {
                        foreach (double convertVal2 in convertedValueDouble2)
                        {
                            
                            switch (binary.OperatorKind)
                            {
                                case BinaryOperatorKind.Add:
                                    res.AddValue(convertVal1 + convertVal2);
                                    break;
                                case BinaryOperatorKind.Subtract:
                                    res.AddValue(convertVal1 - convertVal2);
                                    break;
                                case BinaryOperatorKind.Multiply:
                                    res.AddValue(convertVal1 * convertVal2);
                                    break;
                                case BinaryOperatorKind.Divide:
                                    res.AddValue(convertVal1 / convertVal2);
                                    break;
                            }
                        }
                    }

                }
                else if (value1.Type.SpecialType == SpecialType.System_String && value2.Type.SpecialType == SpecialType.System_String)
                {
                    res.Type = value1.Type;
                    foreach (object val1 in value1.ValuePool)
                    {
                        foreach (object val2 in value2.ValuePool)
                        {
                            switch (binary.OperatorKind)
                            {
                                case BinaryOperatorKind.Add:
                                    res.AddValue(val1.ToString() + val2.ToString());
                                    break;
                            }
                        }
                    }
                }

            }

            if (res.ValuePool.Count == 0)
            {
                res.AddValue(binary.Syntax.ToString());
            }
            if((value1 != null && value1.IsTainted) || (value2 != null && value2.IsTainted))
            {
                if (value1 != null && value1.IsTainted)
                    res.sourceFrom = value1.sourceFrom;
                if (value2 != null && value2.IsTainted)
                    res.sourceFrom = value2.sourceFrom;

                res.IsTainted = true;
            }

            return res;
        }
    }
}