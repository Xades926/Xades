using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.VisualStudio.Utilities;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using Roslyn_Analysis.Callgraph;
using Roslyn_Analysis.TaintAnalysis;

namespace Roslyn_Analysis.DataFlowAnalysis
{
    public class DataFlowAnalysisBlockResult 
    {
        public readonly BasicBlock basicBlock;
        
        private PooledDictionary<int, DataFlowAnalysisBlockResult> blockResultMap;
        private Stack<string> stackTrace;
        public PooledDictionary<int, DataFlowAnalysisOperationResult> operationResultMap= PooledDictionary<int, DataFlowAnalysisOperationResult>.GetInstance();
        
        public enum EmptyOperation{BasicBlockStart, Return};
        public DataFlowAnalysisBlockResult(Stack<string> stackTrace, PooledDictionary<int, DataFlowAnalysisBlockResult> blockResultMap, BasicBlock basicBlock)
        {
            this.stackTrace = stackTrace;
            this.basicBlock = basicBlock;
            this.blockResultMap = blockResultMap;
            this.ClonePredecessorBlockResults();
        }
        public DataFlowAnalysisBlockResult(BasicBlock basicBlock)
        {
            this.basicBlock = basicBlock;
        }
        public void SetParametersOnEntry(DataFlowAnalysisOperationResult args)
        {
            if (operationResultMap.ContainsKey(-1))
            {
                operationResultMap[-1].ClonePreOperation(args);
            }
            else
                operationResultMap.Add(-1, args);
        }
        public void ClonePredecessorBlockResults()
        {
            HashSet<DataFlowAnalysisOperationResult> preBlockLastOperationResults = new HashSet<DataFlowAnalysisOperationResult>();
            foreach (var predecessor in basicBlock.Predecessors)
            {
                DataFlowAnalysisOperationResult preBlockLastOperationResult = null;
                if (blockResultMap.TryGetValue(predecessor.Source.Ordinal, out DataFlowAnalysisBlockResult preBlockResult))
                    preBlockLastOperationResult = preBlockResult.GetLastOperationResult();



                if (operationResultMap.TryGetValue(-1, out DataFlowAnalysisOperationResult value))
                {
                    value.ClonePreOperation(preBlockLastOperationResult);
                }
                else
                {
                    operationResultMap.Add(-1, new DataFlowAnalysisOperationResult(preBlockLastOperationResult));
                }

            }
            if(basicBlock.Predecessors.Length == 0 && basicBlock.Ordinal > 0)
            {
                // is Catch?
                int idx = basicBlock.Ordinal;
                DataFlowAnalysisOperationResult preBlockLastOperationResult = null;
               

                while(idx > 0)
                {  
                    if (blockResultMap.TryGetValue(idx--, out DataFlowAnalysisBlockResult preBlockResult))
                        preBlockLastOperationResult = preBlockResult.GetLastOperationResult();
                        
                    if(preBlockLastOperationResult != null)
                    {
                        if (operationResultMap.TryGetValue(-1, out DataFlowAnalysisOperationResult value))
                        {
                            value.ClonePreOperation(preBlockLastOperationResult);
                        }
                        else
                        {
                            operationResultMap.Add(-1, new DataFlowAnalysisOperationResult(preBlockLastOperationResult));
                        }
                        break;
                    }
                    
                }
            }
        }
        public DataFlowAnalysisValue AnalyzeOperations()
        {
            
            DataFlowAnalysisValue returnValue = new DataFlowAnalysisValue();
            returnValue.Name = "return";
            if (basicBlock.Kind == BasicBlockKind.Entry)
            {
                return null;
            }

  
          
            for (int idx=0; idx<basicBlock.Operations.Length; idx++)
            {
                operationResultMap.TryGetValue(idx-1, out DataFlowAnalysisOperationResult preResult);
                if (!operationResultMap.ContainsKey(idx))
                    operationResultMap.Add(idx, new DataFlowAnalysisOperationResult(stackTrace, basicBlock.Operations[idx], preResult));
            }
            
            if(basicBlock.FallThroughSuccessor != null && basicBlock.FallThroughSuccessor.Semantics == ControlFlowBranchSemantics.Return)
            {
                var branchValue = basicBlock.BranchValue;
                DataFlowAnalysisOperationResult dataFlowAnalysisOperationResult = null;
                foreach (var predecessor in basicBlock.Predecessors)
                {
                    if (predecessor.Semantics == ControlFlowBranchSemantics.Return && predecessor.Source.IsReachable == true)
                    {
                        blockResultMap.TryGetValue(predecessor.Source.Ordinal, out DataFlowAnalysisBlockResult predecessorBlockResult);
                        var predessorOperationResult = predecessorBlockResult.GetLastOperationResult();

                        dataFlowAnalysisOperationResult.ClonePreOperation(predessorOperationResult);
           
                    }
                }
                if (dataFlowAnalysisOperationResult == null)
                    dataFlowAnalysisOperationResult = new DataFlowAnalysisOperationResult();
                returnValue.Join(dataFlowAnalysisOperationResult.SolveChaining(branchValue));
            }


            if (basicBlock.Kind == BasicBlockKind.Exit)
            {
                foreach (var predecessor in basicBlock.Predecessors)
                {
                    if (predecessor.Semantics == ControlFlowBranchSemantics.Return && predecessor.Source.IsReachable == true)
                    {
                        var branchValue = predecessor.Source.BranchValue;
                        returnValue.Type = branchValue.Type;

                        blockResultMap.TryGetValue(predecessor.Source.Ordinal, out DataFlowAnalysisBlockResult predecessorBlockResult);
                        var predessorOperationResult = predecessorBlockResult.GetLastOperationResult();

                        if (predessorOperationResult != null && branchValue != null)
                        {
                            var test = predessorOperationResult.SolveChaining(branchValue);
                            returnValue.Join(predessorOperationResult.SolveChaining(branchValue));
                        }
                    }
                }
            }
            return returnValue;
        }
        public DataFlowAnalysisOperationResult GetLastOperationResult()
        {   
            if(operationResultMap.Count > 0)
                return operationResultMap.Values.Last();
            return null;
        }
    } 
}
