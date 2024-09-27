package java_analyzer;

import soot.*;
import soot.JastAddJ.Opt;
import soot.jimple.InvokeStmt;
import soot.jimple.Jimple;
import soot.jimple.Stmt;
import soot.jimple.StringConstant;
import soot.jimple.infoflow.InfoflowConfiguration;
import soot.jimple.infoflow.android.InfoflowAndroidConfiguration;
import soot.jimple.infoflow.android.SetupApplication;
import soot.jimple.infoflow.results.InfoflowResults;
import soot.jimple.toolkits.callgraph.CallGraph;
import soot.jimple.toolkits.callgraph.CallGraphBuilder;
import soot.jimple.toolkits.callgraph.Edge;
import soot.options.Options;

import java.io.BufferedWriter;
import java.io.Console;
import java.io.File;
import java.io.FileWriter;
import java.util.*;

import static java_analyzer.Json.writeCGJson;

public class Main {
    private final static String USER_HOME = System.getProperty("user.home");
    private static final String androidJar = USER_HOME + "\\AppData\\Local\\Android\\Sdk\\platforms";

    public static Set<MethodOrMethodContext> Nodes = new LinkedHashSet<>();
    public static Set<Edge> Edges = new LinkedHashSet<>();

    private static long timeout = 300;
    private static String apkPath = null;
    public static String outputDir = "./";
    private static String sourcesSinks = null;
    private static  SetupApplication setupApp;
    public static void main(String[] args){
        for (int i = 0; i < args.length; i++) {
            String arg = args[i];
            if (arg.equals("-t") || arg.equals("--timeout")) {
                if (i + 1 < args.length) {
                    timeout = Long.parseLong(args[i+1]);
                    i++;
                }
            }
            if (arg.equals("-o") || arg.equals("--output")) {
                if (i + 1 < args.length) {
                    outputDir = args[i + 1];
                    System.out.println(outputDir);
                    i++;
                } else {
                    System.err.println("[ERROR] can not find output directory in your inputs.");
                    return;
                }
            }
            if (arg.equals("--apk")) {
                if (i + 1 < args.length) {
                    apkPath = args[i + 1];
                    System.out.println(apkPath);
                    i++;
                } else {
                    System.err.println("[ERROR] can not find apk absolute path in your inputs.");
                    return;
                }
            }
            if (arg.equals("--ss")) {
                if (i + 1 < args.length) {
                    sourcesSinks = args[i + 1];
                    System.out.println(sourcesSinks);
                    i++;
                } else {
                    System.err.println("[ERROR] can not find sourceNsinks file path in your inputs.");
                    return;
                }
            }
        }

        if(apkPath == null || sourcesSinks == null){
            System.err.println("[ERROR].");
            return;
        }

        File file = new File(apkPath);
        System.out.println("Set timeout seconds: " + timeout);
        System.out.println("Android Jars path : " + androidJar);
        System.out.println("Analyzing apk file '" + file.getAbsolutePath() + "'.");

        Scene.v().loadBasicClasses();
        Scene.v().loadNecessaryClasses();

        initializeConfig();
        CallGraph cg = null;
        cg = analyzeCG();

        try {
            CsharpCallgraph csharp_analyzer = new CsharpCallgraph(cg);
            csharp_analyzer.ConstructCsharpDummyMain();
        }
        catch (Exception e){
            System.out.println("Failed to construct csharp method.");
        }

        try {
            Set<String> basciClasses = Scene.v().getBasicClasses();
            for(String clazz : basciClasses){
                for(SootMethod method : Scene.v().getSootClass(clazz).getMethods()){
                    if(method.getSource() != null)
                        method.retrieveActiveBody();
                }
            }

            setupApp.getConfig().setSootIntegrationMode(InfoflowAndroidConfiguration.SootIntegrationMode.UseExistingCallgraph);
            InfoflowResults results =  setupApp.runInfoflow();
        }
        catch (Exception e){
            System.out.println("Failed to run FlowDroid.");
        }
    }


    public static void print_Edges(CallGraph cg, String methodSignature){
        System.out.println("Print Edges : " + methodSignature);
        Iterator<MethodOrMethodContext> itMethod =  cg.sourceMethods();
        while(itMethod.hasNext()){
            MethodOrMethodContext methodObj = itMethod.next();
            SootMethod sootMethod = methodObj.method();

            if(sootMethod.getSignature().contains(methodSignature)){
                System.out.println(sootMethod.getSignature());
                Iterator<Edge> itEdgeOUT = cg.edgesOutOf(methodObj);
                while(itEdgeOUT.hasNext()){
                    Edge edgeObj = itEdgeOUT.next();
                    if(edgeObj.src().getSignature().contains(methodSignature)){
                        System.out.println("  src: "+ edgeObj.getSrc().toString() + " => tgt: " + edgeObj.getTgt().toString());
                    }

                }

                Iterator<Edge> itEdgeIN = cg.edgesInto(methodObj);
                while(itEdgeIN.hasNext()){
                    Edge edgeObj = itEdgeIN.next();
                    if(edgeObj.src().getSignature().contains(methodSignature)){
                        System.out.println("  src: "+ edgeObj.getSrc().toString() + " => tgt: " + edgeObj.getTgt().toString());
                    }
                }

            }
        }
        System.out.println("==============================================================");
    }
    private static void initializeConfig(){
        Options.v().set_src_prec(Options.src_prec_apk);
        Options.v().set_process_dir(Collections.singletonList(apkPath));
        Options.v().set_allow_phantom_refs(false);
        Options.v().set_process_multiple_dex(true);
        Options.v().set_prepend_classpath(true);
        Options.v().set_include_all(true);
        Options.v().set_on_the_fly(true);
        Options.v().setPhaseOption("cg", "trim-clinit:false");
        Options.v().setPhaseOption("cg", "propagator:true");

        final InfoflowAndroidConfiguration config = new InfoflowAndroidConfiguration();
        config.getAnalysisFileConfig().setTargetAPKFile(apkPath);
        config.getAnalysisFileConfig().setAndroidPlatformDir(androidJar);
        config.setCodeEliminationMode(InfoflowConfiguration.CodeEliminationMode.RemoveSideEffectFreeCode);
        config.setMergeDexFiles(true);
        config.setTaintAnalysisEnabled(false);
        config.setCallgraphAlgorithm(InfoflowConfiguration.CallgraphAlgorithm.VTA);
        config.getAnalysisFileConfig().setSourceSinkFile(sourcesSinks);
        config.setStaticFieldTrackingMode(InfoflowConfiguration.StaticFieldTrackingMode.ContextFlowInsensitive);
        config.setImplicitFlowMode(InfoflowConfiguration.ImplicitFlowMode.AllImplicitFlows);

        config.setDataFlowTimeout(timeout);
        setupApp = new SetupApplication(config);

        Options.v().set_no_bodies_for_excluded(false);
    }
    private static CallGraph analyzeCG(){

        setupApp.constructCallgraph();
        CallGraphBuilder cgBuilder = new CallGraphBuilder();

        var test = cgBuilder.reachables();
        cgBuilder.build();
        CallGraph cg = Scene.v().getCallGraph();

        Iterator<MethodOrMethodContext> itMethod =  cg.sourceMethods();
        while(itMethod.hasNext()){
            MethodOrMethodContext methodObj = itMethod.next();
            Nodes.add(methodObj);

            Iterator<Edge> itEdge = cg.edgesOutOf(methodObj);
            while(itEdge.hasNext()){
                Edge edgeObj = itEdge.next();
                Nodes.add(edgeObj.getTgt());
                Edges.add(edgeObj);
            }
        }

        JniAnalyzer.Analyze();
        setupApp.getConfig().setTaintAnalysisEnabled(true);
        return cg;
    }
    private static boolean isBanned(MethodOrMethodContext method){
        String[] banned = {"android.", "androidx.", "java", "javax","kotlin", "kotlinx", "xamarin.android", "com.google"};

        for(String str : banned){
            if(method.method().getDeclaringClass().toString().contains(str)){
                return true;
            }
        }
        return false;
    }
}