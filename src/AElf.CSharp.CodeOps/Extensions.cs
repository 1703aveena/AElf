using System;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace AElf.CSharp.CodeOps
{
    public static class Extensions
    {
        public static bool HasSameBody(this MethodDefinition sourceMethod, MethodDefinition targetMethod)
        {
            // Exclude nop opcodes (compile in debug mode adds nop to be able to place breakpoint, ignore those)
            var sourceMethodBodyInstructions = sourceMethod.Body.Instructions
                .Where(i => i.OpCode != OpCodes.Nop).ToArray();
            var targetMethodBodyInstructions = targetMethod.Body.Instructions
                .Where(i => i.OpCode != OpCodes.Nop).ToArray();

            // Compare method body
            return !sourceMethodBodyInstructions.Where((t, i) => 
                t.ToComparableString() != targetMethodBodyInstructions[i].ToComparableString()).Any();
        }

        private static string ToComparableString(this Instruction instruction)
        {
            string operandStr;
            
            if (instruction.Operand is FieldDefinition field)
            {
                operandStr = field.Name;
            }
            else if (instruction.Operand is Instruction ins) // Is probably branching to another instruction
            {
                operandStr = ins.OpCode.ToString() + ins.Operand; // May restrict to branching to ret only
            }
            else
            {
                operandStr = instruction.Operand?.ToString();
            }

            return instruction.OpCode + operandStr;
        }

        public static bool HasSameParameters(this MethodDefinition sourceMethod, MethodDefinition targetMethod)
        {
            // Don't mind if injected type method has more parameters since we check the body to be the same
            return sourceMethod.Parameters.Count == targetMethod.Parameters.Count && 
                   sourceMethod.Parameters.All(sp => 
                       targetMethod.Parameters.SingleOrDefault(tp => 
                           tp.Name == sp.Name && tp.ParameterType.FullName == sp.ParameterType.FullName) != null);
        }

        public static bool HasSameFields(this TypeDefinition sourceType, TypeDefinition targetType)
        {
            // Don't mind if injected type has more fields since we check each of the methods' bodies
            return sourceType.Fields.Count == targetType.Fields.Count && 
                   sourceType.Fields.All(sp => 
                       targetType.Fields.SingleOrDefault(tp => 
                           tp.Name == sp.Name && tp.FieldType.FullName == sp.FieldType.FullName) != null);
        }

        public static void RemoveCoverLetInjectedInstructions(this MethodDefinition method)
        {
            var instructions = method.Body.Instructions.ToList();
            var il = method.Body.GetILProcessor();
        
            il.Body.SimplifyMacros();
            foreach (var instruction in instructions)
            {
                if (instruction.OpCode == OpCodes.Call &&
                    instruction.Operand.ToString().Contains("Coverlet.Core.Instrumentation.Tracker"))
                {
                    il.Remove(instruction.Previous);
                    il.Remove(instruction);
                }
            }
            il.Body.OptimizeMacros();
        }

        public static Type FindContractType(this Assembly assembly)
        {
            var types = assembly.GetTypes();
            return types.SingleOrDefault(t => typeof(ISmartContract).IsAssignableFrom(t) && !t.IsNested);
        }
        
        public static Type FindContractBaseType(this Assembly assembly)
        {
            var types = assembly.GetTypes();
            return types.SingleOrDefault(t => typeof(ISmartContract).IsAssignableFrom(t) && t.IsNested);
        }

        public static Type FindContractContainer(this Assembly assembly)
        {
            var contractBase = FindContractBaseType(assembly);
            return contractBase.DeclaringType;
        }
        
        public static Type FindExecutionObserverType(this Assembly assembly)
        {
            return assembly.GetTypes().SingleOrDefault(t => t.Name == nameof(ExecutionObserverProxy));
        }
    }
}
