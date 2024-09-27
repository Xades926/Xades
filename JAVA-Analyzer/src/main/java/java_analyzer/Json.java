package java_analyzer;

import com.google.gson.GsonBuilder;
import com.google.gson.JsonArray;
import soot.MethodOrMethodContext;
import soot.jimple.toolkits.callgraph.Edge;

import java.io.*;
import java.util.Set;

import com.google.gson.Gson;
import com.google.gson.JsonObject;

public class Json {
    public static void writeCGJson(String outputPath, Set<MethodOrMethodContext> Nodes, Set<Edge> Edges) throws IOException {
        JsonArray nodeArray = new JsonArray();
        for(MethodOrMethodContext nodeObj : Nodes){
            boolean isNative = false;
            String clazz= nodeObj.method().getDeclaringClass().toString();
            String func =  nodeObj.method().getName().toString();
            String parameters =  nodeObj.method().getParameterTypes().toString();
            String returnType =  nodeObj.method().getReturnType().toString();

            if(nodeObj.method().isNative()){
                isNative = true;
            }


            JsonObject jsonObject = new JsonObject();
            jsonObject.addProperty("class", clazz);
            jsonObject.addProperty("func", func);
            jsonObject.addProperty("parameters", parameters);
            jsonObject.addProperty("return", returnType);
            jsonObject.addProperty("isNative", isNative);

            nodeArray.add(jsonObject);
        }

        JsonArray edgeArray = new JsonArray();
        for(Edge edgeObj : Edges){
            MethodOrMethodContext srcMethod = edgeObj.getSrc();
            String srcClass =  srcMethod.method().getDeclaringClass().toString();
            String srcFunc = srcMethod.method().getName();
            String srcSignature = JniAnalyzer.getJSONSignature(srcMethod);

            MethodOrMethodContext tgtMethod = edgeObj.getTgt();
            String tgtClass =  tgtMethod.method().getDeclaringClass().toString();
            String tgtFunc = tgtMethod.method().getName();
            String tgtSignature = JniAnalyzer.getJSONSignature(tgtMethod);


            JsonObject jsonObject = new JsonObject();
            jsonObject.addProperty("srcClass", srcClass);
            jsonObject.addProperty("srcFunc", srcFunc);
            jsonObject.addProperty("srcSignature", srcSignature);
            jsonObject.addProperty("srcUnit", edgeObj.srcUnit().toString());
            jsonObject.addProperty("tgtClass", tgtClass);
            jsonObject.addProperty("tgtFunc", tgtFunc);
            jsonObject.addProperty("tgtSignature", tgtSignature);
            edgeArray.add(jsonObject);
        }

        JsonObject callgraphJson = new JsonObject();

        callgraphJson.addProperty("LANG", "JAVA");
        callgraphJson.add("nodes", edgeArray);
        callgraphJson.add("edges", edgeArray);

        Gson gson = new GsonBuilder().setPrettyPrinting().create();
        FileWriter fw = new FileWriter(outputPath);
        gson.toJson(callgraphJson, fw);
        fw.flush();
        fw.close();

        System.out.println("\nWriting '" +  new File(outputPath).getAbsolutePath() + "'.");
        System.out.println("\tnode Cnt = " + Nodes.size());
        System.out.println("\tedge Cnt = " + Edges.size());
    }

    public static JsonArray ReadJson(String inputPath) throws IOException {
        Reader reader = new FileReader(inputPath);

        Gson gson = new Gson();
        JsonArray obj = gson.fromJson(reader, JsonArray.class);

        return obj;
    }
}
