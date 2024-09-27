package java_analyzer;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import soot.*;
import soot.jimple.*;
import soot.jimple.toolkits.callgraph.CallGraph;
import soot.jimple.toolkits.callgraph.Edge;

import java.io.BufferedWriter;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.util.*;


public class CsharpCallgraph {
    CallGraph cg = null;
    public CsharpCallgraph(CallGraph cg){
        this.cg = cg;
    }

    // ImmutableList.of()
    public void ConstructCsharpDummyMain(){
        JsonArray csharpJson;
        try {
            csharpJson = Json.ReadJson(Main.outputDir + "\\csharp2jimple.json");
        } catch (IOException e) {
            System.out.println("Not exist csharp2jimple.json file.");
            return;
        }

        SootClass dummyMainClass_Csharp = make_newClass("dummyMainClass_csharp");
        dummyMainClass_Csharp.setApplicationClass();
        ConstructCsharpSourceAndSink(dummyMainClass_Csharp);
        SootMethod dummyMainMethod_csharp = new SootMethod("dummyMainMethod_csharp", null, VoidType.v(), Modifier.PUBLIC | Modifier.STATIC);
        JimpleBody dummyMainBody = Jimple.v().newBody(dummyMainMethod_csharp);
        dummyMainMethod_csharp.setActiveBody(dummyMainBody);
        dummyMainClass_Csharp.addMethod(dummyMainMethod_csharp);

        List<SootClass> sootClasses = new ArrayList<>();
        for(int i=0; i<csharpJson.size(); i++){
            SootClass tmp = ConstructCsharpClass((JsonObject) csharpJson.get(i));
            WriteJimple(tmp);
            sootClasses.add(tmp);
        }


        // dummyMainBody
        Map<String, Local> locals = new HashMap<>();
        for(int i=0; i<sootClasses.size(); i++){
            SootClass sootClass = sootClasses.get(i);
            Local local = Jimple.v().newLocal("$v"+i, RefType.v(sootClass));
            dummyMainBody.getLocals().add(local);
            dummyMainBody.getUnits().add(Jimple.v().newAssignStmt(local, Jimple.v().newNewExpr(RefType.v(sootClass))));

//            SootMethodRef ref = Scene.v().makeConstructorRef(sootClass, null);
//            Stmt invokeStmt = Jimple.v().newInvokeStmt(Jimple.v().newSpecialInvokeExpr(local, ref));
//            dummyMainBody.getUnits().add(invokeStmt);

            for(SootMethod sootMethod : sootClass.getMethods()){
                List<Value> parameters = new ArrayList<>();
                for(int j =0; j< sootMethod.getParameterCount(); j++){
                    parameters.add(NullConstant.v());
                }
                Kind kind = Kind.VIRTUAL;
                if(sootMethod.getName().contains("init")){
                    kind = Kind.SPECIAL;
                }
                var test = sootMethod.isValidResolve(sootMethod.makeRef());
                Stmt stmt = make_InvokeStmt(dummyMainMethod_csharp.getSignature(), local, sootMethod.getSignature(), parameters, kind);
                if(stmt != null)
                    dummyMainBody.getUnits().add(stmt);
            }
        }

        dummyMainBody.getUnits().add(Jimple.v().newReturnVoidStmt());
        try {
            dummyMainBody.validate();
        }
        catch(Exception e) {
            System.out.println(e.getMessage());
        }
        // Add edge from original dummy main to csharp dummy main
        addEdges_dummyMainMethod(dummyMainMethod_csharp);
        WriteJimple(dummyMainClass_Csharp);
    }

    private void ConstructCsharpSourceAndSink(SootClass dummyMainClass_Csharp ){
        SootMethod csSource = new SootMethod("csSource", null, RefType.v("java.lang.Object"), Modifier.PUBLIC | Modifier.STATIC);
        Body sourceBody = Jimple.v().newBody(csSource);
        csSource.setActiveBody(sourceBody);
        Local _return = Jimple.v().newLocal("$v", RefType.v("java.lang.Object"));
        sourceBody.getLocals().add(_return);
        sourceBody.getUnits().add(Jimple.v().newAssignStmt(_return, Jimple.v().newNewExpr((RefType) _return.getType())));
        sourceBody.getUnits().add(Jimple.v().newReturnStmt(_return));
        dummyMainClass_Csharp.addMethod(csSource);

        List<Type> parameters = new ArrayList<>();
        parameters.add(RefType.v("java.lang.Object"));
        SootMethod csSink = new SootMethod("csSink",parameters, VoidType.v(), Modifier.PUBLIC | Modifier.STATIC);
        Body sinkBody = Jimple.v().newBody(csSink);
        csSink.setActiveBody(sinkBody);
        Local param = Jimple.v().newLocal("$v", RefType.v("java.lang.Object"));
        sinkBody.getLocals().add(param);
        sinkBody.getUnits().add(Jimple.v().newIdentityStmt(param, Jimple.v().newParameterRef(param.getType(), 0)));
        sinkBody.getUnits().add(Jimple.v().newReturnVoidStmt());
        dummyMainClass_Csharp.addMethod(csSink);
    }
    public SootClass ConstructCsharpClass(JsonObject classJson) {
        String clazz = classJson.get("Class").getAsString();
        //String superClass = ConvertTypeString(classJson.get("SuperClass").getAsString());
        JsonArray methodArray = classJson.getAsJsonArray("Methods");

        SootClass newClass = make_newClass(clazz);
        newClass.setApplicationClass();
        newClass.setModifiers(1);

        SootMethod initMethod = new SootMethod("<init>",null, VoidType.v(), Modifier.PUBLIC);
        Body initBody = Jimple.v().newBody(initMethod);
        initMethod = newClass.getOrAddMethod(initMethod);
        initMethod.setActiveBody(initBody);
        Local $this = Jimple.v().newLocal("this", RefType.v(clazz));
        initBody.getLocals().add($this);
        initBody.getUnits().add(Jimple.v().newIdentityStmt($this, Jimple.v().newThisRef(RefType.v(clazz))));
        initBody.getUnits().add(Jimple.v().newReturnVoidStmt());
        for(int i=0; i<methodArray.size(); i++){
            ConstructCsharpMethod(newClass, (JsonObject)methodArray.get(i));
        }

        return newClass;
    }

    public void ConstructCsharpMethod(SootClass clazz, JsonObject methodJson) {
        String functionName = methodJson.get("Name").getAsString();
        String signature = methodJson.get("Signature").getAsString();
        int modifier = methodJson.get("Modifier").getAsInt();

        JsonArray localsJson = methodJson.getAsJsonArray("Locals");
        JsonArray stmtsJson = methodJson.getAsJsonArray("Stmts");

        String[] split = signature.split("\\)");
        List<Type> parameterTypes = new ArrayList<>(); // ConvertParameterTypes(split[0]);
        String[] params = split[0].split(",");
        for(String param : params){
            parameterTypes.add(ConvertType(param.replaceAll("()","")));
        }

        Type returnType = ConvertType(split[1]);

        SootMethod newMethod = new SootMethod(functionName, parameterTypes, returnType, Modifier.PUBLIC);
        newMethod = clazz.getOrAddMethod(newMethod);

        JimpleBody activeBody = Jimple.v().newBody(newMethod);
        newMethod.setActiveBody(activeBody);

        Map<String, Local> locals = new HashMap<>();
        Local local = Jimple.v().newLocal("this", RefType.v(clazz));
        locals.put("this", local);
        activeBody.getLocals().add(local);
        activeBody.getUnits().add(Jimple.v().newIdentityStmt(local, Jimple.v().newThisRef(RefType.v(clazz))));

        int p = 0;
        for(Type paramType: parameterTypes){
            Local param = Jimple.v().newLocal("$p"+p, paramType);
            activeBody.getLocals().add(param);
            activeBody.getUnits().add(Jimple.v().newIdentityStmt(param, Jimple.v().newParameterRef(paramType, p)));
            p++;
        }

        for (JsonElement element: localsJson) {
            JsonObject localJson = element.getAsJsonObject();
            String name = localJson.get("Name").getAsString();
            String type = localJson.get("Type").getAsString();

            if(!name.equals("this")){
                local =  Jimple.v().newLocal(name, ConvertType(type));
                locals.put(local.getName(), local);
                activeBody.getLocals().add(local);
            }
        }

        for (JsonElement element: stmtsJson) {
            JsonObject stmtJson = element.getAsJsonObject();
            ResolveStmt(stmtJson, newMethod, locals);
        }

        if(returnType.toString().equals("void")){
            activeBody.getUnits().add(Jimple.v().newReturnVoidStmt());
        }
        else{
            Local $return = Jimple.v().newLocal("$return", newMethod.getReturnType());
            activeBody.getLocals().add($return);
            activeBody.getUnits().add(Jimple.v().newReturnStmt($return));
        }

        try {
            activeBody.validate();
        }
        catch(Exception e){
            System.out.println(e.getMessage());
        }

    }
    public void addEdges_dummyMainMethod(SootMethod tgtMethod){

       // AndroidEntryPointCreator androidEntryPointCreator = new AndroidEntryPointCreator();
        //TODO : private? read some information to construct activeBody (json or xml)
        SootClass dummyMainClass = Scene.v().getSootClass("dummyMainClass");
        SootMethod dummyMainMethod = dummyMainClass.getMethod("void dummyMainMethod(java.lang.String[])");
        JimpleBody dummyMainBody = (JimpleBody) dummyMainMethod.getActiveBody();

        InvokeStmt invoke1 = make_InvokeStmt(dummyMainMethod.getSignature(), null, tgtMethod.getSignature(), null, Kind.STATIC);

        var units = dummyMainBody.getUnits();
        Unit targetUnit = null;
        for (Unit unit : units) {
            if (unit instanceof ReturnVoidStmt) {
                targetUnit = unit;
                break;
            }
        }

        if (targetUnit != null) {
            units.insertBefore(invoke1, targetUnit);
        }

        try {
            dummyMainBody.validate();
        }
        catch (Exception e){
            System.out.println(e.getMessage());
        }


        Edge edge = new Edge(dummyMainMethod, invoke1, tgtMethod, Kind.VIRTUAL);
        cg.addEdge(edge);
    }
    public SootClass make_newClass(String className){
        //TODO : private? Sample_sinkInJava.MainActivity
        SootClass newClass = Scene.v().makeSootClass(className);
        newClass.setApplicationClass();
        Scene.v().addClass(newClass);
        return newClass;
    }
    public SootClass make_newClass(String className, String SupperClass){
        //TODO : private? Sample_sinkInJava.MainActivity
        SootClass newClass = Scene.v().makeSootClass(className);
        newClass.setSuperclass(Scene.v().getSootClass(SupperClass));
        Scene.v().addClass(newClass);
        return newClass;
    }
    public InvokeStmt make_InvokeStmt(String srcSignature, Local base, String tgtSignature, List<Value> parameters, Kind invokeKind){
       // SootMethod srcMethod = Scene.v().getMethod(srcSignature);
        if(Scene.v().containsMethod(tgtSignature) && Scene.v().containsMethod(srcSignature)) {
            SootMethod srcMethod = Scene.v().getMethod(srcSignature);
            SootMethod tgtMethod = Scene.v().getMethod(tgtSignature);
            InvokeStmt invokeStmt = null;


            if (tgtMethod.isStatic()) {
                if (parameters == null) {
                    invokeStmt = Jimple.v().newInvokeStmt(Jimple.v().newStaticInvokeExpr(tgtMethod.makeRef()));
                } else {
                    invokeStmt = Jimple.v().newInvokeStmt(Jimple.v().newStaticInvokeExpr(tgtMethod.makeRef(), parameters));
                }
            } else {
                if (invokeKind.isVirtual()) {
                    if (parameters == null) {
                        invokeStmt = Jimple.v().newInvokeStmt(Jimple.v().newVirtualInvokeExpr(base, tgtMethod.makeRef()));
                    } else {
                        invokeStmt = Jimple.v().newInvokeStmt(Jimple.v().newVirtualInvokeExpr(base, tgtMethod.makeRef(), parameters));
                    }
                }
                if (invokeKind.isSpecial()) {
                    if (parameters == null) {
                        invokeStmt = Jimple.v().newInvokeStmt(Jimple.v().newSpecialInvokeExpr(base, tgtMethod.makeRef()));
                    } else {
                        invokeStmt = Jimple.v().newInvokeStmt(Jimple.v().newSpecialInvokeExpr(base, tgtMethod.makeRef(), parameters));
                    }
                }
            }

            if (invokeStmt != null) {
                Edge edge = new Edge(srcMethod, invokeStmt, tgtMethod, invokeKind);
                cg.addEdge(edge);
            }
            return invokeStmt;
        }
        return null;
    }
    public InvokeExpr make_InvokeExpr(Local base, String tgtSignature, List<Value> parameters, Kind invokeKind){
        SootMethod tgtMethod = null;
        tgtMethod  = Scene.v().getMethod(tgtSignature);

        InvokeExpr invokeExpr = null;
        if(invokeKind.isVirtual()){
            if(parameters == null){
                invokeExpr = Jimple.v().newVirtualInvokeExpr(base, tgtMethod.makeRef());
            }
            else{
                invokeExpr = Jimple.v().newVirtualInvokeExpr(base, tgtMethod.makeRef(), parameters);
            }
        }
        if(invokeKind.isSpecial()){
            if(parameters == null){
                invokeExpr = Jimple.v().newSpecialInvokeExpr(base, tgtMethod.makeRef());
            }
            else{
                invokeExpr = Jimple.v().newSpecialInvokeExpr(base, tgtMethod.makeRef(), parameters);
            }
        }
        if(invokeKind.isStatic()){
            if(parameters == null){
                invokeExpr = Jimple.v().newStaticInvokeExpr(tgtMethod.makeRef());
            }
            else{
                invokeExpr = Jimple.v().newStaticInvokeExpr(tgtMethod.makeRef(), parameters);
            }
        }
        return invokeExpr;
    }
    public void ResolveStmt(JsonObject obj, SootMethod srcMethod, Map<String, Local> locals){
        Stmt result = null;
        String kind = obj.get("StmtKind").getAsString();
        Body activeBody = srcMethod.getActiveBody();

        int localCnt = 0;

        if(kind.equals("assign")){
            Local local = locals.get(obj.get("Local").getAsString());
            JsonObject stmtJson = obj.get("Stmt").getAsJsonObject();
            String subStmtKind = stmtJson.get("StmtKind").getAsString();

            if(subStmtKind.equals("new")){
                String typeString = stmtJson.get("Type").getAsString();
                if(local == null){
                    while(locals.containsKey("$v" + localCnt))
                        localCnt ++;

                    Local new_local = Jimple.v().newLocal("$v"+localCnt, RefType.v(typeString));
                    locals.put(new_local.getName(), new_local);
                    activeBody.getLocals().add(new_local);
                    local = new_local;
                }
                if(Scene.v().containsType(typeString)){
                    result =  Jimple.v().newAssignStmt(local, Jimple.v().newNewExpr((RefType) local.getType()));
                    activeBody.getUnits().add(result);

                    SootClass clazz = Scene.v().getSootClass(local.getType().toString());
//                    SootMethod method = clazz.getMethods().get(0);
//                    Stmt st = make_InvokeStmt(srcMethod.getSignature(), local, method.getSignature(), ResolveParameters(srcMethod, method,null, locals), Kind.SPECIAL);
//                    activeBody.getUnits().add(st);
                 //   System.out.println("DEBUG");
                }
            }
            else if(subStmtKind.equals("invoke")){
                String invokeKindStr = stmtJson.get("Kind").getAsString();
                Local base = null;
                if(!stmtJson.get("Base").isJsonNull())
                    base = locals.get(stmtJson.get("Base").getAsString());
                if(stmtJson.get("Tgt").isJsonNull())
                    return;
                String tgtSignature = stmtJson.get("Tgt").getAsString();
                if(!Scene.v().containsMethod(tgtSignature))
                    return;

                if(!Scene.v().getMethod(tgtSignature).isStatic() && base == null){
                    while(locals.containsKey("$r" + localCnt))
                        localCnt ++;

                    SootMethod tgtMethod = Scene.v().getMethod(tgtSignature);
                    Local new_base = Jimple.v().newLocal("$r"+localCnt, RefType.v(tgtMethod.getDeclaringClass()));
                    locals.put(new_base.getName(), new_base);
                    activeBody.getLocals().add(new_base);
                    activeBody.getUnits().add(Jimple.v().newAssignStmt(new_base, Jimple.v().newNewExpr(RefType.v(tgtMethod.getDeclaringClass()))));

                    SootClass clazz = Scene.v().getSootClass(new_base.getType().toString());
//                    SootMethod method = clazz.getMethods().get(0);
//                    Stmt st = make_InvokeStmt(srcMethod.getSignature(), new_base, method.getSignature(),  ResolveParameters(srcMethod, method,null, locals), Kind.SPECIAL);
//                    activeBody.getUnits().add(st);
                    base = new_base;
                }

                JsonArray parameterJson = stmtJson.get("Parameter").getAsJsonArray();
                Kind invokeKind = switch (invokeKindStr) {
                    case "Static" -> Kind.STATIC;
                    case "Virtual" -> Kind.VIRTUAL;
                    case "Special" -> Kind.SPECIAL;
                    default -> null;
                };


                if(Scene.v().containsMethod(tgtSignature)){
                    SootMethod tgtMethod = Scene.v().getMethod(tgtSignature);
                    if(local == null){
                        while(locals.containsKey("$v" + localCnt))
                            localCnt ++;

                        Local new_local = Jimple.v().newLocal("$v"+localCnt, tgtMethod.getReturnType());
                        locals.put(new_local.getName(), new_local);
                        activeBody.getLocals().add(new_local);
                        local = new_local;
                    }

                    result = Jimple.v().newAssignStmt(local,make_InvokeExpr(base,tgtSignature, ResolveParameters(srcMethod, tgtMethod, parameterJson, locals), invokeKind));
                    activeBody.getUnits().add(result);
                }
                else{
                    System.out.println();
                }
            }
        }
        if(kind.equals("invoke")){
            String invokeKindStr = obj.get("Kind").getAsString();
            Local base = null;
            if(!obj.get("Base").isJsonNull())
                base = locals.get(obj.get("Base").getAsString());
            String tgtSignature = obj.get("Tgt").getAsString();
            JsonArray parameterJson = obj.get("Parameter").getAsJsonArray();
            if(!Scene.v().containsMethod(tgtSignature)){
                return;
            }
            if(!Scene.v().getMethod(tgtSignature).isStatic() && (base == null || base.getName().equals("this"))){
                while(locals.containsKey("$v" + localCnt))
                    localCnt ++;

                SootMethod tgtMethod = Scene.v().getMethod(tgtSignature);
                Local new_base = Jimple.v().newLocal("$v"+localCnt, RefType.v(tgtMethod.getDeclaringClass()));
                locals.put(new_base.getName(), new_base);
                activeBody.getLocals().add(new_base);
                activeBody.getUnits().add(Jimple.v().newAssignStmt(new_base, Jimple.v().newNewExpr(RefType.v(tgtMethod.getDeclaringClass()))));


//                SootClass clazz = Scene.v().getSootClass(new_base.getType().toString());
//                SootMethod method = clazz.getMethods().get(0);
//                Stmt st = make_InvokeStmt(srcMethod.getSignature(), new_base, method.getSignature(),  ResolveParameters(srcMethod, method,null, locals), Kind.SPECIAL);
//                activeBody.getUnits().add(st);
                //System.out.println("DEBUG");

                base = new_base;
            }
            Kind invokeKind = switch (invokeKindStr) {
                case "Static" -> Kind.STATIC;
                case "Virtual" -> Kind.VIRTUAL;
                case "Special" -> Kind.SPECIAL;
                default -> null;
            };


            if(Scene.v().containsMethod(tgtSignature)){
                SootMethod tgtMethod = Scene.v().getMethod(tgtSignature);
                result = make_InvokeStmt(srcMethod.getSignature(), base, tgtSignature, ResolveParameters(srcMethod, tgtMethod, parameterJson, locals), invokeKind);
                activeBody.getUnits().add(result);
            }
            else{
                System.out.println();
            }

        }
    }
    public Type ConvertType(String s){
        Type type = VoidType.v();
        switch (s){
            case "void":
                type = VoidType.v();
                break;
            case "boolean":
                type = BooleanType.v();
                break;
            case "byte":
                type = ByteType.v();
                break;
            case "char":
                type = CharType.v();
                break;
            case "short":
                type = ShortType.v();
                break;
            case "int":
                type = IntType.v();
                break;
            case "long":
                type = LongType.v();
                break;
            case "float":
                type = FloatType.v();
                break;
            case "double":
                type = DoubleType.v();
                break;
            case "string":
                type = RefType.v("java.lang.String");
                break;
            case "object":
                type = RefType.v("java.lang.Object");
                break;
            default:
                if(Scene.v().containsType(s))
                    type = RefType.v(s);
                else
                    type = RefType.v("java.lang.Object");
        }


        return type;
    }

    public List<Value> ResolveParameters(SootMethod srcMethod, SootMethod tgtMethod, JsonArray parameterJson, Map<String, Local> locals) {
        List<Value> parameters = new ArrayList<>();
        String jsonLocal = null;

        for (int i = 0; i < tgtMethod.getParameterCount(); i++) {
            Type type = tgtMethod.getParameterType(i);
            Value param = null;
            if (parameterJson != null) {
                if(!parameterJson.isEmpty())
                    jsonLocal = parameterJson.get(i).getAsString();
                if (locals.containsKey(jsonLocal)) {
                    param = locals.get(jsonLocal);
                }
            }

            if (param == null) {
                String typeString = type.toString();
                int localCnt = 0;
                while(locals.containsKey("$v" + localCnt))
                    localCnt ++;

                switch (typeString) {
                    case "byte":
                    case "short":
                    case "int":
                        param = IntConstant.v(1);
                        break;
                    case "long":
                        param = LongConstant.v(12L);
                        break;
                    case "float":
                        param = FloatConstant.v(12.34F);
                        break;
                    case "double":
                        param = DoubleConstant.v(12.34);
                        break;
                    case "java.lang.String":
                        Local stringObject = Jimple.v().newLocal("$v" + localCnt, RefType.v("java.lang.String"));
                        locals.put(stringObject.getName(), stringObject);
                        srcMethod.getActiveBody().getLocals().add(stringObject);
                        srcMethod.getActiveBody().getUnits().add(Jimple.v().newAssignStmt(stringObject, Jimple.v().newNewExpr(RefType.v("java.lang.String"))));
                        param = stringObject;
                        break;
                    case "java.lang.Boolean":
                        Local booleanObject = Jimple.v().newLocal("$v" + localCnt, RefType.v("java.lang.Boolean"));
                        locals.put(booleanObject.getName(), booleanObject);
                        srcMethod.getActiveBody().getLocals().add(booleanObject);
                        srcMethod.getActiveBody().getUnits().add(Jimple.v().newAssignStmt(booleanObject, Jimple.v().newNewExpr(RefType.v("java.lang.Boolean"))));
                        param = booleanObject;
                        break;
                    default:
                        Local localObject = Jimple.v().newLocal("$v" + localCnt, RefType.v("java.lang.Object"));
                        locals.put(localObject.getName(), localObject);
                        srcMethod.getActiveBody().getLocals().add(localObject);
                        srcMethod.getActiveBody().getUnits().add(Jimple.v().newAssignStmt(localObject, Jimple.v().newNewExpr(RefType.v("java.lang.Object"))));
                        param = localObject;
                }
            }
            parameters.add(param);
        }
        return parameters;
    }

    public void WriteJimple(SootClass clazz){
        String folderPath = Main.outputDir + "\\Jimple\\" + clazz.toString();
        new File(folderPath).mkdirs();
        for(SootMethod method : clazz.getMethods()){
            String filePath = folderPath + "\\" + method.getName();
            try {
                File file = new File(filePath);
                FileWriter fileWriter = new FileWriter(file);
                BufferedWriter bufferedWriter = new BufferedWriter(fileWriter);

                String content = method.getActiveBody().toString();

                bufferedWriter.write(content);
                bufferedWriter.flush();
                bufferedWriter.close();
            } catch (IOException e) {
            //    System.out.println(e.getMessage());
            }
        }
    }
}
