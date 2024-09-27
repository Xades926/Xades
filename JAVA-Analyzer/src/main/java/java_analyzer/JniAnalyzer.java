package java_analyzer;

import com.google.gson.*;
import soot.*;
import soot.jimple.toolkits.callgraph.Edge;

import java.io.*;
import java.util.*;

public class JniAnalyzer {
    private static final Map<String, String> java_primitive = new HashMap<>() {{
        put("void", "V");
        put("boolean", "Z");
        put("byte", "B");
        put("char", "C");
        put("short", "S");
        put("int", "I");
        put("long", "J");
        put("float", "F");
        put("double", "D");
    }};
    private static final Map<String, String> jni_primitive = new HashMap<>() {{
        put("V", "void");
        put("Z", "boolean");
        put("B", "byte");
        put("C", "char");
        put("S", "short");
        put("I", "int");
        put("J", "long");
        put("F", "float");
        put("D", "double");
    }};
    private static final Set<String> Libs = new LinkedHashSet<>();
    private static final Set<MethodOrMethodContext> JNIMethods = new LinkedHashSet<>();
    private static JsonObject jniJson = null;
    public static void Analyze(){

        LoadJniJson();

        // CollectCSHARP2JAVAMethod();

        CollectLibrary();
        CollectJAVA2CPPMethod();

        writeJniJson(Main.outputDir + "\\Interfaces.json");
    }
    private static void LoadJniJson(){
        Reader reader = null;
        File jsonPath = new File(Main.outputDir + "\\Interfaces.json");
        try {
            System.out.println("Loading '" + jsonPath.getAbsolutePath() + "'.");
            reader = new FileReader(jsonPath);

            Gson gson = new Gson();
            jniJson = gson.fromJson(reader, JsonObject.class);
        }
        catch (FileNotFoundException e) {
            jniJson = new JsonObject();
            System.out.println("Not exist '" + jsonPath.getAbsolutePath() + "'.");
        }
    }
    private static void CollectCSHARP2JAVAMethod(){
        JsonObject csharp2java =(JsonObject) jniJson.get("Csharp2Java");
        if(csharp2java ==  null)
            return;

        JsonArray edges = (JsonArray) csharp2java.get("edges");
        int cnt = 0;
        for(JsonElement jsonElement : edges){
            JsonObject edge = (JsonObject) jsonElement;
            SootMethod method;
            try{
                method = Scene.v().getMethod(getSootSignature(edge));
            }
            catch (Exception e ){
                String bug = getSootSignature(edge);
               //System.out.println("DEBUG :: json sig = " + bug);
                SootClass clazz = Scene.v().loadClass(edge.get("tgtClass").toString(), SootClass.SIGNATURES );
                method = clazz.getMethodUnsafe(getSootSignature(edge));
                if (method == null) {
                    // System.out.println("DEBUG :: cannot find method = " + bug);
                }
            }
            if (method != null) {
                cnt ++;
                Main.Nodes.add(method);
            }
        }
    }
    private static void CollectLibrary(){
        for(Edge edge : Main.Edges) {
            MethodOrMethodContext tgtMethod = edge.getTgt();
            String tgtFunc = tgtMethod.method().getName();

            if (tgtFunc.equals("loadLibrary")) {
                String srcUnit = edge.srcUnit().toString();
                String[] tmp1 = srcUnit.split("\\(");
                String tmp2 = tmp1[tmp1.length - 1];
                String libFile = tmp2.substring(1, tmp2.length() - 2);

                Libs.add(libFile);
            }
        }
    }
    private static void CollectJAVA2CPPMethod(){
        for(MethodOrMethodContext nodeObj : Main.Nodes){
            if(nodeObj.method().isNative()){
                JNIMethods.add((nodeObj));
            }
        }
    }
    private static String getJNIMethodName(MethodOrMethodContext method){
        String name = "Java_";
        String clazz= method.method().getDeclaringClass().toString();
        String func = method.method().getName();
        String sub = clazz + "." + func;
        name +=  sub.replace("_","_1").replace('.','_');

        return name;
    }
    public static void writeJniJson(String outputPath){
        JsonArray libsJSON = new JsonArray();
        for(String libFile : Libs){
            libsJSON.add("lib"+ libFile +".so");
        }

        JsonArray JniEdgeJson = new JsonArray();
        for(MethodOrMethodContext JNIMethod : JNIMethods){
            String srcClass =  JNIMethod.method().getDeclaringClass().toString();
            String srcFunc = JNIMethod.method().getName();
            String srcSignature = JniAnalyzer.getJSONSignature(JNIMethod);

            JsonObject edgeJson = new JsonObject();
            edgeJson.addProperty("srcClass", srcClass);
            edgeJson.addProperty("srcFunc", srcFunc);
            edgeJson.addProperty("srcSignature", srcSignature);
            edgeJson.addProperty("tgt", getJNIMethodName(JNIMethod));

            JniEdgeJson.add(edgeJson);
        }

        JsonObject java2cpp = new JsonObject();
        java2cpp.add("libs", libsJSON);
        java2cpp.add("edges", JniEdgeJson);

        jniJson.add("JAVA2CPP", java2cpp);

        Gson gson = new GsonBuilder().setPrettyPrinting().create();
        FileWriter fw = null;
        try {
            fw = new FileWriter(Main.outputDir + "\\Interfaces.json");
            gson.toJson(jniJson, fw);
            fw.flush();
            fw.close();
        } catch (IOException e) {
            System.out.println("[ERROR] Error");
        }


        System.out.println("Writing '" + new File(outputPath).getAbsolutePath() + "'.");

        System.out.println("\t'Java to C/C++' JNI Edges Cnt = " + JNIMethods.size());
        System.out.println("\t'Java to C/C++' Library Cnt = " + Libs.size());

    }
    public static String getJSONSignature(MethodOrMethodContext method){
        StringBuilder signature = new StringBuilder("(");
        List<Type> paramters = method.method().getParameterTypes();
        String returnType = method.method().getReturnType().toString();

        for (Type param: paramters) {
            String parameter = param.toString();
            if(parameter.contains("[")) {
                signature.append("[");
                parameter = parameter.replace("[]","");
            }

            if(java_primitive.containsKey(parameter)){
                signature.append(java_primitive.get(parameter));
            }
            else{
                signature.append("L").append(parameter).append(";");
            }
        }
        signature.append(")");


        if(returnType.contains("[")) {
            signature.append("[");
            returnType = returnType.replace("[]","");
        }

        if(java_primitive.containsKey(returnType)){
            signature.append(java_primitive.get(returnType));
        }
        else{
            signature.append("L").append(returnType).append(";");
        }

        return signature.toString();
    }
    public static String getSootSignature(JsonObject edge){

        String clazz = edge.get("tgtClass").toString();
        String func = edge.get("tgtFunc").toString();
        String signature = edge.get("tgtSignature").toString().replace('/','.');
        StringBuilder parameter = new StringBuilder("(");
        String[] split = signature.split("\\)");

        boolean isArr = false;
        for(int i=0; i<split[0].length(); i++){
            if(split[0].charAt(i) == '[') isArr = true;

            if(split[0].charAt(i) == 'L'){
                int idx = split[0].indexOf(";", i);
                parameter.append(split[0], i + 1, idx);
                i = idx;
                if (isArr){
                    parameter.append("[]");
                    isArr = false;
                }
                parameter.append(",");
            }

            if(i < split[0].length() && jni_primitive.containsKey(Character.toString(split[0].charAt(i)))){
                parameter.append(jni_primitive.get(Character.toString(split[0].charAt(i))));
                parameter.append(",");
                if (isArr){
                    parameter.append("[]");
                    isArr = false;
                }
            }
        }
        if(parameter.length() > 2)
            parameter = new StringBuilder(parameter.substring(0, parameter.length()-1));
        parameter.append(")");

        StringBuilder r3turn = new StringBuilder();
        isArr = false;
        for(int i=0; i<split[1].length(); i++){
            if(split[1].charAt(i) == '[') isArr = true;

            if(split[1].charAt(i) == 'L'){
                int idx = split[1].indexOf(";", i);
                r3turn.append(split[1], i + 1, idx);
                i = idx;
                if (isArr) {
                    r3turn.append("[]");
                    isArr = false;
                }
            }

            if(i < split[1].length() && jni_primitive.containsKey(Character.toString(split[1].charAt(i)))){
                r3turn.append(jni_primitive.get(Character.toString(split[1].charAt(i))));
            }
        }

        return String.format("<%s: %s %s%s>", clazz, r3turn, func, parameter);
    }
}
