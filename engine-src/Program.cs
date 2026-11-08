using System;
using System.Globalization;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// MajdataPlay 2.0.3 assembly patcher.
//
//   patcher <input.dll> --inspect
//       -> prints JSON: {"hasFault":bool,"r":..,"g":..,"b":..,"a":..}
//
//   patcher <input.dll> <output.dll> --color R,G,B,A
//       -> writes a patched assembly (Fault counter + sensor highlight color).
//          Components are floats in 0..1. Omitting --color uses (1,0,0,0.3).
//
// The patcher is idempotent: if the input already contains the Fault counter,
// steps 1-6 are skipped and only the sensor color is rewritten. That makes it
// safe to run against both the pristine DLL and an already-patched one, and
// lets the GUI tool restore the whole mod after an official hub update.
internal static class Program
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static int Main(string[] args)
    {
        try { return Run(args); }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            return 1;
        }
    }

    private static void Usage()
    {
        Console.Error.WriteLine("usage:");
        Console.Error.WriteLine("  patcher <input.dll> --inspect");
        Console.Error.WriteLine("  patcher <input.dll> <output.dll> --color R,G,B,A");
    }

    private static string? Option(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return null;
    }

    private static (float r, float g, float b, float a) ParseColor(string text)
    {
        var parts = text.Split(',');
        if (parts.Length != 4)
            throw new Exception("--color expects exactly 4 comma separated floats: R,G,B,A");
        var v = new float[4];
        for (int i = 0; i < 4; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, Inv, out v[i]))
                throw new Exception($"invalid color component #{i + 1}: '{parts[i]}'");
            if (v[i] < 0f || v[i] > 1f)
                throw new Exception($"color component #{i + 1} out of range 0..1: {v[i]}");
        }
        return (v[0], v[1], v[2], v[3]);
    }

    // The active branch of `flag ? Color(R,G,B,A) : Color(0,0,0,0)` is the
    // target of a brtrue.s. That branch target is a semantic anchor which
    // stays valid no matter what color was written previously, so the patcher
    // stays idempotent even after alpha is changed away from its stock 0.3.
    // The four instructions must be mutated in place: R is a branch target,
    // so replacing the Instruction objects would break the branch reference.
    private static Instruction[] LocateColorComponents(MethodDefinition onPreUpdate)
    {
        var branch = onPreUpdate.Body.Instructions.FirstOrDefault(i =>
            (i.OpCode == OpCodes.Brtrue_S || i.OpCode == OpCodes.Brtrue) &&
            i.Operand is Instruction t && t.OpCode == OpCodes.Ldc_R4)
            ?? throw new Exception("sensor active-branch (brtrue -> ldc.r4) not found in OnPreUpdate");

        var r = (Instruction)branch.Operand;
        var g = r.Next ?? throw new Exception("sensor G component not found");
        var b = g.Next ?? throw new Exception("sensor B component not found");
        var a = b.Next ?? throw new Exception("sensor A component not found");
        var all = new[] { r, g, b, a };
        foreach (var i in all)
            if (i.OpCode != OpCodes.Ldc_R4 || i.Operand is not float)
                throw new Exception($"unexpected sensor color instruction: {i}");

        if (a.Next is null || a.Next.OpCode != OpCodes.Newobj ||
            a.Next.Operand is not MethodReference ctor ||
            ctor.DeclaringType.Name != "Color" || ctor.Parameters.Count != 4)
            throw new Exception("sensor Color::.ctor not found directly after the four components");

        return all;
    }

    private static string F(Instruction i) =>
        ((float)i.Operand!).ToString("R", Inv);

    private static int Run(string[] args)
    {
        if (args.Length < 1) { Usage(); return 2; }

        string input = args[0];
        bool inspect = args.Any(a => string.Equals(a, "--inspect", StringComparison.OrdinalIgnoreCase));
        string? colorText = Option(args, "--color");
        string? output = (args.Length >= 2 && !args[1].StartsWith("--", StringComparison.Ordinal))
            ? args[1] : null;

        var asm = AssemblyDefinition.ReadAssembly(input, new ReaderParameters
        {
            ReadSymbols = false,
            InMemory = true
        });
        var module = asm.MainModule;

        var enumType = module.GetType("MajdataPlay.Settings.BGInfoOption")
                       ?? throw new Exception("BGInfoOption type not found");
        var counter = module.GetType("MajdataPlay.Scenes.Game.ObjectCounter")
                      ?? throw new Exception("ObjectCounter type not found");
        var renderer = module.GetType("MajdataPlay.DummyTouchPanelRenderer")
                       ?? throw new Exception("DummyTouchPanelRenderer type not found");
        var onPreUpdate = renderer.Methods.First(m => m.Name == "OnPreUpdate");

        var comps = LocateColorComponents(onPreUpdate);
        bool hasFault = enumType.Fields.Any(f => f.Name == "Fault");

        if (inspect)
        {
            Console.WriteLine(
                "{\"hasFault\":" + (hasFault ? "true" : "false") +
                ",\"r\":" + F(comps[0]) +
                ",\"g\":" + F(comps[1]) +
                ",\"b\":" + F(comps[2]) +
                ",\"a\":" + F(comps[3]) + "}");
            return 0;
        }

        if (output is null) { Usage(); return 2; }
        var color = colorText is null ? (1f, 0f, 0f, 0.3f) : ParseColor(colorText);

        if (!hasFault)
        {
            // ---- 1. BGInfoOption.Fault = 16 (after None = 15) ----
            var faultEnumField = new FieldDefinition(
                "Fault",
                FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault,
                enumType) { Constant = 16 };
            enumType.Fields.Add(faultEnumField);

            // ---- 2. ObjectCounter._fault ----
            var faultField = new FieldDefinition("_fault", FieldAttributes.Private, module.TypeSystem.Int64);
            counter.Fields.Add(faultField);

            MethodDefinition Find(string name, int paramCount) =>
                counter.Methods.First(m => m.Name == name && m.Parameters.Count == paramCount);

            var clear = Find("Clear", 0);
            var updateComboCount = Find("UpdateComboCount", 2);
            var updateOutput = Find("UpdateInfoDisplayerOutput", 6);
            var initText = Find("SetInfoDisplayerInitText", 4);
            var setActive = counter.Methods.First(m => m.Name == "SetInfoDisplayerActive");

            // Reuse an existing callvirt to TMP_Text::set_text(string) so the
            // reference keeps its original scope (Unity.TextMeshPro).
            MethodReference? setText = counter.Methods
                .Where(m => m.HasBody)
                .SelectMany(m => m.Body.Instructions)
                .Where(i => i.OpCode == OpCodes.Callvirt)
                .Select(i => i.Operand as MethodReference)
                .FirstOrDefault(mr => mr is not null && mr.Name == "set_text" && mr.Parameters.Count == 1);
            if (setText is null) throw new Exception("TMP_Text::set_text reference not found");

            // Core-library references must use the target module's mscorlib scope.
            var corelib = module.AssemblyReferences.First(r => r.Name == "mscorlib");
            var objectRef = new TypeReference("System", "Object", module, corelib);
            var int64Ref = new TypeReference("System", "Int64", module, corelib);
            var objectToString = new MethodReference("ToString", module.TypeSystem.String, objectRef) { HasThis = true };

            // ---- 3. Clear(): _fault = 0 ----
            {
                var il = clear.Body.GetILProcessor();
                var first = clear.Body.Instructions[0];
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldc_I4_0));
                il.InsertBefore(first, Instruction.Create(OpCodes.Conv_I8));
                il.InsertBefore(first, Instruction.Create(OpCodes.Stfld, faultField));
            }

            // ---- 4. UpdateComboCount(): decrement on Miss / Good / Great, skip Perfect + TooFast ----
            // JudgeGrade: Miss=0, LateGood=1, LateGreat*=2..4, Perfect*=5..9,
            //             FastGreat*=10..12, FastGood=13, TooFast=14
            {
                var il = updateComboCount.Body.GetILProcessor();
                var first = updateComboCount.Body.Instructions[0];
                var grade = updateComboCount.Parameters[0];
                var dec = Instruction.Create(OpCodes.Ldarg_0);

                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_S, grade));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldc_I4, 5));
                il.InsertBefore(first, Instruction.Create(OpCodes.Blt, dec));       // 0..4 -> decrement
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_S, grade));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldc_I4, 10));
                il.InsertBefore(first, Instruction.Create(OpCodes.Blt, first));     // 5..9 -> skip
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_S, grade));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldc_I4, 14));
                il.InsertBefore(first, Instruction.Create(OpCodes.Bge, first));     // >=14 -> skip
                il.InsertBefore(first, dec);                                       // 10..13 -> decrement
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldfld, faultField));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldc_I4_1));
                il.InsertBefore(first, Instruction.Create(OpCodes.Conv_I8));
                il.InsertBefore(first, Instruction.Create(OpCodes.Sub));
                il.InsertBefore(first, Instruction.Create(OpCodes.Stfld, faultField));
            }

            // ---- 5. UpdateInfoDisplayerOutput(): render the Fault value ----
            {
                var il = updateOutput.Body.GetILProcessor();
                var first = updateOutput.Body.Instructions[0];
                var option = updateOutput.Parameters[5];
                var text = updateOutput.Parameters[0];
                var textObject = updateOutput.Parameters[1];
                var headerObject = updateOutput.Parameters[3];

                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_S, option));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldind_I4));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldc_I4, 16));
                il.InsertBefore(first, Instruction.Create(OpCodes.Bne_Un, first));

                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_S, textObject));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_S, headerObject));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldc_I4_1));
                il.InsertBefore(first, Instruction.Create(OpCodes.Call, setActive));

                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_S, text));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldfld, faultField));
                il.InsertBefore(first, Instruction.Create(OpCodes.Box, int64Ref));
                il.InsertBefore(first, Instruction.Create(OpCodes.Callvirt, objectToString));
                il.InsertBefore(first, Instruction.Create(OpCodes.Callvirt, setText));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ret));
            }

            // ---- 6. SetInfoDisplayerInitText(): header "Fault" ----
            {
                var il = initText.Body.GetILProcessor();
                var first = initText.Body.Instructions[0];
                var option = initText.Parameters[3];
                var header = initText.Parameters[1];
                var ret = Instruction.Create(OpCodes.Ret);

                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_S, option));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldind_I4));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldc_I4, 16));
                il.InsertBefore(first, Instruction.Create(OpCodes.Bne_Un, first));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_S, header));
                il.InsertBefore(first, Instruction.Create(OpCodes.Brfalse, ret));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_S, header));
                il.InsertBefore(first, Instruction.Create(OpCodes.Ldstr, "Fault"));
                il.InsertBefore(first, Instruction.Create(OpCodes.Callvirt, setText));
                il.InsertBefore(first, ret);
            }

            clear.Body.MaxStackSize = Math.Max(clear.Body.MaxStackSize, 4);
            updateComboCount.Body.MaxStackSize = Math.Max(updateComboCount.Body.MaxStackSize, 4);
            updateOutput.Body.MaxStackSize = Math.Max(updateOutput.Body.MaxStackSize, 8);
            initText.Body.MaxStackSize = Math.Max(initText.Body.MaxStackSize, 4);
        }

        // ---- 7. DummyTouchPanelRenderer.OnPreUpdate(): active sensor highlight ----
        // Mutate Operand in place: the R instruction is a brtrue.s target.
        comps[0].Operand = color.Item1;
        comps[1].Operand = color.Item2;
        comps[2].Operand = color.Item3;
        comps[3].Operand = color.Item4;

        asm.Write(output, new WriterParameters { WriteSymbols = false });
        Console.WriteLine(
            "OK hasFault=" + (hasFault ? "existing" : "injected") +
            " color=" + color.Item1.ToString("R", Inv) + "," +
            color.Item2.ToString("R", Inv) + "," +
            color.Item3.ToString("R", Inv) + "," +
            color.Item4.ToString("R", Inv));
        return 0;
    }
}
