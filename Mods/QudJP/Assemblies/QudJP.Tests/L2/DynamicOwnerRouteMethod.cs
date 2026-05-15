using System.Reflection;
using System.Reflection.Emit;

namespace QudJP.Tests.L2;

internal sealed class DynamicOwnerRouteMethod
{
    private readonly FieldInfo callbackField;

    private DynamicOwnerRouteMethod(MethodInfo method, FieldInfo callbackField)
    {
        Method = method;
        this.callbackField = callbackField;
    }

    public MethodInfo Method { get; }

    public void Invoke(Action callback)
    {
        callbackField.SetValue(null, callback);
        try
        {
            _ = Method.Invoke(null, null);
        }
        finally
        {
            callbackField.SetValue(null, null);
        }
    }

    public static DynamicOwnerRouteMethod Create(string typeName, string methodName)
    {
        var assemblyName = new AssemblyName("QudJP.Tests.DynamicOwnerRoutes." + Guid.NewGuid().ToString("N"));
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName.Name!);
        var typeBuilder = module.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        var callbackField = typeBuilder.DefineField(
            "Callback",
            typeof(Action),
            FieldAttributes.Public | FieldAttributes.Static);
        var methodBuilder = typeBuilder.DefineMethod(
            methodName,
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(bool),
            Type.EmptyTypes);
        var il = methodBuilder.GetILGenerator();
        var skipInvoke = il.DefineLabel();
        il.Emit(OpCodes.Ldsfld, callbackField);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Brfalse_S, skipInvoke);
        il.Emit(OpCodes.Callvirt, typeof(Action).GetMethod(nameof(Action.Invoke))!);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);
        il.MarkLabel(skipInvoke);
        il.Emit(OpCodes.Pop);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Ret);

        var type = typeBuilder.CreateTypeInfo()!.AsType();
        return new DynamicOwnerRouteMethod(
            type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(typeName, methodName),
            type.GetField("Callback", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingFieldException(typeName, "Callback"));
    }
}
