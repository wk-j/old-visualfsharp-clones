// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

//----------------------------------------------------------------------------
// Write Abstract IL structures at runtime using Reflection.Emit
//----------------------------------------------------------------------------


module internal Microsoft.FSharp.Compiler.AbstractIL.ILRuntimeWriter    
  
open Internal.Utilities
open Microsoft.FSharp.Compiler.AbstractIL
open Microsoft.FSharp.Compiler.AbstractIL.Internal
open Microsoft.FSharp.Compiler.AbstractIL.Internal.Library
open Microsoft.FSharp.Compiler.AbstractIL.Diagnostics 
open Microsoft.FSharp.Compiler.AbstractIL.Extensions
open Microsoft.FSharp.Compiler.AbstractIL.Extensions.ILX
open Microsoft.FSharp.Compiler.AbstractIL.Extensions.ILX.Types
open Microsoft.FSharp.Compiler.AbstractIL.IL

open Microsoft.FSharp.Core.Printf

open System
open System.Reflection
open System.Reflection.Emit
open System.Runtime.InteropServices
open System.Collections.Generic

let codeLabelOrder = ComparisonIdentity.Structural<ILCodeLabel>

// Convert the output of convCustomAttr
open Microsoft.FSharp.Compiler.AbstractIL.ILAsciiWriter 
let wrapCustomAttr setCustomAttr (cinfo, bytes) =
    setCustomAttr(cinfo, bytes)


//----------------------------------------------------------------------------
// logging to enable debugging
//----------------------------------------------------------------------------

let logRefEmitCalls = false

type System.AppDomain with 
    member x.DefineDynamicAssemblyAndLog(asmName,flags,asmDir:string)  =
        let asmB = x.DefineDynamicAssembly(asmName,flags,asmDir)
        if logRefEmitCalls then 
            printfn "open System"
            printfn "open System.Reflection"
            printfn "open System.Reflection.Emit"
            printfn "let assemblyBuilder%d = System.AppDomain.CurrentDomain.DefineDynamicAssembly(AssemblyName(Name=\"%s\"),enum %d,%A)" (abs <| hash asmB) asmName.Name (LanguagePrimitives.EnumToValue flags) asmDir
        asmB
        

type System.Reflection.Emit.AssemblyBuilder with 
    member asmB.DefineDynamicModuleAndLog(a,b,c) =  
        let modB = asmB.DefineDynamicModule(a,b,c)
        if logRefEmitCalls then printfn "let moduleBuilder%d = assemblyBuilder%d.DefineDynamicModule(%A,%A,%A)" (abs <| hash modB) (abs <| hash asmB) a b c
        modB
        
    member asmB.SetCustomAttributeAndLog(cinfo,bytes)        = 
        if logRefEmitCalls then printfn "assemblyBuilder%d.SetCustomAttribute(%A, %A)" (abs <| hash asmB) cinfo bytes
        wrapCustomAttr asmB.SetCustomAttribute (cinfo, bytes)

    member asmB.AddResourceFileAndLog(nm1, nm2, attrs)        = 
        if logRefEmitCalls then printfn "assemblyBuilder%d.AddResourceFile(%A, %A, enum %d)" (abs <| hash asmB) nm1 nm2 (LanguagePrimitives.EnumToValue attrs)
        asmB.AddResourceFile(nm1,nm2,attrs)

    member asmB.SetCustomAttributeAndLog(cab)        = 
        if logRefEmitCalls then printfn "assemblyBuilder%d.SetCustomAttribute(%A)" (abs <| hash asmB) cab
        asmB.SetCustomAttribute(cab)


type System.Reflection.Emit.ModuleBuilder with 
    member modB.GetArrayMethodAndLog(aty,nm,flags,rty,tys) =
        if logRefEmitCalls then printfn "moduleBuilder%d.GetArrayMethod(%A,%A,%A,%A,%A)" (abs <| hash modB) aty nm flags rty tys
        modB.GetArrayMethod(aty,nm,flags,rty,tys)

    member modB.DefineDocumentAndLog(file,lang,vendor,doctype) =
        let symDoc = modB.DefineDocument(file,lang,vendor,doctype)
        if logRefEmitCalls then printfn "let docWriter%d = moduleBuilder%d.DefineDocument(%A,System.Guid(\"%A\"),System.Guid(\"%A\"),System.Guid(\"%A\"))" (abs <| hash symDoc)  (abs <| hash modB) file lang vendor doctype
        symDoc

    member modB.GetTypeAndLog(nameInModule,flag1,flag2) =
        if logRefEmitCalls then printfn "moduleBuilder%d.GetType(%A,%A,%A) |> ignore" (abs <| hash modB) nameInModule flag1 flag2
        modB.GetType(nameInModule,flag1,flag2)

    member modB.DefineTypeAndLog(name,attrs) =
        let typB = modB.DefineType(name,attrs)
        if logRefEmitCalls then printfn "let typeBuilder%d = moduleBuilder%d.DefineType(%A,enum %d)" (abs <| hash typB) (abs <| hash modB) name (LanguagePrimitives.EnumToValue attrs)
        typB
        
    member modB.DefineManifestResourceAndLog(name,stream,attrs) =
        if logRefEmitCalls then printfn "moduleBuilder%d.DefineManifestResource(%A,%A,enum %d)" (abs <| hash modB) name stream (LanguagePrimitives.EnumToValue attrs)
        modB.DefineManifestResource(name,stream,attrs)
        
    member modB.SetCustomAttributeAndLog(cinfo,bytes)        = 
        if logRefEmitCalls then printfn "moduleBuilder%d.SetCustomAttribute(%A, %A)" (abs <| hash modB) cinfo bytes
        wrapCustomAttr modB.SetCustomAttribute (cinfo,bytes)


type System.Reflection.Emit.ConstructorBuilder with 
    member consB.SetImplementationFlagsAndLog(attrs) =
        if logRefEmitCalls then printfn "constructorBuilder%d.SetImplementationFlags(enum %d)" (abs <| hash consB) (LanguagePrimitives.EnumToValue attrs)
        consB.SetImplementationFlags(attrs)

    member consB.DefineParameterAndLog(n,attr,nm) =
        if logRefEmitCalls then printfn "constructorBuilder%d.DefineParameter(%d,enum %d,%A)" (abs <| hash consB) n (LanguagePrimitives.EnumToValue attr) nm
        consB.DefineParameter(n,attr,nm)

    member consB.GetILGeneratorAndLog() =
        let ilG = consB.GetILGenerator()
        if logRefEmitCalls then printfn "let ilg%d = constructorBuilder%d.GetILGenerator()" (abs <| hash ilG) (abs <| hash consB) 
        ilG

type System.Reflection.Emit.MethodBuilder with 
    member methB.SetImplementationFlagsAndLog(attrs) =
        if logRefEmitCalls then printfn "methodBuilder%d.SetImplementationFlags(enum %d)" (abs <| hash methB) (LanguagePrimitives.EnumToValue attrs)
        methB.SetImplementationFlags(attrs)

    member methB.SetReturnTypeAndLog(rt:System.Type) =
        if logRefEmitCalls then printfn "methodBuilder%d.SetReturnType(typeof<%s>)" (abs <| hash methB) rt.FullName
        methB.SetReturnType(rt)

    member methB.SetParametersAndLog(ps) =
        if logRefEmitCalls then printfn "methodBuilder%d.SetParameters(%A)" (abs <| hash methB) ps
        methB.SetParameters(ps)

    member methB.DefineParameterAndLog(n,attr,nm) =
        if logRefEmitCalls then printfn "methodBuilder%d.DefineParameter(%d,enum %d,%A)" (abs <| hash methB) n (LanguagePrimitives.EnumToValue attr) nm
        methB.DefineParameter(n,attr,nm)

    member methB.DefineGenericParametersAndLog(gps) =
        if logRefEmitCalls then printfn "let gps%d = methodBuilder%d.DefineGenericParameters(%A)" (abs <| hash methB) (abs <| hash methB) gps
        methB.DefineGenericParameters(gps)

    member methB.GetILGeneratorAndLog() =
        let ilG = methB.GetILGenerator()
        if logRefEmitCalls then printfn "let ilg%d = methodBuilder%d.GetILGenerator()" (abs <| hash ilG) (abs <| hash methB) 
        ilG

    member methB.SetCustomAttributeAndLog(cinfo,bytes)        = 
        if logRefEmitCalls then printfn "methodBuilder%d.SetCustomAttribute(%A, %A)" (abs <| hash methB) cinfo bytes
        wrapCustomAttr methB.SetCustomAttribute (cinfo,bytes)




type System.Reflection.Emit.TypeBuilder with 
    member typB.CreateTypeAndLog() = 
        if logRefEmitCalls then printfn "typeBuilder%d.CreateType()" (abs <| hash typB)
        typB.CreateType()

    member typB.DefineNestedTypeAndLog(name,attrs)        = 
        let res = typB.DefineNestedType(name,attrs)
        if logRefEmitCalls then printfn "let typeBuilder%d = typeBuilder%d.DefineNestedType(\"%s\",enum %d)" (abs <| hash res) (abs <| hash typB) name (LanguagePrimitives.EnumToValue attrs)
        res
    
    member typB.DefineMethodAndLog(name,attrs,cconv)        = 
        let methB = typB.DefineMethod(name,attrs,cconv)
        if logRefEmitCalls then printfn "let methodBuilder%d = typeBuilder%d.DefineMethod(\"%s\",enum %d,enum %d)" (abs <| hash methB) (abs <| hash typB) name (LanguagePrimitives.EnumToValue attrs) (LanguagePrimitives.EnumToValue cconv)
        methB

    member typB.DefineGenericParametersAndLog(gps)        = 
        if logRefEmitCalls then printfn "typeBuilder%d.DefineGenericParameters(%A)" (abs <| hash typB) gps
        typB.DefineGenericParameters(gps)

    member typB.DefineConstructorAndLog(attrs,cconv,parms)        = 
        let consB = typB.DefineConstructor(attrs,cconv,parms)
        if logRefEmitCalls then printfn "let constructorBuilder%d = typeBuilder%d.DefineConstructor(enum %d,%A,%A)" (abs <| hash consB) (abs <| hash typB) (LanguagePrimitives.EnumToValue attrs) cconv parms
        consB

    member typB.DefineFieldAndLog(nm,ty:System.Type,attrs)        = 
        if logRefEmitCalls then printfn "typeBuilder%d.DefineField(\"%s\",typeof<%s>,enum %d)" (abs <| hash typB) nm ty.FullName (LanguagePrimitives.EnumToValue attrs)
        typB.DefineField(nm,ty,attrs)

    member typB.DefinePropertyAndLog(nm,attrs,ty,args)        = 
        if logRefEmitCalls then printfn "typeBuilder%d.DefineProperty(\"%A\",enum %d,%A,%A)" (abs <| hash typB) nm (LanguagePrimitives.EnumToValue attrs) ty args
        typB.DefineProperty(nm,attrs,ty,args)

    member typB.DefineEventAndLog(nm,attrs,ty)        = 
        if logRefEmitCalls then printfn "typeBuilder%d.DefineEvent(\"%A\",enum %d,%A)" (abs <| hash typB) nm (LanguagePrimitives.EnumToValue attrs) ty
        typB.DefineEvent(nm,attrs,ty)

    member typB.SetParentAndLog(ty:System.Type)        = 
        if logRefEmitCalls then printfn "typeBuilder%d.SetParent(typeof<%s>)" (abs <| hash typB) ty.FullName
        typB.SetParent(ty)

    member typB.AddInterfaceImplementationAndLog(ty)        = 
        if logRefEmitCalls then printfn "typeBuilder%d.AddInterfaceImplementation(%A)" (abs <| hash typB) ty
        typB.AddInterfaceImplementation(ty)

    member typB.InvokeMemberAndLog(nm,flags,args)        = 
        if logRefEmitCalls then printfn "typeBuilder%d.InvokeMember(\"%s\",enum %d,null,null,%A,Globalization.CultureInfo.InvariantCulture)" (abs <| hash typB) nm (LanguagePrimitives.EnumToValue flags) args     
        typB.InvokeMember(nm,flags,null,null,args,Globalization.CultureInfo.InvariantCulture)

    member typB.SetCustomAttributeAndLog(cinfo,bytes)        = 
        if logRefEmitCalls then printfn "typeBuilder%d.SetCustomAttribute(%A, %A)" (abs <| hash typB) cinfo bytes
        wrapCustomAttr typB.SetCustomAttribute (cinfo,bytes)


type System.Reflection.Emit.OpCode with 
    member opcode.RefEmitName = (string (System.Char.ToUpper(opcode.Name.[0])) +  opcode.Name.[1..]).Replace(".","_").Replace("_i4","_I4")

type System.Reflection.Emit.ILGenerator with 
    member ilG.DeclareLocalAndLog(ty:System.Type,isPinned) = 
        if logRefEmitCalls then printfn "ilg%d.DeclareLocal(typeof<%s>,%b)" (abs <| hash ilG) ty.FullName isPinned
        ilG.DeclareLocal(ty,isPinned)

    member ilG.MarkLabelAndLog(lab) = 
        if logRefEmitCalls then printfn "ilg%d.MarkLabel(label%d_%d)" (abs <| hash ilG) (abs <| hash ilG) (abs <| hash lab)
        ilG.MarkLabel(lab)

    member ilG.MarkSequencePointAndLog(symDoc, l1, c1, l2, c2) = 
        if logRefEmitCalls then printfn "ilg%d.MarkSequencePoint(docWriter%d, %A, %A, %A, %A)" (abs <| hash ilG) (abs <| hash symDoc) l1 c1 l2 c2
        ilG.MarkSequencePoint(symDoc, l1, c1, l2, c2)

    member ilG.BeginExceptionBlockAndLog() = 
        if logRefEmitCalls then printfn "ilg%d.BeginExceptionBlock()" (abs <| hash ilG) 
        ilG.BeginExceptionBlock()

    member ilG.EndExceptionBlockAndLog() = 
        if logRefEmitCalls then printfn "ilg%d.EndExceptionBlock()" (abs <| hash ilG) 
        ilG.EndExceptionBlock()

    member ilG.BeginFinallyBlockAndLog() = 
        if logRefEmitCalls then printfn "ilg%d.BeginFinallyBlock()" (abs <| hash ilG) 
        ilG.BeginFinallyBlock()

    member ilG.BeginCatchBlockAndLog(ty) = 
        if logRefEmitCalls then printfn "ilg%d.BeginCatchBlock(%A)" (abs <| hash ilG)  ty
        ilG.BeginCatchBlock(ty)

    member ilG.BeginExceptFilterBlockAndLog() = 
        if logRefEmitCalls then printfn "ilg%d.BeginExceptFilterBlock()" (abs <| hash ilG)  
        ilG.BeginExceptFilterBlock()

    member ilG.BeginFaultBlockAndLog() = 
        if logRefEmitCalls then printfn "ilg%d.BeginFaultBlock()" (abs <| hash ilG) 
        ilG.BeginFaultBlock()

    member ilG.DefineLabelAndLog() = 
        let lab = ilG.DefineLabel()
        if logRefEmitCalls then printfn "let label%d_%d = ilg%d.DefineLabel()" (abs <| hash ilG) (abs <| hash lab) (abs <| hash ilG) 
        lab

    member x.EmitAndLog (op:OpCode) = 
        if logRefEmitCalls then printfn "ilg%d.Emit(OpCodes.%s)" (abs <| hash x) op.RefEmitName
        x.Emit(op) 
    member x.EmitAndLog (op:OpCode,v:Label) = 
        if logRefEmitCalls then printfn "ilg%d.Emit(OpCodes.%s,label%d_%d)" (abs <| hash x) op.RefEmitName (abs <| hash x) (abs <| hash v); 
        x.Emit(op,v)
    member x.EmitAndLog (op:OpCode,v:int16) = 
        if logRefEmitCalls then printfn "ilg%d.Emit(OpCodes.%s, int16 %d)" (abs <| hash x) op.RefEmitName v; 
        x.Emit(op,v)
    member x.EmitAndLog (op:OpCode,v:int32) = 
        if logRefEmitCalls then printfn "ilg%d.Emit(OpCodes.%s, %d)" (abs <| hash x) op.RefEmitName v; 
        x.Emit(op,v)
    member x.EmitAndLog (op:OpCode,v:MethodInfo) = 
        if logRefEmitCalls then printfn "ilg%d.Emit(OpCodes.%s, meth_%s)" (abs <| hash x) op.RefEmitName v.Name; 
        x.Emit(op,v)
    member x.EmitAndLog (op:OpCode,v:string) = 
        if logRefEmitCalls then printfn "ilg%d.Emit(OpCodes.%s,\"%s\")" (abs <| hash x) op.RefEmitName v; 
        x.Emit(op,v)
    member x.EmitAndLog (op:OpCode,v:Type) = 
        if logRefEmitCalls then printfn "ilg%d.Emit(OpCodes.%s, typeof<%s>)" (abs <| hash x) op.RefEmitName v.FullName; 
        x.Emit(op,v)
    member x.EmitAndLog (op:OpCode,v:FieldInfo) = 
        if logRefEmitCalls then printfn "ilg%d.Emit(OpCodes.%s, field_%s)" (abs <| hash x) op.RefEmitName v.Name; 
        x.Emit(op,v)
    member x.EmitAndLog (op:OpCode,v:ConstructorInfo) = 
        if logRefEmitCalls then printfn "ilg%d.Emit(OpCodes.%s,constructor_%s)" (abs <| hash x) op.RefEmitName v.DeclaringType.Name; 
        x.Emit(op,v)
 

//----------------------------------------------------------------------------
// misc
//----------------------------------------------------------------------------

let inline flagsIf  b x  = if b then x else enum 0

module Zmap = 
    let force x m str = match Zmap.tryFind x m with Some y -> y | None -> failwithf "Zmap.force: %s: x = %+A" str x

let equalTypes (s:Type) (t:Type) = s.Equals(t)
let equalTypeLists ss tt =  List.lengthsEqAndForall2 equalTypes ss tt

let getGenericArgumentsOfType (typT : Type) = 
    if typT .IsGenericType   then typT .GetGenericArguments() else [| |]
let getGenericArgumentsOfMethod (methI : MethodInfo) = 
    if methI.IsGenericMethod then methI.GetGenericArguments() else [| |] 

let getTypeConstructor (ty: Type) = 
    if ty.IsGenericType then ty.GetGenericTypeDefinition() else ty

//----------------------------------------------------------------------------
// convAssemblyRef
//----------------------------------------------------------------------------

let convAssemblyRef (aref:ILAssemblyRef) = 
    let asmName = new System.Reflection.AssemblyName()
    asmName.Name    <- aref.Name;
    (match aref.PublicKey with 
     | None -> ()
     | Some (PublicKey      bytes) -> asmName.SetPublicKey(bytes)
     | Some (PublicKeyToken bytes) -> asmName.SetPublicKeyToken(bytes));
    let setVersion (major,minor,build,rev) = 
       asmName.Version <- System.Version (int32 major,int32 minor,int32 build, int32 rev)
    Option.iter setVersion aref.Version;
    //  asmName.ProcessorArchitecture <- System.Reflection.ProcessorArchitecture.MSIL;
    //Option.iter (fun name -> asmName.CultureInfo <- System.Globalization.CultureInfo.CreateSpecificCulture(name)) aref.Locale;
    asmName.CultureInfo <- System.Globalization.CultureInfo.InvariantCulture;
    asmName

/// The global environment
type cenv = 
    { ilg: ILGlobals; 
      generatePdb: bool;
      resolvePath: (ILAssemblyRef -> Choice<string,System.Reflection.Assembly> option) }

/// Convert an Abstract IL type reference to Reflection.Emit System.Type value
// REVIEW: This ought to be an adequate substitute for this whole function, but it needs 
// to be thoroughly tested.
//    Type.GetType(tref.QualifiedName) 
// []              ,name -> name
// [ns]            ,name -> ns+name
// [ns;typeA;typeB],name -> ns+typeA+typeB+name
let getTRefType (cenv:cenv) (tref:ILTypeRef) = 

    // If an inner nested type's name contains a space, the proper encoding is "\+" on both sides - otherwise,
    // we use "+"
    let rec collectPrefixParts (l : string list) (acc : string list) =
        match l with
        | h1 :: (h2 :: _ as tl) ->
            collectPrefixParts tl 
                (List.append
                    acc
                    [   yield h1
                        if h1.Contains(" ") || h2.Contains(" ") then
                            yield "\\+"
                        else
                            yield "+"])
        | h :: [] -> List.append acc [h]
        | _ -> acc

    let prefix = collectPrefixParts tref.Enclosing [] |> List.fold (fun (s1 : string) (s2 : string) -> s1 + s2) ""
    let qualifiedName = prefix + (if prefix <> "" then (if tref.Name.Contains(" ") then "\\+" else "+") else "") + tref.Name  // e.g. Name.Space.Class+NestedClass
    match tref.Scope with
    | ILScopeRef.Assembly asmref ->
        let assembly = 
            match cenv.resolvePath asmref with                     
            | Some (Choice1Of2 path) ->
                FileSystem.AssemblyLoadFrom(path)              
            | Some (Choice2Of2 assembly) ->
                assembly
            | None ->
                let asmName    = convAssemblyRef asmref
                FileSystem.AssemblyLoad(asmName)
        let typT       = assembly.GetType(qualifiedName)
        typT |> nonNull "GetTRefType" 
    | ILScopeRef.Module _ 
    | ILScopeRef.Local _ ->
        let typT = Type.GetType(qualifiedName,true) 
        typT |> nonNull "GetTRefType" 



/// The (local) emitter env (state). Some of these fields are effectively global accumulators
/// and could be placed as hash tables in the global environment.
[<AutoSerializable(false)>]
type emEnv =
    { emTypMap   : Zmap<ILTypeRef,Type * TypeBuilder * ILTypeDef * Type option (*the created type*) > ;
      emConsMap  : Zmap<ILMethodRef,ConstructorBuilder>;    
      emMethMap  : Zmap<ILMethodRef,MethodBuilder>;
      emFieldMap : Zmap<ILFieldRef,FieldBuilder>;
      emPropMap  : Zmap<ILPropertyRef,PropertyBuilder>;
      emLocals   : LocalBuilder[];
      emLabels   : Zmap<IL.ILCodeLabel,Label>;
      emTyvars   : Type[] list; // stack
      emEntryPts : (TypeBuilder * string) list
      delayedFieldInits :  (unit -> unit) list}
  
let orderILTypeRef      = ComparisonIdentity.Structural<ILTypeRef>
let orderILMethodRef    = ComparisonIdentity.Structural<ILMethodRef>
let orderILFieldRef     = ComparisonIdentity.Structural<ILFieldRef> 
let orderILPropertyRef  = ComparisonIdentity.Structural<ILPropertyRef>

let emEnv0 = 
    { emTypMap   = Zmap.empty orderILTypeRef;
      emConsMap  = Zmap.empty orderILMethodRef;
      emMethMap  = Zmap.empty orderILMethodRef;
      emFieldMap = Zmap.empty orderILFieldRef;
      emPropMap = Zmap.empty orderILPropertyRef;
      emLocals   = [| |];
      emLabels   = Zmap.empty codeLabelOrder;
      emTyvars   = [];
      emEntryPts = []
      delayedFieldInits = [] }

let envBindTypeRef emEnv (tref:ILTypeRef) (typT,typB,typeDef)= 
    match typT with 
    | null -> failwithf "binding null type in envBindTypeRef: %s\n" tref.Name;
    | _ -> {emEnv with emTypMap = Zmap.add tref (typT,typB,typeDef,None) emEnv.emTypMap}

let envUpdateCreatedTypeRef emEnv (tref:ILTypeRef) =
    // The tref's TypeBuilder has been created, so we have a Type proper.
    // Update the tables to include this created type (the typT held prior to this is (i think) actually (TypeBuilder :> Type).
    // The (TypeBuilder :> Type) does not implement all the methods that a Type proper does.
    let typT,typB,typeDef,_createdTypOpt = Zmap.force tref emEnv.emTypMap "envGetTypeDef: failed"
    if typB.IsCreated() then
        let typ = typB.CreateTypeAndLog()
        // Bug DevDev2 40395: Mono 2.6 and 2.8 has a bug where executing code that includes an array type
        // match "match x with :? C[] -> ..." before the full loading of an object of type
        // causes a failure when C is later loaded. One workaround for this is to attempt to do a fake allocation
        // of objects. We use System.Runtime.Serialization.FormatterServices.GetUninitializedObject to do
        // the fake allocation - this creates an "empty" object, even if the object doesn't have 
        // a constructor. It is not usable in partial trust code.
        if runningOnMono && typ.IsClass && not typ.IsAbstract && not typ.IsGenericType && not typ.IsGenericTypeDefinition then 
            try 
              System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typ) |> ignore
            with e -> ()

        {emEnv with emTypMap = Zmap.add tref (typT,typB,typeDef,Some typ) emEnv.emTypMap}
    else
#if DEBUG
        printf "envUpdateCreatedTypeRef: expected type to be created\n";
#endif
        emEnv

let envGetTypT cenv emEnv preferCreated (tref:ILTypeRef) = 
    match Zmap.tryFind tref emEnv.emTypMap with
    | Some (_typT,_typB,_typeDef,Some createdTyp) when preferCreated -> createdTyp |> nonNull "envGetTypT: null create type table?"
    | Some (typT,_typB,_typeDef,_)                                  -> typT       |> nonNull "envGetTypT: null type table?"
    | None                                                        -> getTRefType cenv tref 

let envBindConsRef emEnv (mref:ILMethodRef) consB = 
    {emEnv with emConsMap = Zmap.add mref consB emEnv.emConsMap}

let envGetConsB emEnv (mref:ILMethodRef) = 
    Zmap.force mref emEnv.emConsMap "envGetConsB: failed"

let envBindMethodRef emEnv (mref:ILMethodRef) methB = 
    {emEnv with emMethMap = Zmap.add mref methB emEnv.emMethMap}

let envGetMethB emEnv (mref:ILMethodRef) = 
    Zmap.force mref emEnv.emMethMap "envGetMethB: failed"

let envBindFieldRef emEnv fref fieldB = 
    {emEnv with emFieldMap = Zmap.add fref fieldB emEnv.emFieldMap}

let envGetFieldB emEnv fref =
    Zmap.force fref emEnv.emFieldMap "- envGetMethB: failed"
      
let envBindPropRef emEnv (pref:ILPropertyRef) propB = 
    {emEnv with emPropMap = Zmap.add pref propB emEnv.emPropMap}

let envGetPropB emEnv pref =
    Zmap.force pref emEnv.emPropMap "- envGetPropB: failed"
      
let envGetTypB emEnv (tref:ILTypeRef) = 
    Zmap.force tref emEnv.emTypMap "envGetTypB: failed"
    |> (fun (_typT,typB,_typeDef,_createdTypOpt) -> typB)
                 
let envGetTypeDef emEnv (tref:ILTypeRef) = 
    Zmap.force tref emEnv.emTypMap "envGetTypeDef: failed"
    |> (fun (_typT,_typB,typeDef,_createdTypOpt) -> typeDef)
                 
let envSetLocals emEnv locs = assert (emEnv.emLocals.Length = 0); // check "locals" is not yet set (scopes once only)
                              {emEnv with emLocals = locs}
let envGetLocal  emEnv i    = emEnv.emLocals.[i] // implicit bounds checking

let envSetLabel emEnv name lab =
    assert (not (Zmap.mem name emEnv.emLabels));
    {emEnv with emLabels = Zmap.add name lab emEnv.emLabels}
    
let envGetLabel emEnv name = 
    Zmap.find name emEnv.emLabels

let envPushTyvars emEnv typs =  {emEnv with emTyvars = typs :: emEnv.emTyvars}
let envPopTyvars  emEnv      =  {emEnv with emTyvars = List.tail emEnv.emTyvars}
let envGetTyvar   emEnv u16  =  
    match emEnv.emTyvars with
    | []     -> failwith "envGetTyvar: not scope of type vars"
    | tvs::_ -> let i = int32 u16 
                if i<0 || i>= Array.length tvs then
                    failwith (sprintf "want tyvar #%d, but only had %d tyvars" i (Array.length tvs))
                else
                    tvs.[i]

let isEmittedTypeRef emEnv tref = Zmap.mem tref emEnv.emTypMap

let envAddEntryPt  emEnv mref = {emEnv with emEntryPts = mref::emEnv.emEntryPts}
let envPopEntryPts emEnv      = {emEnv with emEntryPts = []},emEnv.emEntryPts

//----------------------------------------------------------------------------
// convCallConv
//----------------------------------------------------------------------------

let convCallConv (Callconv (hasThis,basic)) =
    let ccA = match hasThis with ILThisConvention.Static            -> CallingConventions.Standard
                               | ILThisConvention.InstanceExplicit -> CallingConventions.ExplicitThis
                               | ILThisConvention.Instance          -> CallingConventions.HasThis
    let ccB = match basic with   ILArgConvention.Default  -> enum 0
                               | ILArgConvention.CDecl    -> enum 0
                               | ILArgConvention.StdCall  -> enum 0
                               | ILArgConvention.ThisCall -> enum 0 // XXX: check all these
                               | ILArgConvention.FastCall -> enum 0
                               | ILArgConvention.VarArg   -> CallingConventions.VarArgs
    ccA ||| ccB


//----------------------------------------------------------------------------
// convType
//----------------------------------------------------------------------------

let rec convTypeSpec cenv emEnv preferCreated (tspec:ILTypeSpec) =
    let typT   = envGetTypT cenv emEnv preferCreated tspec.TypeRef 
    let tyargs = ILList.map (convTypeAux cenv emEnv preferCreated) tspec.GenericArgs
    match ILList.isEmpty tyargs,typT.IsGenericType with
    | _   ,true  -> typT.MakeGenericType(ILList.toArray tyargs)   |> nonNull "convTypeSpec: generic" 
    | true,false -> typT                                          |> nonNull "convTypeSpec: non generic" 
    | _   ,false -> failwithf "- convTypeSpec: non-generic type '%O' has type instance of length %d?" typT tyargs.Length 
      
and convTypeAux cenv emEnv preferCreated typ =
    match typ with
    | ILType.Void               -> Type.GetType("System.Void",true)
    | ILType.Array (shape,eltType) -> 
        let baseT = convTypeAux cenv emEnv preferCreated eltType |> nonNull "convType: array base"
        let nDims = shape.Rank
        // MakeArrayType()  returns "eltType[]"
        // MakeArrayType(1) returns "eltType[*]"
        // MakeArrayType(2) returns "eltType[,]"
        // MakeArrayType(3) returns "eltType[,,]"
        // All non-equal.
        if nDims=1
        then baseT.MakeArrayType() 
        else baseT.MakeArrayType shape.Rank
    | ILType.Value tspec        -> convTypeSpec cenv emEnv preferCreated tspec              |> nonNull "convType: value"
    | ILType.Boxed tspec        -> convTypeSpec cenv emEnv preferCreated tspec             |> nonNull "convType: boxed"
    | ILType.Ptr eltType        -> let baseT = convTypeAux cenv emEnv preferCreated eltType  |> nonNull "convType: ptr eltType"
                                   baseT.MakePointerType()                             |> nonNull "convType: ptr" 
    | ILType.Byref eltType      -> let baseT = convTypeAux cenv emEnv preferCreated eltType |> nonNull "convType: byref eltType"
                                   baseT.MakeByRefType()                               |> nonNull "convType: byref" 
    | ILType.TypeVar tv         -> envGetTyvar emEnv tv                                |> nonNull "convType: tyvar" 
  // XXX: REVIEW: complete the following cases.                                                        
    | ILType.Modified (false, _, modifiedTy)  -> convTypeAux cenv emEnv preferCreated modifiedTy
    | ILType.Modified (true, _, _) -> failwith "convType: modreq"
    | ILType.FunctionPointer _callsig -> failwith "convType: fptr"


// [Bug 4063].
// The convType functions convert AbsIL types into concrete Type values.
// The emitted types have (TypeBuilder:>Type) and (TypeBuilderInstantiation:>Type).
// These can be used to construct the concrete Type for a given AbsIL type.
// This is the convType function.
// Certain functions here, e.g. convMethodRef, convConstructorSpec assume they get the "Builders" for emitted types.
//
// The "LookupType" function (see end of file) provides AbsIL to Type lookup (post emit).
// The external use (reflection and pretty printing) requires the created Type (rather than the builder).
// convCreatedType ensures created types are used where possible.
// Note: typeBuilder.CreateType() freezes the type and makes a proper Type for the collected information.
//------  
// REVIEW: "convType becomes convCreatedType", the functions could be combined.
// If convCreatedType replaced convType functions like convMethodRef, convConstructorSpec, ... (and more?)
// will need to be fixed for emitted types to handle both TypeBuilder and later Type proper.
  
/// Uses TypeBuilder/TypeBuilderInstantiation for emitted types
let convType cenv emEnv typ = convTypeAux cenv emEnv false typ

let convTypes cenv emEnv (typs:ILTypes) = ILList.map (convType cenv emEnv) typs

let convTypesToArray cenv emEnv (typs:ILTypes) = convTypes cenv emEnv typs |> ILList.toArray 

/// Uses the .CreateType() for emitted type (if available)
let convCreatedType cenv emEnv typ = convTypeAux cenv emEnv true typ 
  

//----------------------------------------------------------------------------
// convFieldInit
//----------------------------------------------------------------------------

let convFieldInit x = 
    match x with 
    | ILFieldInit.String s       -> box s
    | ILFieldInit.Bool bool      -> box bool   
    | ILFieldInit.Char u16       -> box (char (int u16))  
    | ILFieldInit.Int8 i8        -> box i8     
    | ILFieldInit.Int16 i16      -> box i16    
    | ILFieldInit.Int32 i32      -> box i32    
    | ILFieldInit.Int64 i64      -> box i64    
    | ILFieldInit.UInt8 u8       -> box u8     
    | ILFieldInit.UInt16 u16     -> box u16    
    | ILFieldInit.UInt32 u32     -> box u32    
    | ILFieldInit.UInt64 u64     -> box u64    
    | ILFieldInit.Single ieee32 -> box ieee32 
    | ILFieldInit.Double ieee64 -> box ieee64 
    | ILFieldInit.Null            -> (null :> Object)

//----------------------------------------------------------------------------
// Some types require hard work...
//----------------------------------------------------------------------------

// This is gross. TypeBuilderInstantiation should really be a public type, since we
// have to use alternative means for various Method/Field/Constructor lookups.  However since 
// it isn't we resort to this technique...
let TypeBuilderInstantiationT = Type.GetType("System.Reflection.Emit.TypeBuilderInstantiation" )

let typeIsNotQueryable (typ : Type) =
    (typ :? TypeBuilder) || ((typ.GetType()).Equals(TypeBuilderInstantiationT))

//----------------------------------------------------------------------------
// convFieldSpec
//----------------------------------------------------------------------------

let queryableTypeGetField _emEnv (parentT:Type) (fref: ILFieldRef)  =
    parentT.GetField(fref.Name, BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance ||| BindingFlags.Static )  
        |> nonNull "queryableTypeGetField"
    
let nonQueryableTypeGetField (parentTI:Type) (fieldInfo : FieldInfo) : FieldInfo = 
    if parentTI.IsGenericType then TypeBuilder.GetField(parentTI,fieldInfo) else fieldInfo


let convFieldSpec cenv emEnv fspec =
    let fref = fspec.FieldRef
    let tref = fref.EnclosingTypeRef 
    let parentTI = convType cenv emEnv fspec.EnclosingType
    if isEmittedTypeRef emEnv tref then
        // NOTE: if "convType becomes convCreatedType", then handle queryable types here too. [bug 4063] (necessary? what repro?)
        let fieldB = envGetFieldB emEnv fref
        nonQueryableTypeGetField parentTI fieldB
    else
        // Prior type.
        if typeIsNotQueryable parentTI then 
            let parentT = getTypeConstructor parentTI
            let fieldInfo = queryableTypeGetField emEnv parentT  fref 
            nonQueryableTypeGetField parentTI fieldInfo
        else 
            queryableTypeGetField emEnv parentTI fspec.FieldRef

//----------------------------------------------------------------------------
// convMethodRef
//----------------------------------------------------------------------------

let queryableTypeGetMethodBySearch cenv emEnv parentT (mref:ILMethodRef) =
    assert(not (typeIsNotQueryable(parentT)));
    let cconv = (if mref.CallingConv.IsStatic then BindingFlags.Static else BindingFlags.Instance)
    let methInfos = parentT.GetMethods(cconv ||| BindingFlags.Public ||| BindingFlags.NonPublic) |> Array.toList
      (* First, filter on name, if unique, then binding "done" *)
    let tyargTs = getGenericArgumentsOfType parentT      
    let methInfos = methInfos |> List.filter (fun methInfo -> methInfo.Name = mref.Name)
    match methInfos with 
    | [methInfo] -> 
        methInfo
    | _ ->
      (* Second, type match. Note type erased (non-generic) F# code would not type match but they have unique names *)
        let select (methInfo:MethodInfo) =
            (* mref implied Types *)
            let mtyargTIs = getGenericArgumentsOfMethod methInfo 
            if mtyargTIs.Length <> mref.GenericArity then false (* method generic arity mismatch *) else
            let argTs,resT = 
                let emEnv = envPushTyvars emEnv (Array.append tyargTs mtyargTIs)
                let argTs = convTypes cenv emEnv mref.ArgTypes
                let resT  = convType cenv emEnv mref.ReturnType
                argTs,resT 
          
          (* methInfo implied Types *)
            let haveArgTs = methInfo.GetParameters() |> Array.toList |> List.map (fun param -> param.ParameterType) 
         
            let haveResT  = methInfo.ReturnType
          (* check for match *)
            if argTs.Length <> haveArgTs.Length then false (* method argument length mismatch *) else
            let res = equalTypes resT haveResT && equalTypeLists (ILList.toList argTs) haveArgTs
            res
       
        match List.tryFind select methInfos with
        | None          -> failwith "convMethodRef: could not bind to method"
        | Some methInfo -> methInfo (* return MethodInfo for (generic) type's (generic) method *)
                           |> nonNull "convMethodRef"
          
let queryableTypeGetMethod cenv emEnv parentT (mref:ILMethodRef) =
    assert(not (typeIsNotQueryable(parentT)));
    if mref.GenericArity = 0 then 
        let tyargTs = getGenericArgumentsOfType parentT      
        let argTs,resT = 
            let emEnv = envPushTyvars emEnv tyargTs
            let argTs = convTypesToArray cenv emEnv mref.ArgTypes
            let resT  = convType cenv emEnv mref.ReturnType
            argTs,resT 
        let stat = mref.CallingConv.IsStatic
        let cconv = (if stat then BindingFlags.Static else BindingFlags.Instance)
        let methInfo = 
            try 
              parentT.GetMethod(mref.Name,cconv ||| BindingFlags.Public ||| BindingFlags.NonPublic,
                                null,
                                argTs,
                                (null:ParameterModifier[])) 
            // This can fail if there is an ambiguity w.r.t. return type 
            with _ -> null
        if (isNonNull methInfo && equalTypes resT methInfo.ReturnType) then 
             methInfo
        else
             queryableTypeGetMethodBySearch cenv emEnv parentT mref
    else 
        queryableTypeGetMethodBySearch cenv emEnv parentT mref

let nonQueryableTypeGetMethod (parentTI:Type) (methInfo : MethodInfo) : MethodInfo = 
    if (parentTI.IsGenericType &&
        not (equalTypes parentTI (getTypeConstructor parentTI))) 
    then TypeBuilder.GetMethod(parentTI,methInfo )
    else methInfo 

let convMethodRef cenv emEnv (parentTI:Type) (mref:ILMethodRef) =
    let parent = mref.EnclosingTypeRef
    if isEmittedTypeRef emEnv parent then
        // NOTE: if "convType becomes convCreatedType", then handle queryable types here too. [bug 4063]      
        // Emitted type, can get fully generic MethodBuilder from env.
        let methB = envGetMethB emEnv mref
        nonQueryableTypeGetMethod parentTI methB
        |> nonNull "convMethodRef (emitted)"
    else
        // Prior type.
        if typeIsNotQueryable parentTI then 
            let parentT = getTypeConstructor parentTI
            let methInfo = queryableTypeGetMethod cenv emEnv parentT mref 
            nonQueryableTypeGetMethod parentTI methInfo
        else 
            queryableTypeGetMethod cenv emEnv parentTI mref 

//----------------------------------------------------------------------------
// convMethodSpec
//----------------------------------------------------------------------------
      
let convMethodSpec cenv emEnv (mspec:ILMethodSpec) =
    let typT     = convType cenv emEnv mspec.EnclosingType       (* (instanced) parent Type *)
    let methInfo = convMethodRef cenv emEnv typT mspec.MethodRef (* (generic)   method of (generic) parent *)
    let methInfo =
        if mspec.GenericArgs.Length = 0 then 
            methInfo // non generic 
        else 
            let minstTs  = convTypesToArray cenv emEnv mspec.GenericArgs
            let methInfo = methInfo.MakeGenericMethod minstTs // instantiate method 
            methInfo
    methInfo |> nonNull "convMethodSpec"

//----------------------------------------------------------------------------
// - QueryableTypeGetConstructors: get a constructor on a non-TypeBuilder type
//----------------------------------------------------------------------------

let queryableTypeGetConstructor cenv emEnv (parentT:Type) (mref:ILMethodRef)  =
    let tyargTs  = getGenericArgumentsOfType parentT
    let reqArgTs  = 
        let emEnv = envPushTyvars emEnv tyargTs
        convTypesToArray cenv emEnv mref.ArgTypes
    parentT.GetConstructor(BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Instance,null, reqArgTs,null)  

let nonQueryableTypeGetConstructor (parentTI:Type) (consInfo : ConstructorInfo) : ConstructorInfo = 
    if parentTI.IsGenericType then TypeBuilder.GetConstructor(parentTI,consInfo) else consInfo

//----------------------------------------------------------------------------
// convConstructorSpec (like convMethodSpec) 
//----------------------------------------------------------------------------

let convConstructorSpec cenv emEnv (mspec:ILMethodSpec) =
    let mref   = mspec.MethodRef
    let parentTI = convType cenv emEnv mspec.EnclosingType
    if isEmittedTypeRef emEnv mref.EnclosingTypeRef then
        // NOTE: if "convType becomes convCreatedType", then handle queryable types here too. [bug 4063]
        let consB = envGetConsB emEnv mref
        nonQueryableTypeGetConstructor parentTI consB |> nonNull "convConstructorSpec: (emitted)"
    else
        // Prior type.
        if typeIsNotQueryable parentTI then 
            let parentT  = getTypeConstructor parentTI       
            let ctorG = queryableTypeGetConstructor cenv emEnv parentT mref 
            nonQueryableTypeGetConstructor parentTI ctorG
        else
            queryableTypeGetConstructor cenv emEnv parentTI mref 

//----------------------------------------------------------------------------
// emitLabelMark, defineLabel
//----------------------------------------------------------------------------

let emitLabelMark emEnv (ilG:ILGenerator) (label:ILCodeLabel) =
    let lab = envGetLabel emEnv label
    ilG.MarkLabelAndLog(lab)
    

let defineLabel (ilG:ILGenerator) emEnv (label:ILCodeLabel) =
    let lab = ilG.DefineLabelAndLog()
    envSetLabel emEnv label lab


//----------------------------------------------------------------------------
// emitInstr cenv - I_arith
//----------------------------------------------------------------------------

///Emit comparison instructions
let emitInstrCompare emEnv (ilG:ILGenerator) comp targ  = 
    match comp with
    | BI_beq     -> ilG.EmitAndLog(OpCodes.Beq,envGetLabel emEnv targ)
    | BI_bge     -> ilG.EmitAndLog(OpCodes.Bge    ,envGetLabel emEnv targ)
    | BI_bge_un  -> ilG.EmitAndLog(OpCodes.Bge_Un ,envGetLabel emEnv targ)
    | BI_bgt     -> ilG.EmitAndLog(OpCodes.Bgt    ,envGetLabel emEnv targ)
    | BI_bgt_un  -> ilG.EmitAndLog(OpCodes.Bgt_Un ,envGetLabel emEnv targ)
    | BI_ble     -> ilG.EmitAndLog(OpCodes.Ble    ,envGetLabel emEnv targ)
    | BI_ble_un  -> ilG.EmitAndLog(OpCodes.Ble_Un ,envGetLabel emEnv targ)
    | BI_blt     -> ilG.EmitAndLog(OpCodes.Blt    ,envGetLabel emEnv targ)
    | BI_blt_un  -> ilG.EmitAndLog(OpCodes.Blt_Un ,envGetLabel emEnv targ)
    | BI_bne_un  -> ilG.EmitAndLog(OpCodes.Bne_Un ,envGetLabel emEnv targ)
    | BI_brfalse -> ilG.EmitAndLog(OpCodes.Brfalse,envGetLabel emEnv targ)
    | BI_brtrue  -> ilG.EmitAndLog(OpCodes.Brtrue ,envGetLabel emEnv targ)


/// Emit the volatile. prefix
let emitInstrVolatile (ilG:ILGenerator) = function
    | Volatile    -> ilG.EmitAndLog(OpCodes.Volatile)
    | Nonvolatile -> ()

/// Emit the align. prefix
let emitInstrAlign (ilG:ILGenerator) = function      
    | Aligned     -> ()
    | Unaligned1 -> ilG.Emit(OpCodes.Unaligned,1L) // note: doc says use "long" overload!
    | Unaligned2 -> ilG.Emit(OpCodes.Unaligned,2L)
    | Unaligned4 -> ilG.Emit(OpCodes.Unaligned,3L)

/// Emit the tail. prefix if necessary
let emitInstrTail (ilG:ILGenerator) tail emitTheCall = 
    match tail with
    | Tailcall   -> ilG.EmitAndLog(OpCodes.Tailcall); emitTheCall(); ilG.EmitAndLog(OpCodes.Ret)
    | Normalcall -> emitTheCall()

let emitInstrNewobj cenv emEnv (ilG:ILGenerator) mspec varargs =
    match varargs with
    | None         -> ilG.EmitAndLog(OpCodes.Newobj,convConstructorSpec cenv emEnv mspec)
    | Some _vartyps -> failwith "emit: pending new varargs" // XXX - gap

let emitSilverlightCheck (ilG:ILGenerator) =
    ignore ilG
    ()

let emitInstrCall cenv emEnv (ilG:ILGenerator) opCall tail (mspec:ILMethodSpec) varargs =
    emitInstrTail ilG tail (fun () ->
        if mspec.MethodRef.Name = ".ctor" || mspec.MethodRef.Name = ".cctor" then
            let cinfo = convConstructorSpec cenv emEnv mspec
            match varargs with
            | None         -> ilG.EmitAndLog     (opCall,cinfo)
            | Some _vartyps -> failwith "emitInstrCall: .ctor and varargs"
        else
            let minfo = convMethodSpec cenv emEnv mspec
            match varargs with
            | None         -> ilG.EmitAndLog(opCall,minfo)
            | Some vartyps -> ilG.EmitCall (opCall,minfo,convTypesToArray cenv emEnv vartyps)
    )

let getGenericMethodDefinition q (ty:Type) = 
    let gminfo = 
        match q with 
        | Quotations.Patterns.Call(_,minfo,_) -> minfo.GetGenericMethodDefinition()
        | _ -> failwith "unexpected failure decoding quotation at ilreflect startup"
    gminfo.MakeGenericMethod [| ty |]

let getArrayMethInfo n ty = 
    match n with 
    | 2 -> getGenericMethodDefinition <@@ LanguagePrimitives.IntrinsicFunctions.GetArray2D<int> null 0 0 @@> ty
    | 3 -> getGenericMethodDefinition <@@ LanguagePrimitives.IntrinsicFunctions.GetArray3D<int> null 0 0 0 @@> ty
    | 4 -> getGenericMethodDefinition <@@ LanguagePrimitives.IntrinsicFunctions.GetArray4D<int> null 0 0 0 0 @@> ty
    | _ -> invalidArg "n" "not expecting array dimension > 4"
    
let setArrayMethInfo n ty = 
    match n with 
    | 2 -> getGenericMethodDefinition <@@ LanguagePrimitives.IntrinsicFunctions.SetArray2D<int> null 0 0 0 @@> ty
    | 3 -> getGenericMethodDefinition <@@ LanguagePrimitives.IntrinsicFunctions.SetArray3D<int> null 0 0 0 0 @@> ty
    | 4 -> getGenericMethodDefinition <@@ LanguagePrimitives.IntrinsicFunctions.SetArray4D<int> null 0 0 0 0 0 @@> ty
    | _ -> invalidArg "n"  "not expecting array dimension > 4"


//----------------------------------------------------------------------------
// emitInstr cenv
//----------------------------------------------------------------------------

let rec emitInstr cenv (modB : ModuleBuilder) emEnv (ilG:ILGenerator) instr = 
    match instr with 
    | AI_add                      -> ilG.EmitAndLog(OpCodes.Add) 
    | AI_add_ovf                  -> ilG.EmitAndLog(OpCodes.Add_Ovf) 
    | AI_add_ovf_un               -> ilG.EmitAndLog(OpCodes.Add_Ovf_Un)
    | AI_and                      -> ilG.EmitAndLog(OpCodes.And)
    | AI_div                      -> ilG.EmitAndLog(OpCodes.Div)
    | AI_div_un                   -> ilG.EmitAndLog(OpCodes.Div_Un)
    | AI_ceq                      -> ilG.EmitAndLog(OpCodes.Ceq)
    | AI_cgt                      -> ilG.EmitAndLog(OpCodes.Cgt)
    | AI_cgt_un                   -> ilG.EmitAndLog(OpCodes.Cgt_Un)
    | AI_clt                      -> ilG.EmitAndLog(OpCodes.Clt)
    | AI_clt_un                   -> ilG.EmitAndLog(OpCodes.Clt_Un)
    (* conversion *)
    | AI_conv dt                  -> (match dt with
                                        | DT_I   -> ilG.EmitAndLog(OpCodes.Conv_I)
                                        | DT_I1  -> ilG.EmitAndLog(OpCodes.Conv_I1)
                                        | DT_I2  -> ilG.EmitAndLog(OpCodes.Conv_I2)
                                        | DT_I4  -> ilG.EmitAndLog(OpCodes.Conv_I4)
                                        | DT_I8  -> ilG.EmitAndLog(OpCodes.Conv_I8)
                                        | DT_U   -> ilG.EmitAndLog(OpCodes.Conv_U)      
                                        | DT_U1  -> ilG.EmitAndLog(OpCodes.Conv_U1)      
                                        | DT_U2  -> ilG.EmitAndLog(OpCodes.Conv_U2)      
                                        | DT_U4  -> ilG.EmitAndLog(OpCodes.Conv_U4)      
                                        | DT_U8  -> ilG.EmitAndLog(OpCodes.Conv_U8)
                                        | DT_R   -> ilG.EmitAndLog(OpCodes.Conv_R_Un)
                                        | DT_R4  -> ilG.EmitAndLog(OpCodes.Conv_R4)
                                        | DT_R8  -> ilG.EmitAndLog(OpCodes.Conv_R8)
                                        | DT_REF -> failwith "AI_conv DT_REF?" // XXX - check
                     )
    (* conversion - ovf checks *)
    | AI_conv_ovf dt              -> (match dt with
                                        | DT_I   -> ilG.EmitAndLog(OpCodes.Conv_Ovf_I)
                                        | DT_I1  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_I1)
                                        | DT_I2  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_I2)
                                        | DT_I4  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_I4)
                                        | DT_I8  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_I8)
                                        | DT_U   -> ilG.EmitAndLog(OpCodes.Conv_Ovf_U)      
                                        | DT_U1  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_U1)      
                                        | DT_U2  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_U2)      
                                        | DT_U4  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_U4)
                                        | DT_U8  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_U8)
                                        | DT_R   -> failwith "AI_conv_ovf DT_R?" // XXX - check       
                                        | DT_R4  -> failwith "AI_conv_ovf DT_R4?" // XXX - check       
                                        | DT_R8  -> failwith "AI_conv_ovf DT_R8?" // XXX - check       
                                        | DT_REF -> failwith "AI_conv_ovf DT_REF?" // XXX - check
                     )
    (* conversion - ovf checks and unsigned *)
    | AI_conv_ovf_un dt           -> (match dt with
                                        | DT_I   -> ilG.EmitAndLog(OpCodes.Conv_Ovf_I_Un)
                                        | DT_I1  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_I1_Un)
                                        | DT_I2  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_I2_Un)
                                        | DT_I4  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_I4_Un)
                                        | DT_I8  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_I8_Un)
                                        | DT_U   -> ilG.EmitAndLog(OpCodes.Conv_Ovf_U_Un)            
                                        | DT_U1  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_U1_Un)      
                                        | DT_U2  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_U2_Un)      
                                        | DT_U4  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_U4_Un)      
                                        | DT_U8  -> ilG.EmitAndLog(OpCodes.Conv_Ovf_U8_Un)
                                        | DT_R   -> failwith "AI_conv_ovf_un DT_R?" // XXX - check       
                                        | DT_R4  -> failwith "AI_conv_ovf_un DT_R4?" // XXX - check       
                                        | DT_R8  -> failwith "AI_conv_ovf_un DT_R8?" // XXX - check       
                                        | DT_REF -> failwith "AI_conv_ovf_un DT_REF?" // XXX - check
                     )
    | AI_mul                      -> ilG.EmitAndLog(OpCodes.Mul)
    | AI_mul_ovf                  -> ilG.EmitAndLog(OpCodes.Mul_Ovf)
    | AI_mul_ovf_un               -> ilG.EmitAndLog(OpCodes.Mul_Ovf_Un)
    | AI_rem                      -> ilG.EmitAndLog(OpCodes.Rem)
    | AI_rem_un                   -> ilG.EmitAndLog(OpCodes.Rem_Un)
    | AI_shl                      -> ilG.EmitAndLog(OpCodes.Shl)
    | AI_shr                      -> ilG.EmitAndLog(OpCodes.Shr)
    | AI_shr_un                   -> ilG.EmitAndLog(OpCodes.Shr_Un)
    | AI_sub                      -> ilG.EmitAndLog(OpCodes.Sub)
    | AI_sub_ovf                  -> ilG.EmitAndLog(OpCodes.Sub_Ovf)
    | AI_sub_ovf_un               -> ilG.EmitAndLog(OpCodes.Sub_Ovf_Un)
    | AI_xor                      -> ilG.EmitAndLog(OpCodes.Xor)
    | AI_or                       -> ilG.EmitAndLog(OpCodes.Or)
    | AI_neg                      -> ilG.EmitAndLog(OpCodes.Neg)
    | AI_not                      -> ilG.EmitAndLog(OpCodes.Not)
    | AI_ldnull                   -> ilG.EmitAndLog(OpCodes.Ldnull)
    | AI_dup                      -> ilG.EmitAndLog(OpCodes.Dup)
    | AI_pop                      -> ilG.EmitAndLog(OpCodes.Pop)
    | AI_ckfinite                 -> ilG.EmitAndLog(OpCodes.Ckfinite)
    | AI_nop                      -> ilG.EmitAndLog(OpCodes.Nop)
    | AI_ldc (DT_I4,ILConst.I4 i32)   -> ilG.EmitAndLog(OpCodes.Ldc_I4,i32)
    | AI_ldc (DT_I8,ILConst.I8 i64)   -> ilG.Emit(OpCodes.Ldc_I8,i64)
    | AI_ldc (DT_R4,ILConst.R4 r32)   -> ilG.Emit(OpCodes.Ldc_R4,r32)
    | AI_ldc (DT_R8,ILConst.R8 r64)   -> ilG.Emit(OpCodes.Ldc_R8,r64)
    | AI_ldc (_    ,_         )   -> failwith "emitInstrI_arith (AI_ldc (typ,const)) iltyped"
    | I_ldarg  u16                -> ilG.EmitAndLog(OpCodes.Ldarg ,int16 u16)
    | I_ldarga u16                -> ilG.EmitAndLog(OpCodes.Ldarga,int16 u16)
    | I_ldind (align,vol,dt)      -> emitInstrAlign ilG align;
                                     emitInstrVolatile ilG vol;
                                     (match dt with
                                      | DT_I   -> ilG.EmitAndLog(OpCodes.Ldind_I)
                                      | DT_I1  -> ilG.EmitAndLog(OpCodes.Ldind_I1)
                                      | DT_I2  -> ilG.EmitAndLog(OpCodes.Ldind_I2)
                                      | DT_I4  -> ilG.EmitAndLog(OpCodes.Ldind_I4)
                                      | DT_I8  -> ilG.EmitAndLog(OpCodes.Ldind_I8)
                                      | DT_R   -> failwith "emitInstr cenv: ldind R"
                                      | DT_R4  -> ilG.EmitAndLog(OpCodes.Ldind_R4)
                                      | DT_R8  -> ilG.EmitAndLog(OpCodes.Ldind_R8)
                                      | DT_U   -> failwith "emitInstr cenv: ldind U"
                                      | DT_U1  -> ilG.EmitAndLog(OpCodes.Ldind_U1)
                                      | DT_U2  -> ilG.EmitAndLog(OpCodes.Ldind_U2)
                                      | DT_U4  -> ilG.EmitAndLog(OpCodes.Ldind_U4)
                                      | DT_U8  -> failwith "emitInstr cenv: ldind U8"
                                      | DT_REF -> ilG.EmitAndLog(OpCodes.Ldind_Ref))
    | I_ldloc  u16                -> ilG.EmitAndLog(OpCodes.Ldloc ,int16 u16)
    | I_ldloca u16                -> ilG.EmitAndLog(OpCodes.Ldloca,int16 u16)
    | I_starg  u16                -> ilG.EmitAndLog(OpCodes.Starg ,int16 u16)
    | I_stind (align,vol,dt)      -> emitInstrAlign ilG align;
                                     emitInstrVolatile ilG vol;
                                     (match dt with
                                      | DT_I   -> ilG.EmitAndLog(OpCodes.Stind_I)
                                      | DT_I1  -> ilG.EmitAndLog(OpCodes.Stind_I1)
                                      | DT_I2  -> ilG.EmitAndLog(OpCodes.Stind_I2)
                                      | DT_I4  -> ilG.EmitAndLog(OpCodes.Stind_I4)
                                      | DT_I8  -> ilG.EmitAndLog(OpCodes.Stind_I8)
                                      | DT_R   -> failwith "emitInstr cenv: stind R"
                                      | DT_R4  -> ilG.EmitAndLog(OpCodes.Stind_R4)
                                      | DT_R8  -> ilG.EmitAndLog(OpCodes.Stind_R8)
                                      | DT_U   -> ilG.EmitAndLog(OpCodes.Stind_I)    // NOTE: unsigned -> int conversion
                                      | DT_U1  -> ilG.EmitAndLog(OpCodes.Stind_I1)   // NOTE: follows code ilwrite.fs
                                      | DT_U2  -> ilG.EmitAndLog(OpCodes.Stind_I2)   // NOTE: is it ok?
                                      | DT_U4  -> ilG.EmitAndLog(OpCodes.Stind_I4)   // NOTE: it is generated by bytearray tests
                                      | DT_U8  -> ilG.EmitAndLog(OpCodes.Stind_I8)   // NOTE: unsigned -> int conversion
                                      | DT_REF -> ilG.EmitAndLog(OpCodes.Stind_Ref)) 
    | I_stloc  u16                -> ilG.EmitAndLog(OpCodes.Stloc,int16 u16)
    | I_br  _                 -> () 
    | I_jmp mspec                 -> ilG.EmitAndLog(OpCodes.Jmp,convMethodSpec cenv emEnv mspec)
    | I_brcmp (comp,targ,_)    -> emitInstrCompare emEnv ilG comp targ 
    | I_switch (labels,_)      -> ilG.Emit(OpCodes.Switch,Array.ofList (List.map (envGetLabel emEnv) labels));
    | I_ret                       -> ilG.EmitAndLog(OpCodes.Ret)
    | I_call           (tail,mspec,varargs)   -> emitSilverlightCheck ilG
                                                 emitInstrCall cenv emEnv ilG OpCodes.Call     tail mspec varargs
    | I_callvirt       (tail,mspec,varargs)   -> emitSilverlightCheck ilG
                                                 emitInstrCall cenv emEnv ilG OpCodes.Callvirt tail mspec varargs
    | I_callconstraint (tail,typ,mspec,varargs) -> ilG.Emit(OpCodes.Constrained,convType cenv emEnv typ); 
                                                   emitInstrCall cenv emEnv ilG OpCodes.Callvirt tail mspec varargs                                                     
    | I_calli (tail,callsig,None)             -> emitInstrTail ilG tail (fun () ->
                                                   ilG.EmitCalli(OpCodes.Calli,
                                                                 convCallConv callsig.CallingConv,
                                                                 convType cenv emEnv callsig.ReturnType,
                                                                 convTypesToArray cenv emEnv callsig.ArgTypes,
                                                                 Unchecked.defaultof<System.Type[]>))
    | I_calli (tail,callsig,Some vartyps)     -> emitInstrTail ilG tail (fun () ->
                                                   ilG.EmitCalli(OpCodes.Calli,
                                                                 convCallConv callsig.CallingConv,
                                                                 convType cenv emEnv callsig.ReturnType,
                                                                 convTypesToArray cenv emEnv callsig.ArgTypes,
                                                                 convTypesToArray cenv emEnv vartyps))                                                                
    | I_ldftn mspec                           -> ilG.EmitAndLog(OpCodes.Ldftn,convMethodSpec cenv emEnv mspec)
    | I_newobj (mspec,varargs)                -> emitInstrNewobj cenv emEnv ilG mspec varargs
    | I_throw                        -> ilG.EmitAndLog(OpCodes.Throw)
    | I_endfinally                   -> ilG.EmitAndLog(OpCodes.Endfinally) (* capitalization! *)
    | I_endfilter                    -> () (* ilG.EmitAndLog(OpCodes.Endfilter) *)
    | I_leave label                  -> ilG.EmitAndLog(OpCodes.Leave,envGetLabel emEnv label)
    | I_ldsfld (vol,fspec)           ->                           emitInstrVolatile ilG vol; ilG.EmitAndLog(OpCodes.Ldsfld ,convFieldSpec cenv emEnv fspec)
    | I_ldfld (align,vol,fspec)      -> emitInstrAlign ilG align; emitInstrVolatile ilG vol; ilG.EmitAndLog(OpCodes.Ldfld  ,convFieldSpec cenv emEnv fspec)
    | I_ldsflda fspec                ->                                                      ilG.EmitAndLog(OpCodes.Ldsflda,convFieldSpec cenv emEnv fspec)
    | I_ldflda fspec                 ->                                                      ilG.EmitAndLog(OpCodes.Ldflda ,convFieldSpec cenv emEnv fspec)
    | I_stsfld (vol,fspec)           ->                           emitInstrVolatile ilG vol; ilG.EmitAndLog(OpCodes.Stsfld ,convFieldSpec cenv emEnv fspec)
    | I_stfld (align,vol,fspec)      -> emitInstrAlign ilG align; emitInstrVolatile ilG vol; ilG.EmitAndLog(OpCodes.Stfld  ,convFieldSpec cenv emEnv fspec)
    | I_ldstr     s                  -> ilG.EmitAndLog(OpCodes.Ldstr    ,s)
    | I_isinst    typ                -> ilG.EmitAndLog(OpCodes.Isinst   ,convType cenv emEnv  typ)
    | I_castclass typ                -> ilG.EmitAndLog(OpCodes.Castclass,convType cenv emEnv  typ)
    | I_ldtoken (ILToken.ILType typ)     -> ilG.EmitAndLog(OpCodes.Ldtoken  ,convType cenv emEnv  typ)
    | I_ldtoken (ILToken.ILMethod mspec) -> ilG.EmitAndLog(OpCodes.Ldtoken  ,convMethodSpec cenv emEnv mspec)
    | I_ldtoken (ILToken.ILField fspec)  -> ilG.EmitAndLog(OpCodes.Ldtoken  ,convFieldSpec cenv emEnv fspec)
    | I_ldvirtftn mspec              -> ilG.EmitAndLog(OpCodes.Ldvirtftn,convMethodSpec cenv emEnv mspec)
    (* Value type instructions *)
    | I_cpobj     typ             -> ilG.EmitAndLog(OpCodes.Cpobj    ,convType cenv emEnv  typ)
    | I_initobj   typ             -> ilG.EmitAndLog(OpCodes.Initobj  ,convType cenv emEnv  typ)
    | I_ldobj (align,vol,typ)     -> emitInstrAlign ilG align; emitInstrVolatile ilG vol; ilG.EmitAndLog(OpCodes.Ldobj ,convType cenv emEnv  typ)
    | I_stobj (align,vol,typ)     -> emitInstrAlign ilG align; emitInstrVolatile ilG vol; ilG.EmitAndLog(OpCodes.Stobj ,convType cenv emEnv  typ)
    | I_box       typ             -> ilG.EmitAndLog(OpCodes.Box      ,convType cenv emEnv  typ)
    | I_unbox     typ             -> ilG.EmitAndLog(OpCodes.Unbox    ,convType cenv emEnv  typ)
    | I_unbox_any typ             -> ilG.EmitAndLog(OpCodes.Unbox_Any,convType cenv emEnv  typ)
    | I_sizeof    typ             -> ilG.EmitAndLog(OpCodes.Sizeof   ,convType cenv emEnv  typ)
    // Generalized array instructions. 
    // In AbsIL these instructions include 
    // both the single-dimensional variants (with ILArrayShape == ILArrayShape.SingleDimensional) 
    // and calls to the "special" multi-dimensional "methods" such as 
    //   newobj void string[,]::.ctor(int32, int32) 
    //   call string string[,]::Get(int32, int32) 
    //   call string& string[,]::Address(int32, int32) 
    //   call void string[,]::Set(int32, int32,string) 
    // The IL reader transforms calls of this form to the corresponding 
    // generalized instruction with the corresponding ILArrayShape 
    // argument. This is done to simplify the IL and make it more uniform. 
    // The IL writer then reverses this when emitting the binary. 
    | I_ldelem dt                 -> (match dt with
                                      | DT_I   -> ilG.EmitAndLog(OpCodes.Ldelem_I)
                                      | DT_I1  -> ilG.EmitAndLog(OpCodes.Ldelem_I1)
                                      | DT_I2  -> ilG.EmitAndLog(OpCodes.Ldelem_I2)
                                      | DT_I4  -> ilG.EmitAndLog(OpCodes.Ldelem_I4)
                                      | DT_I8  -> ilG.EmitAndLog(OpCodes.Ldelem_I8)
                                      | DT_R   -> failwith "emitInstr cenv: ldelem R"
                                      | DT_R4  -> ilG.EmitAndLog(OpCodes.Ldelem_R4)
                                      | DT_R8  -> ilG.EmitAndLog(OpCodes.Ldelem_R8)
                                      | DT_U   -> failwith "emitInstr cenv: ldelem U"
                                      | DT_U1  -> ilG.EmitAndLog(OpCodes.Ldelem_U1)
                                      | DT_U2  -> ilG.EmitAndLog(OpCodes.Ldelem_U2)
                                      | DT_U4  -> ilG.EmitAndLog(OpCodes.Ldelem_U4)
                                      | DT_U8  -> failwith "emitInstr cenv: ldelem U8"
                                      | DT_REF -> ilG.EmitAndLog(OpCodes.Ldelem_Ref))
    | I_stelem dt                 -> (match dt with
                                      | DT_I   -> ilG.EmitAndLog(OpCodes.Stelem_I)
                                      | DT_I1  -> ilG.EmitAndLog(OpCodes.Stelem_I1)
                                      | DT_I2  -> ilG.EmitAndLog(OpCodes.Stelem_I2)
                                      | DT_I4  -> ilG.EmitAndLog(OpCodes.Stelem_I4)
                                      | DT_I8  -> ilG.EmitAndLog(OpCodes.Stelem_I8)
                                      | DT_R   -> failwith "emitInstr cenv: stelem R"
                                      | DT_R4  -> ilG.EmitAndLog(OpCodes.Stelem_R4)
                                      | DT_R8  -> ilG.EmitAndLog(OpCodes.Stelem_R8)
                                      | DT_U   -> failwith "emitInstr cenv: stelem U"
                                      | DT_U1  -> failwith "emitInstr cenv: stelem U1"
                                      | DT_U2  -> failwith "emitInstr cenv: stelem U2"
                                      | DT_U4  -> failwith "emitInstr cenv: stelem U4"
                                      | DT_U8  -> failwith "emitInstr cenv: stelem U8"
                                      | DT_REF -> ilG.EmitAndLog(OpCodes.Stelem_Ref))
    | I_ldelema (ro,_isNativePtr,shape,typ)     -> 
        if (ro = ReadonlyAddress) then ilG.EmitAndLog(OpCodes.Readonly);
        if (shape = ILArrayShape.SingleDimensional) 
        then ilG.EmitAndLog(OpCodes.Ldelema,convType cenv emEnv  typ)
        else 
            let aty = convType cenv emEnv  (ILType.Array(shape,typ)) 
            let ety = aty.GetElementType()
            let rty = ety.MakeByRefType() 
            let meth = modB.GetArrayMethodAndLog(aty,"Address",System.Reflection.CallingConventions.HasThis,rty,Array.create shape.Rank (typeof<int>) )
            ilG.EmitAndLog(OpCodes.Call,meth)
    | I_ldelem_any (shape,typ)     -> 
        if (shape = ILArrayShape.SingleDimensional)      then ilG.EmitAndLog(OpCodes.Ldelem,convType cenv emEnv  typ)
        else 
            let aty = convType cenv emEnv  (ILType.Array(shape,typ)) 
            let ety = aty.GetElementType()
            let meth = 
                // See bug 6254: Mono has a bug in reflection-emit dynamic calls to the "Get", "Address" or "Set" methods on arrays
                if runningOnMono then 
                    getArrayMethInfo shape.Rank ety
                else
                    modB.GetArrayMethodAndLog(aty,"Get",System.Reflection.CallingConventions.HasThis,ety,Array.create shape.Rank (typeof<int>) )
            ilG.EmitAndLog(OpCodes.Call,meth)

    | I_stelem_any (shape,typ)     -> 
        if (shape = ILArrayShape.SingleDimensional)      then ilG.EmitAndLog(OpCodes.Stelem,convType cenv emEnv  typ)
        else 
            let aty = convType cenv emEnv  (ILType.Array(shape,typ)) 
            let ety = aty.GetElementType()
            let meth = 
                // See bug 6254: Mono has a bug in reflection-emit dynamic calls to the "Get", "Address" or "Set" methods on arrays
                if runningOnMono then 
                    setArrayMethInfo shape.Rank ety
                else
                    modB.GetArrayMethodAndLog(aty,"Set",System.Reflection.CallingConventions.HasThis,(null:Type),Array.append (Array.create shape.Rank (typeof<int>)) (Array.ofList [ ety ])) 
            ilG.EmitAndLog(OpCodes.Call,meth)

    | I_newarr (shape,typ)         -> 
        if (shape = ILArrayShape.SingleDimensional)
        then ilG.EmitAndLog(OpCodes.Newarr,convType cenv emEnv  typ)
        else 
            let aty = convType cenv emEnv  (ILType.Array(shape,typ)) 
            let meth = modB.GetArrayMethodAndLog(aty,".ctor",System.Reflection.CallingConventions.HasThis,(null:Type),Array.create shape.Rank (typeof<int>))
            ilG.EmitAndLog(OpCodes.Newobj,meth)
    | I_ldlen                      -> ilG.EmitAndLog(OpCodes.Ldlen)
    | I_mkrefany   typ             -> ilG.EmitAndLog(OpCodes.Mkrefany,convType cenv emEnv  typ)
    | I_refanytype                 -> ilG.EmitAndLog(OpCodes.Refanytype)
    | I_refanyval typ              -> ilG.EmitAndLog(OpCodes.Refanyval,convType cenv emEnv  typ)
    | I_rethrow                    -> ilG.EmitAndLog(OpCodes.Rethrow)
    | I_break                      -> ilG.EmitAndLog(OpCodes.Break)
    | I_seqpoint src               -> 
        if cenv.generatePdb && not (src.Document.File.EndsWith("stdin",StringComparison.Ordinal)) then
            let guid x = match x with None -> Guid.Empty | Some g -> Guid(g:byte[]) in
            let symDoc = modB.DefineDocumentAndLog(src.Document.File, guid src.Document.Language, guid src.Document.Vendor, guid src.Document.DocumentType)
            ilG.MarkSequencePointAndLog(symDoc, src.Line, src.Column, src.EndLine, src.EndColumn)
    | I_arglist                    -> ilG.EmitAndLog(OpCodes.Arglist)
    | I_localloc                   -> ilG.EmitAndLog(OpCodes.Localloc)
    | I_cpblk (align,vol)          -> emitInstrAlign ilG align;
                                      emitInstrVolatile ilG vol;
                                      ilG.EmitAndLog(OpCodes.Cpblk)
    | I_initblk (align,vol)        -> emitInstrAlign ilG align;
                                      emitInstrVolatile ilG vol;
                                      ilG.EmitAndLog(OpCodes.Initblk)
    | EI_ldlen_multi (_,m) -> 
        emitInstr cenv modB emEnv ilG (mkLdcInt32 m);
        emitInstr cenv modB emEnv ilG (mkNormalCall(mkILNonGenericMethSpecInTy(cenv.ilg.typ_Array, ILCallingConv.Instance, "GetLength", [cenv.ilg.typ_int32], cenv.ilg.typ_int32)))
    | I_other e when isIlxExtInstr e -> Printf.failwithf "the ILX instruction %s cannot be emitted" (e.ToString())
    |  i -> Printf.failwithf "the IL instruction %s cannot be emitted" (i.ToString())

//----------------------------------------------------------------------------
// emitCode 
//----------------------------------------------------------------------------

let emitBasicBlock cenv  modB emEnv (ilG:ILGenerator) bblock =
    emitLabelMark emEnv ilG bblock.Label;
    Array.iter (emitInstr cenv modB emEnv ilG) bblock.Instructions;

let emitCode cenv modB emEnv (ilG:ILGenerator) code =
    // pre define labels pending determining their actual marks
    let labels = labelsOfCode code
    let emEnv  = List.fold (defineLabel ilG) emEnv labels
              
    let emitSusp susp = 
        match susp with 
        | Some dest -> ilG.EmitAndLog(OpCodes.Br, envGetLabel emEnv dest)
        | _ -> ()
     
    let commitSusp susp lab = 
        match susp with 
        | Some dest when dest <> lab -> emitSusp susp 
        | _ -> ()

    let rec emitter susp code = 
        match code with
        | ILBasicBlock bblock                  -> 
            commitSusp susp bblock.Label;
            emitBasicBlock cenv modB emEnv ilG bblock
            bblock.Fallthrough
        | GroupBlock (_localDebugInfos,codes)-> 
            List.fold emitter susp codes
        | RestrictBlock (_labels,code)       -> 
            code |> emitter susp (* restrictions ignorable: code_labels unique *)
        | TryBlock (code,seh)                -> 
            commitSusp susp (uniqueEntryOfCode code);
            let _endExBlockL = ilG.BeginExceptionBlockAndLog()
            code |> emitter None |> emitSusp
            //ilG.MarkLabel endExBlockL;
            emitHandler seh;
            ilG.EndExceptionBlockAndLog();
            None
    and emitHandler seh =
        match seh with      
        | FaultBlock code         -> 
            ilG.BeginFaultBlockAndLog();   
            emitter None code |> emitSusp 
        | FinallyBlock code       -> 
            ilG.BeginFinallyBlockAndLog(); 
            emitter None code |> emitSusp 
        | FilterCatchBlock fcodes -> 
            let emitFilter (filter,code) =
                match filter with
                | TypeFilter typ  -> 
                    ilG.BeginCatchBlockAndLog (convType cenv emEnv  typ); 
                    emitter None code |> emitSusp 
                    
                | CodeFilter test -> 
                    ilG.BeginExceptFilterBlockAndLog(); 
                    emitter None test |> emitSusp 
                    ilG.BeginCatchBlockAndLog null; 
                    emitter None code |> emitSusp 
            fcodes |> List.iter emitFilter 
    let initialSusp = Some (uniqueEntryOfCode code)
    emitter initialSusp code |> emitSusp

//----------------------------------------------------------------------------
// emitILMethodBody 
//----------------------------------------------------------------------------

let emitLocal cenv emEnv (ilG : ILGenerator) (local: ILLocal) =
    let ty = convType cenv emEnv  local.Type
    ilG.DeclareLocalAndLog(ty,local.IsPinned)

let emitILMethodBody cenv modB emEnv (ilG:ILGenerator) ilmbody =
    // XXX - REVIEW:
    //      NoInlining: bool;
    //      SourceMarker: source option }
    // emit locals and record emEnv
    let localBs = Array.map (emitLocal cenv emEnv ilG) (ILList.toArray ilmbody.Locals)
    let emEnv = envSetLocals emEnv localBs
    emitCode cenv modB emEnv ilG ilmbody.Code 


//----------------------------------------------------------------------------
// emitMethodBody 
//----------------------------------------------------------------------------

let emitMethodBody cenv modB emEnv ilG _name (mbody: ILLazyMethodBody) =
    match mbody.Contents with
    | MethodBody.IL ilmbody       -> emitILMethodBody cenv modB emEnv (ilG()) ilmbody
    | MethodBody.PInvoke  _pinvoke -> () (* printf "EMIT: pinvoke method %s\n" name *) (* XXX - check *)
    | MethodBody.Abstract         -> () (* printf "EMIT: abstract method %s\n" name *) (* XXX - check *)
    | MethodBody.Native           -> failwith "emitMethodBody cenv: native"               (* XXX - gap *)


//----------------------------------------------------------------------------
// emitCustomAttrs
//----------------------------------------------------------------------------

let convCustomAttr cenv emEnv cattr =
    let methInfo = 
       match convConstructorSpec cenv emEnv cattr.Method with 
       | null -> failwithf "convCustomAttr: %+A" cattr.Method
       | res -> res
    let data = cattr.Data 
    (methInfo,data)

let emitCustomAttr cenv emEnv add cattr  = add (convCustomAttr cenv emEnv cattr)
let emitCustomAttrs cenv emEnv add (cattrs : ILAttributes) = List.iter (emitCustomAttr cenv emEnv add) cattrs.AsList

//----------------------------------------------------------------------------
// buildGenParams
//----------------------------------------------------------------------------

let buildGenParamsPass1 _emEnv defineGenericParameters (gps : ILGenericParameterDefs) = 
    match gps with 
    | [] -> () 
    | gps ->
        let gpsNames = gps |> List.map (fun gp -> gp.Name) 
        defineGenericParameters (Array.ofList gpsNames)  |> ignore


let buildGenParamsPass1b cenv emEnv (genArgs : Type array) (gps : ILGenericParameterDefs) = 
    let genpBs =  genArgs |>  Array.map (fun x -> (x :?> GenericTypeParameterBuilder)) 
    gps |> List.iteri (fun i (gp:ILGenericParameterDef) ->
        let gpB = genpBs.[i]
        // the Constraints are either the parent (base) type or interfaces.
        let constraintTs = convTypes cenv emEnv gp.Constraints
        let interfaceTs,baseTs = List.partition (fun (typ:System.Type) -> typ.IsInterface) (ILList.toList constraintTs)
        // set base type constraint
        (match baseTs with
            [ ]      -> () // Q: should a baseType be set? It is in some samples. Should this be a failure case?
          | [ baseT ] -> gpB.SetBaseTypeConstraint(baseT)
          | _       -> failwith "buildGenParam: multiple base types"
        );
        // set interface contraints (interfaces that instances of gp must meet)
        gpB.SetInterfaceConstraints(Array.ofList interfaceTs);
        gp.CustomAttrs |> emitCustomAttrs cenv emEnv (wrapCustomAttr gpB.SetCustomAttribute)

        let flags = GenericParameterAttributes.None 
        let flags =
           match gp.Variance with
           | NonVariant    -> flags
           | CoVariant     -> flags ||| GenericParameterAttributes.Covariant
           | ContraVariant -> flags ||| GenericParameterAttributes.Contravariant
       
        let flags = if gp.HasReferenceTypeConstraint        then flags ||| GenericParameterAttributes.ReferenceTypeConstraint        else flags 
        let flags = if gp.HasNotNullableValueTypeConstraint then flags ||| GenericParameterAttributes.NotNullableValueTypeConstraint else flags
        let flags = if gp.HasDefaultConstructorConstraint   then flags ||| GenericParameterAttributes.DefaultConstructorConstraint   else flags
        
        gpB.SetGenericParameterAttributes(flags)
    )
//----------------------------------------------------------------------------
// emitParameter
//----------------------------------------------------------------------------

let emitParameter cenv emEnv (defineParameter : int * ParameterAttributes * string -> ParameterBuilder) i param =
    //  -Type: typ;
    //  -Default: ILFieldInit option;  
    //  -Marshal: NativeType option; (* Marshalling map for parameters. COM Interop only. *)
    let attrs = flagsIf param.IsIn       ParameterAttributes.In ||| 
                flagsIf param.IsOut      ParameterAttributes.Out |||
                flagsIf param.IsOptional ParameterAttributes.Optional
    let name = match param.Name with
               | Some name -> name
               | None      -> "X"^string(i+1)
   
    let parB = defineParameter(i,attrs,name)
    emitCustomAttrs cenv emEnv (wrapCustomAttr parB.SetCustomAttribute) param.CustomAttrs

//----------------------------------------------------------------------------
// convMethodAttributes
//----------------------------------------------------------------------------

let convMethodAttributes (mdef: ILMethodDef) =    
    let attrKind = 
        match mdef.mdKind with 
        | MethodKind.Static        -> MethodAttributes.Static
        | MethodKind.Cctor         -> MethodAttributes.Static
        | MethodKind.Ctor          -> enum 0                 
        | MethodKind.NonVirtual    -> enum 0
        | MethodKind.Virtual vinfo -> MethodAttributes.Virtual |||
                                      flagsIf vinfo.IsNewSlot   MethodAttributes.NewSlot |||
                                      flagsIf vinfo.IsFinal     MethodAttributes.Final |||
                                      flagsIf vinfo.IsCheckAccessOnOverride    MethodAttributes.CheckAccessOnOverride |||
                                      flagsIf vinfo.IsAbstract  MethodAttributes.Abstract
   
    let attrAccess = 
        match mdef.Access with
        | ILMemberAccess.Assembly -> MethodAttributes.Assembly
        | ILMemberAccess.CompilerControlled -> failwith "Method access compiler controled."
        | ILMemberAccess.FamilyAndAssembly        -> MethodAttributes.FamANDAssem
        | ILMemberAccess.FamilyOrAssembly         -> MethodAttributes.FamORAssem
        | ILMemberAccess.Family             -> MethodAttributes.Family
        | ILMemberAccess.Private            -> MethodAttributes.Private
        | ILMemberAccess.Public             -> MethodAttributes.Public
   
    let attrOthers = flagsIf mdef.HasSecurity MethodAttributes.HasSecurity |||
                     flagsIf mdef.IsSpecialName MethodAttributes.SpecialName |||
                     flagsIf mdef.IsHideBySig   MethodAttributes.HideBySig |||
                     flagsIf mdef.IsReqSecObj   MethodAttributes.RequireSecObject 
   
    attrKind ||| attrAccess ||| attrOthers

let convMethodImplFlags mdef =    
    (match  mdef.mdCodeKind with 
     | MethodCodeKind.Native -> MethodImplAttributes.Native
     | MethodCodeKind.Runtime -> MethodImplAttributes.Runtime
     | MethodCodeKind.IL  -> MethodImplAttributes.IL) 
    ||| flagsIf mdef.IsInternalCall MethodImplAttributes.InternalCall
    ||| (if mdef.IsManaged then MethodImplAttributes.Managed else MethodImplAttributes.Unmanaged)
    ||| flagsIf mdef.IsForwardRef MethodImplAttributes.ForwardRef
    ||| flagsIf mdef.IsPreserveSig MethodImplAttributes.PreserveSig
    ||| flagsIf mdef.IsSynchronized MethodImplAttributes.Synchronized
    ||| flagsIf (match mdef.mdBody.Contents with MethodBody.IL b -> b.NoInlining | _ -> false) MethodImplAttributes.NoInlining

//----------------------------------------------------------------------------
// buildMethodPass2
//----------------------------------------------------------------------------
  
let rec buildMethodPass2 cenv tref (typB:TypeBuilder) emEnv (mdef : ILMethodDef) =
   // remaining REVIEW:
   // SecurityDecls: Permissions;
   // IsUnmanagedExport: bool; (* -- The method is exported to unmanaged code using COM interop. *)
   // IsMustRun: bool; (* Whidbey feature: SafeHandle finalizer must be run *)
    let attrs = convMethodAttributes mdef
    let implflags = convMethodImplFlags mdef
    let cconv = convCallConv mdef.CallingConv
    let mref = mkRefToILMethod (tref,mdef)   
    let emEnv = if mdef.IsEntryPoint && mdef.ParameterTypes.Length = 0 then 
                    (* Bug 2209:
                        Here, we collect the entry points generated by ilxgen corresponding to the top-level effects.
                        Users can (now) annotate their own functions with EntryPoint attributes.
                        However, these user entry points functions must take string[] argument.
                        By only adding entry points with no arguments, we only collect the top-level effects.
                     *)
                    envAddEntryPt emEnv (typB,mdef.Name)
                else
                    emEnv
    match mdef.mdBody.Contents with
    | MethodBody.PInvoke  p -> 
        let argtys = convTypesToArray cenv emEnv mdef.ParameterTypes
        let rty = convType cenv emEnv  mdef.Return.Type
        
        let pcc = 
            match p.CallingConv with 
            | PInvokeCallingConvention.Cdecl -> CallingConvention.Cdecl
            | PInvokeCallingConvention.Stdcall -> CallingConvention.StdCall
            | PInvokeCallingConvention.Thiscall -> CallingConvention.ThisCall
            | PInvokeCallingConvention.Fastcall -> CallingConvention.FastCall
            | PInvokeCallingConvention.None 
            | PInvokeCallingConvention.WinApi -> CallingConvention.Winapi 
        
        let pcs = 
            match p.CharEncoding with 
            | PInvokeCharEncoding.None -> CharSet.None
            | PInvokeCharEncoding.Ansi -> CharSet.Ansi
            | PInvokeCharEncoding.Unicode -> CharSet.Unicode
            | PInvokeCharEncoding.Auto -> CharSet.Auto 
      
(* p.ThrowOnUnmappableChar *)
(* p.CharBestFit *)
(* p.NoMangle *)

        let methB = typB.DefinePInvokeMethod(mdef.Name, 
                                             p.Where.Name, 
                                             p.Name, 
                                             attrs, 
                                             cconv, 
                                             rty, 
                                             null, null, 
                                             argtys, 
                                             null, null, 
                                             pcc, 
                                             pcs) 
        methB.SetImplementationFlagsAndLog(implflags);
        envBindMethodRef emEnv mref methB

    | _ -> 
      match mdef.Name with
      | ".cctor" 
      | ".ctor" ->
          let consB = typB.DefineConstructorAndLog(attrs,cconv,convTypesToArray cenv emEnv mdef.ParameterTypes)
          consB.SetImplementationFlagsAndLog(implflags);
          envBindConsRef emEnv mref consB
      | _name    ->
          // Note the return/argument types may involve the generic parameters
          let methB = typB.DefineMethodAndLog(mdef.Name,attrs,cconv) 
        
          // Method generic type parameters         
          buildGenParamsPass1 emEnv methB.DefineGenericParametersAndLog mdef.GenericParams;
          let genArgs = getGenericArgumentsOfMethod methB 
          let emEnv = envPushTyvars emEnv (Array.append (getGenericArgumentsOfType typB) genArgs)
          buildGenParamsPass1b cenv emEnv genArgs mdef.GenericParams;
          // set parameter and return types (may depend on generic args)
          methB.SetParametersAndLog(convTypesToArray cenv emEnv mdef.ParameterTypes);
          methB.SetReturnTypeAndLog(convType cenv emEnv  mdef.Return.Type);
          let emEnv = envPopTyvars emEnv
          methB.SetImplementationFlagsAndLog(implflags);
          envBindMethodRef emEnv mref methB


//----------------------------------------------------------------------------
// buildMethodPass3 cenv
//----------------------------------------------------------------------------
    
let rec buildMethodPass3 cenv tref modB (typB:TypeBuilder) emEnv (mdef : ILMethodDef) =
    let mref  = mkRefToILMethod (tref,mdef)
    let isPInvoke = 
        match mdef.mdBody.Contents with
        | MethodBody.PInvoke  _p -> true
        | _ -> false
    match mdef.Name with
    | ".cctor" | ".ctor" ->
          let consB = envGetConsB emEnv mref
          // Constructors can not have generic parameters
          assert isNil mdef.GenericParams
          // Value parameters       
          let defineParameter (i,attr,name) = consB.DefineParameterAndLog(i+1,attr,name)
          mdef.Parameters |> ILList.iteri (emitParameter cenv emEnv defineParameter);
          // Body
          emitMethodBody cenv modB emEnv consB.GetILGenerator mdef.Name mdef.mdBody;
          emitCustomAttrs cenv emEnv (wrapCustomAttr consB.SetCustomAttribute) mdef.CustomAttrs;
          ()
     | _name ->
       
          let methB = envGetMethB emEnv mref
          let emEnv = envPushTyvars emEnv (Array.append
                                             (getGenericArgumentsOfType typB)
                                             (getGenericArgumentsOfMethod methB))

          match mdef.Return.CustomAttrs.AsList with
          | [] -> ()
          | _ ->
              let retB = methB.DefineParameterAndLog(0,System.Reflection.ParameterAttributes.Retval,null) 
              emitCustomAttrs cenv emEnv (wrapCustomAttr retB.SetCustomAttribute) mdef.Return.CustomAttrs

          // Value parameters
          let defineParameter (i,attr,name) = methB.DefineParameterAndLog(i+1,attr,name) 
          mdef.Parameters |> ILList.iteri (fun a b -> emitParameter cenv emEnv defineParameter a b);
          // Body
          if not isPInvoke then 
              emitMethodBody cenv modB emEnv methB.GetILGeneratorAndLog mdef.Name mdef.mdBody;
          let emEnv = envPopTyvars emEnv // case fold later...
          emitCustomAttrs cenv emEnv methB.SetCustomAttributeAndLog mdef.CustomAttrs
      
//----------------------------------------------------------------------------
// buildFieldPass2
//----------------------------------------------------------------------------
  
let buildFieldPass2 cenv tref (typB:TypeBuilder) emEnv (fdef : ILFieldDef) =
    
    (*{ -Data:    bytes option;       
        -Marshal: NativeType option;  *)
    let attrsAccess = match fdef.Access with
                      | ILMemberAccess.Assembly           -> FieldAttributes.Assembly
                      | ILMemberAccess.CompilerControlled -> failwith "Field access compiler controled."
                      | ILMemberAccess.FamilyAndAssembly        -> FieldAttributes.FamANDAssem
                      | ILMemberAccess.FamilyOrAssembly         -> FieldAttributes.FamORAssem
                      | ILMemberAccess.Family             -> FieldAttributes.Family
                      | ILMemberAccess.Private            -> FieldAttributes.Private
                      | ILMemberAccess.Public             -> FieldAttributes.Public
    let attrsOther = flagsIf fdef.IsStatic        FieldAttributes.Static |||
                     flagsIf fdef.IsSpecialName   FieldAttributes.SpecialName |||
                     flagsIf fdef.IsLiteral       FieldAttributes.Literal |||
                     flagsIf fdef.IsInitOnly      FieldAttributes.InitOnly |||
                     flagsIf fdef.NotSerialized FieldAttributes.NotSerialized 
    let attrs = attrsAccess ||| attrsOther
    let fieldT = convType cenv emEnv  fdef.Type
    let fieldB = 
        match fdef.Data with 
        | Some d -> typB.DefineInitializedData(fdef.Name, d, attrs)
        | None -> 
        typB.DefineFieldAndLog(fdef.Name,fieldT,attrs)
     
    // set default value
    let emEnv = 
        match fdef.LiteralValue with
        | None -> emEnv
        | Some initial -> 
            if not fieldT.IsEnum 
#if FX_ATLEAST_45
                || not fieldT.Assembly.IsDynamic // it is ok to init fields with type = enum  that are defined in other assemblies 
#endif
            then 
                fieldB.SetConstant(convFieldInit initial)
                emEnv
            else
                // if field type (enum) is defined in FSI dynamic assembly it is created as nested type 
                // => its underlying type cannot be explicitly specified and will be inferred at the very moment of first field definition
                // => here we cannot detect if underlying type is already set so as a conservative solution we delay initialization of fields
                // to the end of pass2 (types and members are already created but method bodies are yet not emitted)
                { emEnv with delayedFieldInits = (fun() -> fieldB.SetConstant(convFieldInit initial))::emEnv.delayedFieldInits }
    fdef.Offset |> Option.iter (fun offset ->  fieldB.SetOffset(offset));
    // custom attributes: done on pass 3 as they may reference attribute constructors generated on
    // pass 2.
    let fref = mkILFieldRef (tref,fdef.Name,fdef.Type)    
    envBindFieldRef emEnv fref fieldB

let buildFieldPass3 cenv tref (_typB:TypeBuilder) emEnv (fdef : ILFieldDef) =
    let fref = mkILFieldRef (tref,fdef.Name,fdef.Type)    
    let fieldB = envGetFieldB emEnv fref
    emitCustomAttrs cenv emEnv (wrapCustomAttr fieldB.SetCustomAttribute) fdef.CustomAttrs

//----------------------------------------------------------------------------
// buildPropertyPass2,3
//----------------------------------------------------------------------------
  
let buildPropertyPass2 cenv tref (typB:TypeBuilder) emEnv (prop : ILPropertyDef) =
    let attrs = flagsIf prop.IsRTSpecialName PropertyAttributes.RTSpecialName |||
                flagsIf prop.IsSpecialName   PropertyAttributes.SpecialName

    let propB = typB.DefinePropertyAndLog(prop.Name,attrs,convType cenv emEnv  prop.Type,convTypesToArray cenv emEnv prop.Args)
   
    prop.SetMethod |> Option.iter (fun mref -> propB.SetSetMethod(envGetMethB emEnv mref));
    prop.GetMethod |> Option.iter (fun mref -> propB.SetGetMethod(envGetMethB emEnv mref));
    // set default value
    prop.Init |> Option.iter (fun initial -> propB.SetConstant(convFieldInit initial));
    // custom attributes
    let pref = ILPropertyRef.Create (tref,prop.Name)    
    envBindPropRef emEnv pref propB

let buildPropertyPass3 cenv tref (_typB:TypeBuilder) emEnv (prop : ILPropertyDef) = 
  let pref = ILPropertyRef.Create (tref,prop.Name)    
  let propB = envGetPropB emEnv pref
  emitCustomAttrs cenv emEnv (wrapCustomAttr propB.SetCustomAttribute) prop.CustomAttrs

//----------------------------------------------------------------------------
// buildEventPass3
//----------------------------------------------------------------------------
  

let buildEventPass3 cenv (typB:TypeBuilder) emEnv (eventDef : ILEventDef) = 
    let attrs = flagsIf eventDef.IsSpecialName EventAttributes.SpecialName |||
                flagsIf eventDef.IsRTSpecialName EventAttributes.RTSpecialName 
    assert eventDef.Type.IsSome
    let eventB = typB.DefineEventAndLog(eventDef.Name,attrs,convType cenv emEnv  eventDef.Type.Value) 

    eventDef.AddMethod |> (fun mref -> eventB.SetAddOnMethod(envGetMethB emEnv mref));
    eventDef.RemoveMethod |> (fun mref -> eventB.SetRemoveOnMethod(envGetMethB emEnv mref));
    eventDef.FireMethod |> Option.iter (fun mref -> eventB.SetRaiseMethod(envGetMethB emEnv mref));
    eventDef.OtherMethods |> List.iter (fun mref -> eventB.AddOtherMethod(envGetMethB emEnv mref));
    emitCustomAttrs cenv emEnv (wrapCustomAttr eventB.SetCustomAttribute) eventDef.CustomAttrs

//----------------------------------------------------------------------------
// buildMethodImplsPass3
//----------------------------------------------------------------------------
  
let buildMethodImplsPass3 cenv _tref (typB:TypeBuilder) emEnv (mimpl : IL.ILMethodImplDef) =
    let bodyMethInfo = convMethodRef cenv emEnv (typB :> Type) mimpl.OverrideBy.MethodRef // doc: must be MethodBuilder
    let (OverridesSpec (mref,dtyp)) = mimpl.Overrides
    let declMethTI = convType cenv emEnv  dtyp 
    let declMethInfo = convMethodRef cenv emEnv declMethTI mref
    typB.DefineMethodOverride(bodyMethInfo,declMethInfo);
    emEnv

//----------------------------------------------------------------------------
// typeAttributesOf*
//----------------------------------------------------------------------------

let typeAttrbutesOfTypeDefKind x = 
    match x with 
    // required for a TypeBuilder
    | ILTypeDefKind.Class           -> TypeAttributes.Class
    | ILTypeDefKind.ValueType       -> TypeAttributes.Class
    | ILTypeDefKind.Interface       -> TypeAttributes.Interface
    | ILTypeDefKind.Enum            -> TypeAttributes.Class
    | ILTypeDefKind.Delegate        -> TypeAttributes.Class
    | ILTypeDefKind.Other _xtdk     -> failwith "typeAttributes of other external"

let typeAttrbutesOfTypeAccess x =
    match x with 
    | ILTypeDefAccess.Public       -> TypeAttributes.Public
    | ILTypeDefAccess.Private      -> TypeAttributes.NotPublic
    | ILTypeDefAccess.Nested macc  -> 
        match macc with
        | ILMemberAccess.Assembly           -> TypeAttributes.NestedAssembly
        | ILMemberAccess.CompilerControlled -> failwith "Nested compiler controled."
        | ILMemberAccess.FamilyAndAssembly        -> TypeAttributes.NestedFamANDAssem
        | ILMemberAccess.FamilyOrAssembly         -> TypeAttributes.NestedFamORAssem
        | ILMemberAccess.Family             -> TypeAttributes.NestedFamily
        | ILMemberAccess.Private            -> TypeAttributes.NestedPrivate
        | ILMemberAccess.Public             -> TypeAttributes.NestedPublic
                        
let typeAttributesOfTypeEncoding x = 
    match x with 
    | ILDefaultPInvokeEncoding.Ansi     -> TypeAttributes.AnsiClass    
    | ILDefaultPInvokeEncoding.Auto -> TypeAttributes.AutoClass
    | ILDefaultPInvokeEncoding.Unicode  -> TypeAttributes.UnicodeClass


let typeAttributesOfTypeLayout cenv emEnv x = 
    let attr p = 
      if p.Size =None && p.Pack = None then None
      else 
        Some(convCustomAttr cenv emEnv  
               (IL.mkILCustomAttribute cenv.ilg
                  (mkILTyRef (cenv.ilg.traits.ScopeRef,"System.Runtime.InteropServices.StructLayoutAttribute"), 
                   [mkILNonGenericValueTy (mkILTyRef (cenv.ilg.traits.ScopeRef,"System.Runtime.InteropServices.LayoutKind")) ],
                   [ ILAttribElem.Int32 0x02 ],
                   (p.Pack |> Option.toList |> List.map (fun x -> ("Pack", cenv.ilg.typ_int32, false, ILAttribElem.Int32 (int32 x))))  @
                   (p.Size |> Option.toList |> List.map (fun x -> ("Size", cenv.ilg.typ_int32, false, ILAttribElem.Int32 x)))))) in
    match x with 
    | ILTypeDefLayout.Auto         -> TypeAttributes.AutoLayout,None
    | ILTypeDefLayout.Explicit p   -> TypeAttributes.ExplicitLayout,(attr p)
    | ILTypeDefLayout.Sequential p -> TypeAttributes.SequentialLayout, (attr p)


//----------------------------------------------------------------------------
// buildTypeDefPass1 cenv
//----------------------------------------------------------------------------
    
let rec buildTypeDefPass1 cenv emEnv (modB:ModuleBuilder) rootTypeBuilder nesting (tdef : ILTypeDef) =
    // -IsComInterop: bool; (* Class or interface generated for COM interop *) 
    // -SecurityDecls: Permissions;
    // -InitSemantics: ILTypeInit;
    // TypeAttributes
    let attrsKind   = typeAttrbutesOfTypeDefKind tdef.tdKind 
    let attrsAccess = typeAttrbutesOfTypeAccess  tdef.Access
    let attrsLayout,cattrsLayout = typeAttributesOfTypeLayout cenv emEnv tdef.Layout
    let attrsEnc    = typeAttributesOfTypeEncoding tdef.Encoding
    let attrsOther  = flagsIf tdef.IsAbstract     TypeAttributes.Abstract |||
                      flagsIf tdef.IsSealed       TypeAttributes.Sealed |||
                      flagsIf tdef.IsSerializable TypeAttributes.Serializable |||
                      flagsIf tdef.IsSpecialName  TypeAttributes.SpecialName |||
                      flagsIf tdef.HasSecurity  TypeAttributes.HasSecurity
     
    let attrsType = attrsKind ||| attrsAccess ||| attrsLayout ||| attrsEnc ||| attrsOther

    // TypeBuilder from TypeAttributes.
    let typB : TypeBuilder = rootTypeBuilder  (tdef.Name,attrsType)
    let typB = typB |> nonNull "buildTypeDefPass1 cenv: typB is null!"
    cattrsLayout |> Option.iter typB.SetCustomAttributeAndLog;

    buildGenParamsPass1 emEnv typB.DefineGenericParametersAndLog tdef.GenericParams; 
    // bind tref -> (typT,typB)
    let tref = mkRefForNestedILTypeDef ILScopeRef.Local (nesting,tdef)    
    let typT =
        // Q: would it be ok to use typB :> Type ?
        // Maybe not, recall TypeBuilder maybe subtype of Type, but it is not THE Type.
        let nameInModule = tref.QualifiedName
        modB.GetTypeAndLog(nameInModule,false,false)
   
    let emEnv = envBindTypeRef emEnv tref (typT,typB,tdef)
    // recurse on nested types
    let nesting = nesting @ [tdef]     
    let buildNestedType emEnv tdef = buildTypeTypeDef cenv emEnv modB typB nesting tdef
    let emEnv = List.fold buildNestedType emEnv tdef.NestedTypes.AsList
    emEnv

and buildTypeTypeDef cenv emEnv modB (typB : TypeBuilder) nesting tdef =
    buildTypeDefPass1 cenv emEnv modB typB.DefineNestedTypeAndLog nesting tdef

//----------------------------------------------------------------------------
// buildTypeDefPass1b
//----------------------------------------------------------------------------
    
let rec buildTypeDefPass1b cenv nesting emEnv (tdef : ILTypeDef) = 
    let tref = mkRefForNestedILTypeDef ILScopeRef.Local (nesting,tdef)
    let typB  = envGetTypB emEnv tref
    let genArgs = getGenericArgumentsOfType typB
    let emEnv = envPushTyvars emEnv genArgs
    // Parent may reference types being defined, so has to come after it's Pass1 creation 
    tdef.Extends |> Option.iter (fun typ -> typB.SetParentAndLog(convType cenv emEnv  typ));
    // build constraints on ILGenericParameterDefs.  Constraints may reference types being defined, 
    // so have to come after all types are created
    buildGenParamsPass1b cenv emEnv genArgs tdef.GenericParams; 
    let emEnv = envPopTyvars emEnv     
    let nesting = nesting @ [tdef]     
    List.iter (buildTypeDefPass1b cenv nesting emEnv) tdef.NestedTypes.AsList

//----------------------------------------------------------------------------
// buildTypeDefPass2
//----------------------------------------------------------------------------

let rec buildTypeDefPass2 cenv nesting emEnv (tdef : ILTypeDef) = 
    let tref = mkRefForNestedILTypeDef ILScopeRef.Local (nesting,tdef)
    let typB  = envGetTypB emEnv tref
    let emEnv = envPushTyvars emEnv (getGenericArgumentsOfType typB)
    // add interface impls
    tdef.Implements |> convTypes cenv emEnv |> ILList.iter (fun implT -> typB.AddInterfaceImplementationAndLog(implT));
    // add methods, properties
    let emEnv = List.fold (buildMethodPass2      cenv tref typB) emEnv tdef.Methods.AsList 
    let emEnv = List.fold (buildFieldPass2       cenv tref typB) emEnv tdef.Fields.AsList  
    let emEnv = List.fold (buildPropertyPass2    cenv tref typB) emEnv tdef.Properties.AsList 
    let emEnv = envPopTyvars emEnv
    // nested types
    let nesting = nesting @ [tdef]
    let emEnv = List.fold (buildTypeDefPass2 cenv nesting) emEnv tdef.NestedTypes.AsList
    emEnv

//----------------------------------------------------------------------------
// buildTypeDefPass3 cenv
//----------------------------------------------------------------------------
    
let rec buildTypeDefPass3 cenv nesting modB emEnv (tdef : ILTypeDef) =
    let tref = mkRefForNestedILTypeDef ILScopeRef.Local (nesting,tdef)
    let typB = envGetTypB emEnv tref
    let emEnv = envPushTyvars emEnv (getGenericArgumentsOfType typB)
    // add method bodies, properties, events
    tdef.Methods |> Seq.iter (buildMethodPass3 cenv tref modB typB emEnv);
    tdef.Properties.AsList |> List.iter (buildPropertyPass3 cenv tref typB emEnv);
    tdef.Events.AsList |> List.iter (buildEventPass3 cenv typB emEnv);
    tdef.Fields.AsList |> List.iter (buildFieldPass3 cenv tref typB emEnv);
    let emEnv = List.fold (buildMethodImplsPass3 cenv tref typB) emEnv tdef.MethodImpls.AsList
    tdef.CustomAttrs |> emitCustomAttrs cenv emEnv typB.SetCustomAttributeAndLog ;
    // custom attributes
    let emEnv = envPopTyvars emEnv
    // nested types
    let nesting = nesting @ [tdef]
    let emEnv = List.fold (buildTypeDefPass3 cenv nesting modB) emEnv tdef.NestedTypes.AsList
    emEnv

//----------------------------------------------------------------------------
// buildTypeDefPass4 - Create the Types
// MSDN says: If this type is a nested type, the CreateType method must 
// be called on the enclosing type before it is called on the nested type.
// If the current type derives from an incomplete type or implements 
// incomplete interfaces, call the CreateType method on the parent 
// type and the interface types before calling it on the current type.
// If the enclosing type contains a field that is a value type 
// defined as a nested type (for example, a field that is an 
// enumeration defined as a nested type), calling the CreateType method 
// on the enclosing type will generate a AppDomain.TypeResolve event. 
// This is because the loader cannot determine the size of the enclosing 
// type until the nested type has been completed. The caller should define 
// a handler for the TypeResolve event to complete the definition of the 
// nested type by calling CreateType on the TypeBuilder object that represents 
// the nested type. The code example for this topic shows how to define such 
// an event handler.
//----------------------------------------------------------------------------

let getEnclosingTypeRefs (tref:ILTypeRef) = 
   match tref.Enclosing with 
   | [] -> []
   | h :: t -> List.scan (fun tr nm -> mkILTyRefInTyRef (tr,nm)) (mkILTyRef(tref.Scope, h)) t

let rec getTypeRefsInType valueTypesOnly typ acc = 
    match typ with
    | ILType.Void | ILType.TypeVar _                              -> acc
    | ILType.Ptr eltType | ILType.Byref eltType -> getTypeRefsInType valueTypesOnly eltType acc
    | ILType.Array (_,eltType) -> if valueTypesOnly then acc else getTypeRefsInType valueTypesOnly eltType acc
    | ILType.Value tspec -> tspec.TypeRef :: ILList.foldBack (getTypeRefsInType valueTypesOnly) tspec.GenericArgs acc
    | ILType.Boxed tspec -> if valueTypesOnly then acc else tspec.TypeRef :: ILList.foldBack (getTypeRefsInType valueTypesOnly) tspec.GenericArgs acc
    | ILType.FunctionPointer _callsig -> failwith "getTypeRefsInType: fptr"
    | ILType.Modified _   -> failwith "getTypeRefsInType: modified"

let verbose2 = false

let createTypeRef (visited : Dictionary<_,_>, created : Dictionary<_,_>) emEnv tref = 
    let rec traverseTypeDef priority (tref:ILTypeRef) (tdef:ILTypeDef) =
        if priority >= 2 then 
            if verbose2 then dprintf "buildTypeDefPass4: Creating Enclosing Types of %s\n" tdef.Name; 
            tref |> getEnclosingTypeRefs |> List.iter (traverseTypeRef priority);
    
        // WORKAROUND (ProductStudio FSharp 1.0 bug 615): the constraints on generic method parameters 
        // are resolved overly eagerly by reflection emit's CreateType. 
        if priority >= 1 then 
            if verbose2 then dprintf "buildTypeDefPass4: Doing type typar constraints of %s\n" tdef.Name; 
            tdef.GenericParams |> List.iter (fun gp -> gp.Constraints |> ILList.iter (traverseType false 2));
            if verbose2 then dprintf "buildTypeDefPass4: Doing method constraints of %s\n" tdef.Name; 
            tdef.Methods.AsList |> Seq.iter   (fun md -> md.GenericParams |> List.iter (fun gp -> gp.Constraints |> ILList.iter (traverseType false 2)));
            
        // We absolutely need the parent type...
        if priority >= 1 then 
            if verbose2 then dprintf "buildTypeDefPass4: Creating Super Class Chain of %s\n" tdef.Name; 
            tdef.Extends    |> Option.iter (traverseType false priority);
        
        // We absolutely need the interface types...
        if priority >= 1 then 
            if verbose2 then dprintf "buildTypeDefPass4: Creating Interface Chain of %s\n" tdef.Name; 
            tdef.Implements |> ILList.iter (traverseType false priority);
            
        // We have to define all struct types in all methods before a class is defined. This only has any effect when there is a struct type
        // being defined simultaneously with this type.
        if priority >= 1 then 
            if verbose2 then dprintf "buildTypeDefPass4: Doing value types in method signatures of %s, #mdefs = %d\n" tdef.Name tdef.Methods.AsList.Length; 
            tdef.Methods |> Seq.iter   (fun md -> md.Parameters |> ILList.iter (fun p -> p.Type |> (traverseType true 1))
                                                  md.Return.Type |> traverseType true 1);
        
        if priority >= 1 then 
            if verbose2 then dprintf "buildTypeDefPass4: Do value types in fields of %s\n" tdef.Name; 
            tdef.Fields.AsList |> List.iter (fun fd -> traverseType true 1 fd.Type);
        
        if verbose2 then dprintf "buildTypeDefPass4: Done with dependencies of %s\n" tdef.Name
            
    and traverseType valueTypesOnly priority typ = 
        if verbose2 then dprintf "- traverseType %+A\n" typ;
        getTypeRefsInType valueTypesOnly typ []
        |> List.filter (isEmittedTypeRef emEnv)
        |> List.iter (traverseTypeRef priority)

    and traverseTypeRef priority  tref = 
        let typB = envGetTypB emEnv tref
        if verbose2 then dprintf "- considering reference to type %s\n" typB.FullName;
        if not (visited.ContainsKey(tref)) || visited.[tref] > priority then 
            visited.[tref] <- priority;
            let tdef = envGetTypeDef emEnv tref
            if verbose2 then dprintf "- traversing type %s\n" typB.FullName;        
            let typeCreationHandler =
                let nestingToProbe = tref.Enclosing 
                ResolveEventHandler(
                    fun o r ->
                        let typeName = r.Name
                        let typeRef = ILTypeRef.Create(ILScopeRef.Local, nestingToProbe, typeName)
                        match emEnv.emTypMap.TryFind typeRef with
                        |   Some(_,tb,_,_) -> 
                                if not (tb.IsCreated()) then
                                    tb.CreateTypeAndLog() |> ignore
                                tb.Assembly
                        |   None -> null
                )
            System.AppDomain.CurrentDomain.add_TypeResolve typeCreationHandler
            try
                traverseTypeDef priority tref tdef;
            finally
               System.AppDomain.CurrentDomain.remove_TypeResolve typeCreationHandler           
            if not (created.ContainsKey(tref)) then 
                created.[tref] <- true;   
                if verbose2 then dprintf "- creating type %s\n" typB.FullName;
                typB.CreateTypeAndLog()  |> ignore
    
    traverseTypeRef 2 tref 

let rec buildTypeDefPass4 (visited,created) nesting emEnv (tdef : ILTypeDef) =
    if verbose2 then dprintf "buildTypeDefPass4 %s\n" tdef.Name; 
    let tref = mkRefForNestedILTypeDef ILScopeRef.Local (nesting,tdef)
    createTypeRef (visited,created) emEnv tref;
        
    
    // nested types
    let nesting = nesting @ [tdef]
    tdef.NestedTypes |> Seq.iter (buildTypeDefPass4 (visited,created) nesting emEnv)

//----------------------------------------------------------------------------
// buildModuleType
//----------------------------------------------------------------------------
     
let buildModuleTypePass1 cenv (modB:ModuleBuilder) emEnv (tdef:ILTypeDef) =
    buildTypeDefPass1 cenv emEnv modB modB.DefineTypeAndLog [] tdef

let buildModuleTypePass1b          cenv emEnv tdef = buildTypeDefPass1b cenv [] emEnv tdef
let buildModuleTypePass2           cenv emEnv tdef = buildTypeDefPass2 cenv [] emEnv tdef
let buildModuleTypePass3 cenv modB emEnv tdef = buildTypeDefPass3 cenv [] modB emEnv tdef
let buildModuleTypePass4 visited   emEnv tdef = buildTypeDefPass4 visited [] emEnv tdef

//----------------------------------------------------------------------------
// buildModuleFragment - only the types the fragment get written
//----------------------------------------------------------------------------
    
let buildModuleFragment cenv emEnv (asmB : AssemblyBuilder) (modB : ModuleBuilder) (m: ILModuleDef) =
    let tdefs = m.TypeDefs.AsList 

    let emEnv = List.fold (buildModuleTypePass1 cenv modB) emEnv tdefs
    tdefs |> List.iter (buildModuleTypePass1b cenv emEnv) 
    let emEnv = List.fold (buildModuleTypePass2 cenv) emEnv  tdefs
    
    for delayedFieldInit in emEnv.delayedFieldInits do
        delayedFieldInit()

    let emEnv = { emEnv with delayedFieldInits = [] }

    let emEnv = List.fold (buildModuleTypePass3 cenv modB) emEnv  tdefs
    let visited = new Dictionary<_,_>(10) 
    let created = new Dictionary<_,_>(10) 
    tdefs |> List.iter (buildModuleTypePass4  (visited,created) emEnv) 
    let emEnv = Seq.fold envUpdateCreatedTypeRef emEnv created.Keys // update typT with the created typT
    emitCustomAttrs cenv emEnv modB.SetCustomAttributeAndLog m.CustomAttrs;    
    m.Resources.AsList |> List.iter (fun r -> 
        let attribs = (match r.Access with ILResourceAccess.Public -> ResourceAttributes.Public | ILResourceAccess.Private -> ResourceAttributes.Private) 
        match r.Location with 
        | ILResourceLocation.Local bf -> 
            modB.DefineManifestResourceAndLog(r.Name, new System.IO.MemoryStream(bf()), attribs)
        | ILResourceLocation.File (mr,_n) -> 
           asmB.AddResourceFileAndLog(r.Name, mr.Name, attribs)
        | ILResourceLocation.Assembly _ -> 
           failwith "references to resources other assemblies may not be emitted using System.Reflection");
    emEnv

//----------------------------------------------------------------------------
// test hook
//----------------------------------------------------------------------------

let mkDynamicAssemblyAndModule (assemblyName, optimize, debugInfo) =
    let filename = assemblyName ^ ".dll"
    let currentDom  = System.AppDomain.CurrentDomain   
    let asmDir  = "."
    let asmName = new AssemblyName()
    asmName.Name <- assemblyName;
    let asmB = currentDom.DefineDynamicAssemblyAndLog(asmName,AssemblyBuilderAccess.RunAndSave,asmDir) 
    if not optimize then 
        let daType = typeof<System.Diagnostics.DebuggableAttribute>;
        let daCtor = daType.GetConstructor [| typeof<System.Diagnostics.DebuggableAttribute.DebuggingModes> |]
        let daBuilder = new CustomAttributeBuilder(daCtor, [| System.Diagnostics.DebuggableAttribute.DebuggingModes.DisableOptimizations ||| System.Diagnostics.DebuggableAttribute.DebuggingModes.Default  |])
        asmB.SetCustomAttributeAndLog(daBuilder);
    
    let modB = asmB.DefineDynamicModuleAndLog(assemblyName,filename,debugInfo)
    asmB,modB

let emitModuleFragment (ilg, emEnv, asmB : AssemblyBuilder, modB : ModuleBuilder, modul : IL.ILModuleDef, debugInfo : bool, resolvePath) =
    let cenv = { ilg = ilg ; generatePdb = debugInfo; resolvePath=resolvePath }

    let emEnv = buildModuleFragment cenv emEnv asmB modB modul
    match modul.Manifest with 
    | None -> ()
    | Some mani ->  
       // REVIEW: remainder of manifest
       emitCustomAttrs cenv emEnv asmB.SetCustomAttributeAndLog mani.CustomAttrs;    
    // invoke entry point methods
    let execEntryPtFun ((typB : TypeBuilder),methodName) () =
      try        
        ignore (typB.InvokeMemberAndLog(methodName,BindingFlags.InvokeMethod ||| BindingFlags.Public ||| BindingFlags.Static,[| |]));       
        None
      with 
         | :? System.Reflection.TargetInvocationException as e ->
             Some(e.InnerException)
   
    let emEnv,entryPts = envPopEntryPts emEnv
    let execs = List.map execEntryPtFun entryPts
    emEnv,execs


//----------------------------------------------------------------------------
// lookup* allow conversion from AbsIL to their emitted representations
//----------------------------------------------------------------------------

// TypeBuilder is a subtype of Type.
// However, casting TypeBuilder to Type is not the same as getting Type proper.
// The builder version does not implement all methods on the parent.
// 
// The emEnv stores (typT:Type) for each tref.
// Once the emitted type is created this typT is updated to ensure it is the Type proper.
// So Type lookup will return the proper Type not TypeBuilder.
let LookupTypeRef   emEnv tref = Zmap.tryFind tref emEnv.emTypMap   |> Option.map (function (_typ,_,_,Some createdTyp) -> createdTyp | (typ,_,_,None) -> typ)
let LookupType      cenv emEnv typ  = convCreatedType cenv emEnv typ

// Lookups of ILFieldRef and MethodRef may require a similar non-Builder-fixup post Type-creation.
let LookupFieldRef  emEnv fref = Zmap.tryFind fref emEnv.emFieldMap |> Option.map (fun fieldBuilder  -> fieldBuilder  :> FieldInfo)
let LookupMethodRef emEnv mref = Zmap.tryFind mref emEnv.emMethMap  |> Option.map (fun methodBuilder -> methodBuilder :> MethodInfo)

