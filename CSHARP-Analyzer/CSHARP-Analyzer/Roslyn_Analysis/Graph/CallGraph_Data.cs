using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace Roslyn_Analysis.Graph
{
    public class CallgraphNode
    {
        public string Clazz { get; set; }
        public string Function { get; set; }
        public string parameters { get; set; } 
        public string ReturnType { get; set; } 
        public bool IsWrapper { get; set; } = false;
        public bool IsJava { get; set; } = false;
        public bool IsCpp { get; set; } = false;
        public bool isSource{ get; set; } = false;
        public bool isSink { get; set; } = false;
        public string Signature { get; set; } = null;
        public SyntaxNode syntax { get; set; } = null;
        public string GetSignature()
        {
            if (Signature != null)
            {
                return Signature;
            }
            else
            {
                if (parameters != null && ReturnType != null)
                {
                    this.Signature = Signature;
                    return $"({parameters.Substring(1, parameters.Length - 2)}){ReturnType}";
                }
                return "()void";
            }
        }
        public string GetKey()
        {
            return $"{Clazz}/{Function}{GetSignature()}";
        }
        public string GetSootSignatrue()
        {
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
            string[] split= this.GetSignature().Split(')');
            string _parameter = split[0].Substring(1);
            string _ret = split[1];
            string parameter = "";
            string ret = "";

            bool isArray = false;
            if (_ret[0] == '[')
            {
                isArray = true;
                _ret = _ret.Substring(1);
            }

            if (java_primitive.ContainsKey(_ret[0]))
            {
                ret = java_primitive[_ret[0]];
            }
            else
            {
                ret += _ret.Substring(1, _ret.Length - 2);
            }
            
            if(isArray) {
                ret += "[]";
                isArray = false;
            }
            
            for(int i =0; i<_parameter.Length; i++)
            {
                if (_parameter[i] == '[')
                {
                    isArray = true;
                }
                if (java_primitive.ContainsKey(_parameter[i]))
                {
                    parameter += $"{java_primitive[_parameter[i]]}";
                    if (isArray) { 
                        parameter += "[]"; 
                        isArray=false;
                    }
                    parameter += ",";
                }
                else
                {
                    i++;
                    while (_parameter[i] != ';')
                    {
                        parameter += _parameter[i++];
                    }
                    if (isArray)
                    {
                        parameter += "[]";
                        isArray = false;
                    }
                    parameter += ",";
                }
            }
            if(parameter.Length > 0)
            {
                parameter = parameter.Substring(0, parameter.Length - 1);
            }
           

            return $"<{this.Clazz}: {ret} {Function}({parameter})>";
        }
    }
    public class CallgraphEdge
     {
        public CallgraphEdge(CallgraphNode src, CallgraphNode tgt, SyntaxNode srcUnit)
        {
            this.src = src;
            this.tgt= tgt;
            this.srcUnit = srcUnit;
        }
        public CallgraphEdge()
        {
          
        }
        public CallgraphNode src { get; set; }
        public SyntaxNode srcUnit { get; set; }
        public CallgraphNode tgt { get; set; }

        public string Get_srcUnit()
        {
            if(srcUnit != null)
                return srcUnit.ToString();
            return "";
        }
    }
}
