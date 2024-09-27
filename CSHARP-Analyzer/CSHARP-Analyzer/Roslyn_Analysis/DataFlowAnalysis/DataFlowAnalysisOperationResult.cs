using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.VisualStudio.Utilities;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Xml.Linq;
using System.Windows.Input;
using Microsoft.CodeAnalysis.CSharp;
using Roslyn_Analysis.Callgraph;
using Roslyn_Analysis.TaintAnalysis;
using System;
using System.Reflection.Metadata;
using Newtonsoft.Json.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Diagnostics;

namespace Roslyn_Analysis.DataFlowAnalysis
{
    public class DataFlowAnalysisOperationResult
    {
        private uint invokeCnt = 0;
        private Stack<string> stackTrace;
        public readonly IOperation rootOperation;
        public PooledDictionary<string, DataFlowAnalysisValue> dataValueMap = PooledDictionary<string, DataFlowAnalysisValue>.GetInstance();
        public DataFlowAnalysisOperationResult(Stack<string> stackTrace, IOperation operation, DataFlowAnalysisOperationResult preResult)
        {
            this.stackTrace = stackTrace;
   
            this.rootOperation = operation;
            this.ClonePreOperation(preResult);

            // PrintOperationResult();
            this.Compute();
            // PrintOperationResult();

        }
        public DataFlowAnalysisOperationResult() {}

        public DataFlowAnalysisOperationResult(DataFlowAnalysisOperationResult preResult)
        {
            // BasicBlock Analysis Starting or Return
            this.ClonePreOperation(preResult);
        }
        public DataFlowAnalysisOperationResult(IOperation operation)
        {
            this.rootOperation = operation;
            this.Compute();
        }
        public void Compute()
        {
            DataFlowAnalysisValue data;
            if (rootOperation is ISimpleAssignmentOperation simpleAssignment)
            {
                data = SolveChaining(simpleAssignment.Value);
                AddNewData(simpleAssignment, data);
            }
            else if (rootOperation is IExpressionStatementOperation expression)
            {
                if (expression.Operation is ISimpleAssignmentOperation assignment)
                {
                    data = SolveChaining(assignment.Value);    
                    AddNewData(assignment, data);
                }
                else if (expression.Operation is IInvocationOperation invocation)
                {
                    DataFlowAnalysisValue val = null;
                
                    val = SolveChaining(invocation);

                    if (val != null)
                    {
                        val.Name = $"$invoke{invokeCnt++}";
                        while (dataValueMap.TryGetValue(val.Name, out DataFlowAnalysisValue tmp))
                        {
                            val.Name = $"$invoke{invokeCnt++}";
                        }
                        val.TargetSymbol = invocation.TargetMethod;
                        val.Operation = rootOperation;
                        dataValueMap.Add(val.Name, val);
                    }
                }
                else
                {
                  //  Console.WriteLine();
                }
            }
        }

        private void AddNewData(ISimpleAssignmentOperation assignment, DataFlowAnalysisValue data)
        {
            string targetInstance = null;
            string name = "";
            bool isLocal = false;
            var target = assignment.Target;


            if (target is IPropertyReferenceOperation property)
            {
                if (target.Syntax is MemberAccessExpressionSyntax member)
                {
                    if(property.Instance is ILocalReferenceOperation local) 
                    {
                        targetInstance = local.Local.Name;
                    }
                    else if(property.Instance is IInstanceReferenceOperation ins)
                    {
                        targetInstance = ins.ToString();
                    }
                }
                name = (property.Member as IPropertySymbol).Name;
            }
            else if (target is IFieldReferenceOperation field)
            {
                if (target.Syntax is MemberAccessExpressionSyntax && field.Instance != null)
                {
                    if (field.Instance.Syntax.ToString() != "this")
                    {
                        if (field.Instance is ILocalReferenceOperation local)
                        {
                            targetInstance = local.Local.Name;
                        }
                        else
                        {
                            targetInstance = field.Instance.ToString();
                        }
                    }
                }
                name = (field.Member as IFieldSymbol).Name;
            }
            else if (target is ILocalReferenceOperation local)
            {
                isLocal = true;
                name = local.Local.Name;
            }

            DataFlowAnalysisValue newData = data;
            if (newData != null)
            {
                newData.Name = name;
                newData.Operation = this.rootOperation;
                newData.IsLocal = isLocal;
                if (dataValueMap.ContainsKey(name))
                {
                    dataValueMap.Remove(name); // remove old data if data is exist
                }

                if (targetInstance == null)
                {
                    dataValueMap.Add(name, newData);
                }
                else
                {
                    dataValueMap.TryGetValue(targetInstance, out DataFlowAnalysisValue leftInstance);
                    if(leftInstance != null)
                    {
                        if (target is IFieldReferenceOperation)
                        {
                            leftInstance.SetMemberValue(name, newData);
                        }
                        if (target is IPropertyReferenceOperation tmp)
                        {
                            var left = tmp.Member as IPropertySymbol;
                            if (left != null)
                            {
                                var leftSetMethod = CallGraph.callgraph.GetNode(TaintAnalyzer.GetCallgraphKey(left.SetMethod));
                                if (leftSetMethod != null)
                                {
                                    var SetMethodSytax = leftSetMethod.syntax as AccessorDeclarationSyntax;
                                    leftInstance.InvokeMemberAccessor(stackTrace, left.Name, SetMethodSytax, newData);
                                    data.BaseInstance =  leftInstance;
                                }
                            }
                            else
                            {
                               // Console.WriteLine();
                            }
                        }
                    }
                    else
                    {
                        name = $"{targetInstance}.{name}";
                        if (dataValueMap.ContainsKey(name))
                        {
                            dataValueMap.Remove(name);
                        }
                        dataValueMap.Add(name, newData);
                    }
               
                }
            }
           

        }
        private HashSet<DataFlowAnalysisValue> ConvertArgs2Values(ImmutableArray<IArgumentOperation> args)
        {
            HashSet<DataFlowAnalysisValue> result = new HashSet<DataFlowAnalysisValue>();

            foreach (var arg in args)
            {
                DataFlowAnalysisValue argument = SolveChaining(arg.Value);
                argument.IsArgs = true;
                result.Add(argument);
            }
            return result;
        }
        public void ClonePreOperation(DataFlowAnalysisOperationResult preResult)
        {
            if (preResult != null)
            {
                foreach (var data in preResult.dataValueMap.Keys)
                {
                    if (dataValueMap.ContainsKey(data))
                    {
                        dataValueMap.TryGetValue(data, out DataFlowAnalysisValue value1);
                        preResult.dataValueMap.TryGetValue(data, out DataFlowAnalysisValue value2);

                        value1.Join(value2);
                    }
                    else
                    {
                        preResult.dataValueMap.TryGetValue(data, out DataFlowAnalysisValue value);
                        dataValueMap.Add(data, value);
                    }
                }
            }
        }
    
        public DataFlowAnalysisValue SolveChaining(IOperation operation)
        {
            DataFlowAnalysisValue result = null;
            IOperation operationCopy = operation;
            IOperation instance = null;
            Stack<IOperation> stack = new Stack<IOperation>();

            if (operationCopy is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            if (operation is IFieldReferenceOperation || operation is IPropertyReferenceOperation || operation is IInvocationOperation)
            {
                instance = operation;
                stack.Push(instance);
            }
          
            while (instance != null)
            {
                if (instance is IFieldReferenceOperation fieldReference)
                {
                    instance = fieldReference.Instance;
                }
                else if (instance is IPropertyReferenceOperation propertyReference)
                {
                    instance = propertyReference.Instance;
                }
                else if (instance is IInvocationOperation invocation)
                {
                    instance = invocation.Instance;
                }
                else
                {
                    break;
                }

                stack.Push(instance);
            }

            if (stack.Count == 0)
            {
                result = AnalyzeAnOperation(null, operation);
            }
            else
            {
                DataFlowAnalysisValue chain = null;
                while (stack.Count > 0)
                {
                    IOperation op = stack.Pop();

                    if (op != null)
                    {
                        chain = AnalyzeAnOperation(chain, op);
                    }
                }
                result = chain;
            }

            return result;
        }
        public DataFlowAnalysisValue AnalyzeAnOperation(DataFlowAnalysisValue instance, IOperation operation)
        {
            DataFlowAnalysisValue data = null;
        
            if(stackTrace == null)
            {
                stackTrace = new Stack<string>();
            }
            if (operation is ILiteralOperation literal)
            {
                data = new DataFlowAnalysisValue();
                data.Type = literal.Type;
                if (data.Type != null)
                {
                    if (data.Type.SpecialType == SpecialType.System_String)
                    {
                        data.AddValue($"\"{literal.ConstantValue.Value}\"");
                    }
                    else
                    {
                        data.AddValue(literal.ConstantValue.Value);
                    }
                }
            }
            else if (operation is IBinaryOperation binary)
            {
                DataFlowAnalysisValue value1;
                DataFlowAnalysisValue value2;
                // Caclulate not constant values in right side
                if (binary.LeftOperand.ConstantValue.HasValue == false)
                {
                    value1 = SolveChaining(binary.LeftOperand);
                }
                else
                {
                    value1 = new DataFlowAnalysisValue(binary.LeftOperand.ConstantValue.Value);
                    value1.Type = binary.LeftOperand.Type;
                }

                if (binary.RightOperand.ConstantValue.HasValue == false)
                {
                    value2 = SolveChaining(binary.RightOperand);
                }
                else
                {
                    value2 = new DataFlowAnalysisValue(binary.RightOperand.ConstantValue.Value);
                    value2.Type = binary.RightOperand.Type;
                }

                data = DataFlowAnalysisValue.CalculateBinaryOperation(binary, value1, value2);
            }
            else if (operation is ILocalReferenceOperation localReference)
            {
                if (dataValueMap.TryGetValue(localReference.Local.Name, out DataFlowAnalysisValue local))
                {
                    data = local;
                }
                else
                {
                    data = new DataFlowAnalysisValue();
                    data.Type = localReference.Type;
                    data.AddValue(localReference.Syntax);
                }
            }
            else if (operation is IParameterReferenceOperation parameter)
            {
                if(dataValueMap.TryGetValue((parameter.Syntax as IdentifierNameSyntax).Identifier.Text, out DataFlowAnalysisValue param))
                {
                    data = new DataFlowAnalysisValue(param);
                }
                else
                {
                    data = new DataFlowAnalysisValue();
                    data.Type = parameter.Type;
                    data.AddValue($"parameter : {(parameter.Syntax as IdentifierNameSyntax).Identifier.Text}");
                }
            }
            else if (operation is IInstanceReferenceOperation)
            {
                data = GetThisInstance();
            }
            else if (operation is IObjectCreationOperation creation)
            {
                HashSet<DataFlowAnalysisValue> parameters = ConvertArgs2Values(creation.Arguments);
             
                var clazz = creation.Constructor.ContainingNamespace.ToString() + creation.Constructor.ContainingSymbol.ToString();
                var node = CallGraph.callgraph.GetNode(TaintAnalyzer.GetCallgraphKey(creation.Constructor as IMethodSymbol));
                if (stackTrace != null && stackTrace.Contains(TaintAnalyzer.GetCallgraphKey(creation.Constructor as IMethodSymbol)))
                {
                    if (node.syntax != null)
                    {
                        stackTrace.Push(node.GetKey());
                        ConstructorDeclarationSyntax targetConstructor = node.syntax as ConstructorDeclarationSyntax;
                        DataFlowAnalyzer subDataflowAnalyzer = new DataFlowAnalyzer(stackTrace, targetConstructor, parameters);
                        data = subDataflowAnalyzer.AnalyzeConstructor(this.rootOperation);
                        stackTrace.Pop();
                        if (data != null)
                            data.Type = creation.Type;
                    }
                }
                else
                {
                    data = new DataFlowAnalysisValue();
                    data.Type = creation.Type;
                    data.AddValue(creation.Syntax);
                }
            }
            else if (operation is IFieldReferenceOperation fieldReference)
            {
                if(instance != null)
                {
                    var val = instance.GetMemberValue(fieldReference.Member.Name);
                    
                    if(val != null)
                    {
                        data = new DataFlowAnalysisValue(val);
                        data.Type = fieldReference.Type;
                        data.BaseInstance =  instance;
                    }
                    else
                    {
                       // Console.WriteLine();
                    }
                }
                else if(fieldReference.Instance is IInstanceReferenceOperation ThisInstance)
                {
                    DataFlowAnalysisValue value = GetThisInstance().GetMemberValue(fieldReference.Field.Name);
                    data = new DataFlowAnalysisValue(value);
                    data.BaseInstance =  GetThisInstance();
                }
                else if(fieldReference.Instance is ILocalReferenceOperation LocalInstance)
                {
                    if (dataValueMap.TryGetValue(LocalInstance.Local.Name, out DataFlowAnalysisValue rightInstance))
                    {
                        var field = (fieldReference.Member as ISymbol).Name;
                        data = new DataFlowAnalysisValue(rightInstance.GetMemberValue(field));
                        data.BaseInstance = rightInstance;
                    }
                }

            }
            else if (operation is IPropertyReferenceOperation propertyReference)
            { 
                var clazz = propertyReference.Property.ContainingNamespace.ToString() + propertyReference.Property.ContainingSymbol.ToString();
                if(instance != null)
                {
                    if (DataFlowAnalyzer.Is_XamarinAPI(clazz))
                    {
                        var property = propertyReference.Member as IPropertySymbol;
                        IMethodSymbol propertyGetMethod = property.GetMethod;

                        data = instance.InvokeSkipAnalysis(propertyGetMethod, null, this.rootOperation);
                        data.BaseInstance = instance;
                    }
                    else
                    {
                        var property = propertyReference.Member as IPropertySymbol;
                        if (propertyReference.Instance is IInstanceReferenceOperation)
                        {
                            if(dataValueMap.TryGetValue(property.Name, out DataFlowAnalysisValue tmp))
                            {
                                data = new DataFlowAnalysisValue(tmp);
                                data.BaseInstance =  GetThisInstance();
                            }
                        }
                        else if (propertyReference.Instance is ILocalReferenceOperation LocalInstance)
                        {
                            if (property.GetMethod != null)
                            {
                                var propertyGetMethodNode = CallGraph.callgraph.GetNode(TaintAnalyzer.GetCallgraphKey(property.GetMethod));           
                                if (stackTrace != null && stackTrace.Contains(TaintAnalyzer.GetCallgraphKey(property.GetMethod)))
                                {
                                    data = new DataFlowAnalysisValue();
                                    data.Type = operation.Type;
                                    data.AddValue(operation.Syntax);
                                }
                                else { 
                                    if (propertyGetMethodNode != null)
                                    {
                                        //getter
                                        AccessorDeclarationSyntax propertyGetMethod = propertyGetMethodNode.syntax as AccessorDeclarationSyntax;
                             
                                        stackTrace.Push(TaintAnalyzer.GetCallgraphKey(property.GetMethod));
                                        if (DataFlowAnalyzer.IsWrapper(property.GetMethod))
                                        {
                                            data = instance.InvokeSkipAnalysis(property.GetMethod, null, this.rootOperation);
                                            stackTrace.Pop();
                                            data.BaseInstance = instance;
                                        }
                                        else
                                        {
                                            data = instance.InvokeMemberAccessor(stackTrace, property.Name, propertyGetMethod, null);
                                            stackTrace.Pop();
                                            data.BaseInstance = instance;
                                        }
                                    }
                                    else
                                    {
                                        stackTrace.Push(TaintAnalyzer.GetCallgraphKey(property.SetMethod));
                                        data = instance.InvokeMemberAccessor(stackTrace, property.Name, null, null);
                                        stackTrace.Pop();
                                        data.BaseInstance =  instance;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (DataFlowAnalyzer.Is_XamarinAPI(clazz))
                    {
                        
                        var property = propertyReference.Member as IPropertySymbol;
                        IMethodSymbol propertyGetMethod = property.GetMethod;

                        if (propertyGetMethod.IsStatic)
                        {
                            DataFlowAnalysisValue _static = new DataFlowAnalysisValue(clazz);
                            _static.Name = clazz;
                            data = _static.InvokeSkipAnalysis(propertyGetMethod, null, this.rootOperation);
                           
                        }
                    }
                    else
                    {
                        var property = propertyReference.Member as IPropertySymbol;

                        AccessorDeclarationSyntax propertyGetMethod = null;

                        if (propertyReference.Instance is IInstanceReferenceOperation)
                        {
                            if (dataValueMap.TryGetValue(property.Name, out DataFlowAnalysisValue tmp))
                            {
                                data = new DataFlowAnalysisValue(tmp);
                            }
                        }
                        else if (propertyReference.Instance is ILocalReferenceOperation LocalInstance)
                        {
                            if (property.GetMethod != null)
                            {
                                if (CallGraph.callgraph.GetNode(TaintAnalyzer.GetCallgraphKey(property.GetMethod)) != null)
                                {
                                    propertyGetMethod = CallGraph.callgraph.GetNode(TaintAnalyzer.GetCallgraphKey(property.GetMethod)).syntax as AccessorDeclarationSyntax;
                                }


                                if (propertyGetMethod != null && dataValueMap.TryGetValue(LocalInstance.Local.ToString(), out DataFlowAnalysisValue tmp))
                                {
                                    if (stackTrace != null && !stackTrace.Contains(TaintAnalyzer.GetCallgraphKey(property.GetMethod as IMethodSymbol)))
                                    {
                                        stackTrace.Push(TaintAnalyzer.GetCallgraphKey(property.GetMethod));
                                        data = tmp.InvokeMemberAccessor(stackTrace, property.Name, propertyGetMethod, null);
                                        stackTrace.Pop();
                                        data.BaseInstance = tmp;
                                    }
                                    else
                                    {
                                        data = new DataFlowAnalysisValue();
                                        data.Type = (property.GetMethod as IMethodSymbol).ReturnType;
                                        data.AddValue(propertyReference.Syntax);
                                    }                
                                }

                            }
                        }

                    }
                }
            }
            else if (operation is IInvocationOperation invocation)
            {
                IMethodSymbol targetMethod = invocation.TargetMethod;
                HashSet<DataFlowAnalysisValue> parameters = ConvertArgs2Values(invocation.Arguments);
                if ((stackTrace != null && !stackTrace.Contains(TaintAnalyzer.GetCallgraphKey(targetMethod)))){
                    data = new DataFlowAnalysisValue(targetMethod.ReceiverType, invocation.Syntax.ToFullString(), parameters);
                    data.Operation = operation;
                    data.TargetSymbol = targetMethod;
                }
                else
                {
                    stackTrace.Push(TaintAnalyzer.GetCallgraphKey(targetMethod));
                    string clazz = $"{targetMethod.ContainingSymbol.ToString()}";
                    if (instance != null)
                    {
                        if (DataFlowAnalyzer.Is_XamarinAPI(clazz) || DataFlowAnalyzer.IsWrapper(targetMethod))
                        {
                            data = instance.InvokeSkipAnalysis(targetMethod, parameters, this.rootOperation);
                            stackTrace.Pop();
                            data.BaseInstance = instance;
                        }
                        else
                        {
                            if (invocation.TargetMethod != null)
                            {
                                var method = CallGraph.callgraph.GetNode(TaintAnalyzer.GetCallgraphKey(targetMethod));
                                if (method != null)
                                {
                                    var methodSyntax = method.syntax as MethodDeclarationSyntax;
                                    stackTrace.Push(TaintAnalyzer.GetCallgraphKey(targetMethod));
                                    data = instance.InvokeMemberMethod(stackTrace, invocation.TargetMethod, methodSyntax, parameters, this.rootOperation);
                                    stackTrace.Pop();
                                    if (data == null)
                                    {
                                        data = new DataFlowAnalysisValue();
                                        data.AddValue(invocation.Syntax.ToString());
                                    }
                                    data.BaseInstance = instance;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (DataFlowAnalyzer.Is_XamarinAPI(clazz) || DataFlowAnalyzer.IsWrapper(targetMethod))
                        {
                            data = GetThisInstance().InvokeSkipAnalysis(targetMethod, parameters, this.rootOperation);
                            stackTrace.Pop();
                        }
                        else
                        {
                            if (targetMethod != null)
                            {
                                var method = CallGraph.callgraph.GetNode(TaintAnalyzer.GetCallgraphKey(targetMethod));

                                if (method != null)
                                {
                                    if (stackTrace != null && !stackTrace.Contains(TaintAnalyzer.GetCallgraphKey(targetMethod)))
                                    {
                                        var methodSyntax = method.syntax as MethodDeclarationSyntax;
                                        if (invocation.Instance is IInstanceReferenceOperation)
                                        {
                                            DataFlowAnalysisValue thisInstance = GetThisInstance();
                                            stackTrace.Push(TaintAnalyzer.GetCallgraphKey(targetMethod));
                                            data = thisInstance.InvokeMemberMethod(stackTrace, invocation.TargetMethod, methodSyntax, parameters, this.rootOperation);
                                            SetThisInstance(thisInstance);
                                            stackTrace.Pop();
                                            data.BaseInstance = thisInstance;
                                        }
                                        else if (invocation.Instance is ILocalReferenceOperation LocalInstance)
                                        {
                                            if (dataValueMap.TryGetValue(LocalInstance.Local.ToString(), out DataFlowAnalysisValue tmp))
                                            {
                                                stackTrace.Push(TaintAnalyzer.GetCallgraphKey(targetMethod));
                                                data = tmp.InvokeMemberMethod(stackTrace, invocation.TargetMethod, methodSyntax, parameters, this.rootOperation);
                                                stackTrace.Pop();
                                                data.BaseInstance = tmp;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        data = new DataFlowAnalysisValue();
                                        data.Type = targetMethod.ReturnType;
                                        data.AddValue(invocation.Syntax);
                                    }
                                   
                                }
                            }
                        }
                    }
 
                }
            }
            else if (operation is IConversionOperation conversion)
            {
                IOperation op = conversion;
                while(op is IConversionOperation)
                {
                    op = (op as IConversionOperation).Operand;
                }
                
                if(op is IInstanceReferenceOperation instanceReferenceOperation)
                {
                    data = GetThisInstance();
                    data.Type = conversion.Type;
                }
                else if(op is ILocalReferenceOperation LocalInstance)
                {
                    if (dataValueMap.TryGetValue(LocalInstance.Local.Name, out DataFlowAnalysisValue local))
                    {
                        data = new DataFlowAnalysisValue(local);
                        data.Type = conversion.Type;
                    }
                    else
                    {
                        data = new DataFlowAnalysisValue();
                        data.Type = conversion.Type;
                        data.AddValue(LocalInstance.Syntax);
                    }
                }
            }
            else
            {
              //  Console.WriteLine();
                // FlowCaptureReferenceOperation
                //conversion? int to string  or Invalid Operation = can not.
            }

            if(data == null)
            {
                data = new DataFlowAnalysisValue();
                data.Type = operation.Type;
                data.AddValue(operation.Syntax);
            }
            return data;
        }       
        public DataFlowAnalysisValue GetThisInstance()
        {
            DataFlowAnalysisValue thisInstance = new DataFlowAnalysisValue();
            thisInstance.IsInstance = true;
            thisInstance.Name = "this";

            foreach (var data in dataValueMap.Values)
            {
                if(!data.IsArgs && !data.IsLocal)
                {
                    thisInstance.AddMember(data);
                }
            }

            return thisInstance;
        }
        public bool SetThisInstance(DataFlowAnalysisValue thisInstance)
        {
            if(thisInstance.IsInstance && thisInstance.Name == "this")
            {
                foreach(var member in thisInstance.GetMembers())
                {
                    if (this.dataValueMap.ContainsKey(member.Name))
                    {
                        this.dataValueMap[member.Name] = thisInstance.GetMemberValue(member.Name);
                    }
                    else
                    {
                        this.dataValueMap.Add(member.Name, member);
                    }
                }
                return true;
            }
            return false;
        }
    }
}
