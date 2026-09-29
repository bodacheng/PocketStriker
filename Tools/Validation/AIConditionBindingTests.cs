using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Inspect the actual player IL without loading Unity native objects. String-only
// reflection dispatch works in the Editor but supplies no direct linker roots.
internal static class AIConditionBindingTests
{
    public static int Main(string[] args)
    {
        try
        {
            using var player = AssemblyDefinition.ReadAssembly(args[0]);
            using var shared = AssemblyDefinition.ReadAssembly(args[1]);
            var behavior = player.MainModule.GetType("Soul.Behavior");
            var rules = shared.MainModule.GetType("MCombat.Shared.AI.AiConditionResponseUtility");
            var required = new HashSet<string>(rules.Methods.Where(m => m.HasBody)
                .SelectMany(m => m.Body.Instructions)
                .Where(i => i.OpCode == OpCodes.Ldstr).Select(i => (string)i.Operand));
            if (required.Count == 0) throw new Exception("No AI condition names found in the shared rules.");

            var reachable = new HashSet<MethodDefinition>();
            var pending = new Stack<MethodDefinition>();
            pending.Push(behavior.Methods.Single(m => m.IsConstructor && m.IsStatic));
            while (pending.Count > 0)
            {
                var method = pending.Pop();
                if (!reachable.Add(method) || !method.HasBody) continue;
                foreach (var instruction in method.Body.Instructions)
                {
                    if (!(instruction.Operand is MethodReference reference)) continue;
                    // Only follow this type and its compiler-generated delegates.
                    var typeName = reference.DeclaringType.FullName;
                    if (typeName != behavior.FullName && !typeName.StartsWith(behavior.FullName + "/")) continue;
                    pending.Push(reference.Resolve());
                }
            }

            foreach (var name in required.OrderBy(value => value))
            {
                var condition = behavior.Methods.SingleOrDefault(m => m.Name == name &&
                    m.Parameters.Count == 0 && m.ReturnType.FullName == "System.Boolean");
                if (condition == null || !reachable.Contains(condition))
                    throw new Exception("AI condition has no direct binding for IL2CPP: " + name);
            }
            Console.WriteLine($"PASS: {required.Count} shared AI conditions have direct player-code bindings");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }
}
