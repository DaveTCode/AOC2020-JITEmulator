using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace ILEmulator
{
    class Program
    {
        internal static void Main()
        {
            var lines = new string[]
            {
                "nop +0",
                "acc +1",
                "jmp +4",
                "acc +3",
                "jmp -3",
                "acc -99",
                "acc +1",
                "nop -4",
                "acc +6",
            };

            var (emulator, accmulatorFieldInfo, runMethodInfo) = CreateEmulator(lines);

            Console.WriteLine(accmulatorFieldInfo.GetValue(emulator));

            runMethodInfo.Invoke(emulator, Array.Empty<object>());

            Console.WriteLine(accmulatorFieldInfo.GetValue(emulator));
        }

        internal static (object, FieldInfo, MethodInfo) CreateEmulator(string[] program)
        {
            var asmName = new AssemblyName("Emulator");
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("Execute");
            var typeBuilder = moduleBuilder.DefineType("Emulator", TypeAttributes.Public);
            var accumulatorField = typeBuilder.DefineField("Accumulator", typeof(int), FieldAttributes.Public);

            #region Create Constructor
            var constructor = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { typeof(int) });
            var constructorIL = constructor.GetILGenerator();
            constructorIL.Emit(OpCodes.Ldarg_0);
            constructorIL.Emit(OpCodes.Call,typeof(object).GetConstructor(Type.EmptyTypes));
            constructorIL.Emit(OpCodes.Ldarg_0);
            constructorIL.Emit(OpCodes.Ldarg_1);
            constructorIL.Emit(OpCodes.Stfld, accumulatorField);
            constructorIL.Emit(OpCodes.Ret);

            var defaultConstructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            var defaultConstructorIL = defaultConstructorBuilder.GetILGenerator();
            defaultConstructorIL.Emit(OpCodes.Ldarg_0);
            defaultConstructorIL.Emit(OpCodes.Ldc_I4_S, 0);
            defaultConstructorIL.Emit(OpCodes.Call, constructor);
            defaultConstructorIL.Emit(OpCodes.Ret);
            #endregion

            #region Emulation Method
            var methodBuilder = typeBuilder.DefineMethod("Run", MethodAttributes.Public, CallingConventions.Standard);
            var methodIL = methodBuilder.GetILGenerator();
            methodIL.Emit(OpCodes.Nop);

            var labels = program.Select(i => methodIL.DefineLabel()).ToArray();
            foreach ((var instruction, var ix) in program.Select((instr, ix) => (instr, ix)))
            {
                methodIL.MarkLabel(labels[ix]);
                switch (instruction.Substring(0, 3))
                {
                    case "nop":
                        methodIL.Emit(OpCodes.Nop);
                        break;
                    case "jmp":
                        var jumpRelative = int.Parse(instruction[3..].Replace("+", ""));
                        methodIL.Emit(OpCodes.Br, labels[ix + jumpRelative]);
                        break;
                    case "acc":
                        var amount = long.Parse(instruction[3..].Replace("+", ""));
                        methodIL.Emit(OpCodes.Ldarg_0); // Load the class onto the eval stack
                        methodIL.Emit(OpCodes.Ldarg_0);
                        methodIL.Emit(OpCodes.Ldfld, accumulatorField); // Load accumulator onto eval stack
                        methodIL.Emit(OpCodes.Ldc_I8, amount); // Load addition onto eval stack
                        methodIL.Emit(OpCodes.Add); // Add and store result on eval stack
                        methodIL.Emit(OpCodes.Stfld, accumulatorField); // Write value back into the class field
                        break;
                }
            }

            methodIL.Emit(OpCodes.Ret);

            #endregion

            var t = typeBuilder.CreateType();
            var fi = t.GetField(accumulatorField.Name);
            var mi = t.GetMethod(methodBuilder.Name);

            return (assemblyBuilder.CreateInstance(t.Name), fi, mi);
        }
    }
}
