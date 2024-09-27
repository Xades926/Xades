using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.Immutable;

using System.Reflection;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Roslyn_Analysis.Callgraph;
using Roslyn_Analysis.Graph;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Roslyn_Analysis.DataFlowAnalysis;
using Roslyn_Analysis.TaintAnalysis;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;

namespace Roslyn_Analysis
{
    class Program
    {
        public static long maxMemory = 0;
        public static string solutionPath = "";
        public static string configurePath = "";
        public static string xamarinApiPath = "";
        public static string javaApiPath = "";
        public static string java_sourcesinkPath = "";
        public static string outPath = "";
        public static int cppCustom = 0;
        public static Compilation Compilation { get; private set; }
        public static async Task Main(string[] args)
        {
            // TODO : Change timeout seconds
            Process currentProcess = Process.GetCurrentProcess();
            double timeoutSec = 1800;

            for(int i=0; i<args.Length; i++)
            {
                if (args[i] == "-sln")
                {
                    solutionPath = args[++i];
                }
                if (args[i] == "-conf")
                {
                    configurePath = args[++i];
                }
                if (args[i] == "-out")
                {
                    outPath = args[++i];
                }
                if (args[i] == "-t")
                {
                    timeoutSec = double.Parse(args[++i]);
                }
            }
     
            if(solutionPath.Length == 0 || configurePath.Length == 0 || outPath.Length == 0) {

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"You need more options.");
                Console.ResetColor();
                return;
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            Thread monitoringThread = new Thread(AnalysisMonitor);
            monitoringThread.IsBackground = true;
            monitoringThread.Start();

            Console.WriteLine("Starting memory monitor.");
            Console.WriteLine($"Set timeout seconds = {timeoutSec}\n");

            xamarinApiPath = $"{configurePath}\\XamarinAPIs.txt";
            javaApiPath = $"{configurePath}\\JavaAPIs.txt";
            
            JavaWrapperAnalyzer.CallSyntax = JavaWrapperAnalyzer.InitCallFuctions();


            JavaWrapperAnalyzer.InitializeJavaAPIs(javaApiPath);
            DataFlowAnalyzer.InitializeXamarinAPI(xamarinApiPath);
            TaintAnalyzer.InitializeSourcesAndSinks(configurePath);
            

            if (!MSBuildLocator.IsRegistered) //MSBuildLocator.RegisterDefaults(); // ensures correct version is loaded up
            {
                var vs2022 = MSBuildLocator.QueryVisualStudioInstances().Where<VisualStudioInstance>(x => x.Name == "Visual Studio Community 2022").First(); // find the correct VS setup. There are namy ways to organise logic here, we'll just assume we want VS2022
                Console.WriteLine($"Using MSBuild at '{vs2022.MSBuildPath}' to load projects.");
                MSBuildLocator.RegisterInstance(vs2022); // register the selected instance
                var _ = typeof(Microsoft.CodeAnalysis.CSharp.Formatting.CSharpFormattingOptions); // this ensures library is referenced so the compiler would not try to optimise it away (if dynamically loading assemblies or doing other voodoo that can throw the compiler off) - probably less important than the above but we prefer to follow cargo cult here and leave it be
            }

          
            TimeSpan timeout = TimeSpan.FromSeconds(timeoutSec); 

            Task task = Task.Run(() => AnalyzeSolutionAsync(solutionPath));

            // Task.Delay와 Task.WhenAny를 이용하여 timeout을 구현합니다.
            if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
            {

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Finishied.");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine($"Timeout-MemoryUsage: {maxMemory} MB");
                currentProcess.Dispose();   
                Console.WriteLine("Timeout at Csharp Analyzer");
            }


            stopwatch.Stop();
            TimeSpan elapsedTime = stopwatch.Elapsed;
            Console.WriteLine($"Running Time: {elapsedTime.TotalSeconds} sec");

            Console.WriteLine($"MemoryUsage: {maxMemory / (1024 * 1024)} MB");
            currentProcess.Dispose();
        }

        static async  Task AnalyzeSolutionAsync(String filePath) 
        {

            Console.ResetColor();
            Solution solution;
            using (var w = MSBuildWorkspace.Create())
            {
                Console.WriteLine($"Loading solution '{filePath}'.");
                try
                {
                    solution = await w.OpenSolutionAsync(filePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message}");
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Finishied loading soultion '{filePath}'.\n");
                Console.ResetColor();

                HashSet<MetadataReference> refs = new HashSet<MetadataReference>();
                HashSet<SyntaxTree> syntaxtrees = new HashSet<SyntaxTree>();
                foreach (var project in solution.Projects)
                {
                    Console.WriteLine($"Loading project '{project.FilePath}'.");
                    Compilation subcompilation = project.GetCompilationAsync().Result;

                    var references = subcompilation.References;
                    foreach (MetadataReference reference in references)
                    {
                        refs.Add(reference);
                    }

                    var trees = subcompilation.SyntaxTrees;
                    foreach (SyntaxTree tree in trees)
                    {
                        syntaxtrees.Add(tree);
                    }
                }


                Program.Compilation = CSharpCompilation.Create("Analysis").AddReferences(refs).AddSyntaxTrees(syntaxtrees);


                Console.WriteLine($"\nAnalyzing {filePath}'s call-graph.");

                CallGraph cg = new CallGraph(syntaxtrees);
                cg.AnalyzeCallGraph();
                // write whole call graph to JSON file.
                cg.writeJSON();

                w.CloseSolution();
            }

            JObject interfaceJSON = new JObject();

            CppWrapperAnalyzer.Add_CppWrapperEdge(CallGraph.callgraph);
            interfaceJSON.Add(new JProperty("Csharp2Cpp", CppWrapperAnalyzer.CppWrapper2Json()));

            Console.WriteLine($"\t'Csharp to C/C++' nodes Cnt = {CppWrapperAnalyzer.nodes.Count}");
            Console.WriteLine($"\t'Csharp to C/C++' edges Cnt = {CppWrapperAnalyzer.edges.Count}");
            Console.WriteLine($"\t'Csharp to C/C++' Custom Edges = {Program.cppCustom}");

            Console.WriteLine($"\nAnalyzing JavaWrapper functions ..");
            foreach (var acwCall in JavaWrapperAnalyzer.JavaWrapperCalls)
            {
                var semanticModel = Compilation.GetSemanticModel(acwCall.Key.SyntaxTree, false);
                JavaWrapperAnalyzer acwAnalyzer = new JavaWrapperAnalyzer(acwCall.Key, acwCall.Value);
                acwAnalyzer.AnalyzeJavaWrapper();
            }
      
            JavaWrapperAnalyzer.Add_JavaWrapperEdge(CallGraph.callgraph);
            interfaceJSON.Add(new JProperty("Csharp2Java", JavaWrapperAnalyzer.JavaWrapper2Json()));

            Console.WriteLine($"\t'Csharp to Java' nodes Cnt = {JavaWrapperAnalyzer.JavaWrapper_Nodes.Count}");
            Console.WriteLine($"\t'Csharp to Java' edges Cnt = {JavaWrapperAnalyzer.JavaWrapper_Edges.Count}");
            Console.WriteLine($"\t'Csharp to Java' Custom Edges = {JavaWrapperAnalyzer.javaCustom}");
            Console.WriteLine();
            Console.WriteLine($"\tCsharp call graph # nodes = {CallGraph.callgraph.GetNodes().Count}");
            Console.WriteLine($"\tCsharp call graph # edges = {CallGraph.callgraph.GetEdges().Count}");
            // 파일쓰기
            var jsonPath = Path.GetFullPath($"{Program.outPath}\\Interfaces.json");
            Console.WriteLine($"\nWriting '{jsonPath}'.");
            File.WriteAllText(jsonPath, interfaceJSON.ToString());
           

            TaintAnalyzer taintAnalyzer = new TaintAnalyzer();
            taintAnalyzer.Analyze();

        }

        static void AnalysisMonitor()
        {
            while (true)
            {
                Process currentProcess = Process.GetCurrentProcess();
                long nowMemory = currentProcess.WorkingSet64;

                if(nowMemory > maxMemory)
                {
                    maxMemory = nowMemory;
                }
                Thread.Sleep(500);
            }
        }
    }
}