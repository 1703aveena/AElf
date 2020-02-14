using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AElf.CSharp.CodeOps.Validators.Method
{
    public class DescriptorAccessValidator : IValidator<MethodDefinition>
    {
        public IEnumerable<ValidationResult> Validate(MethodDefinition method)
        {
            if (!method.HasBody || method.IsConstructor)
                return Enumerable.Empty<ValidationResult>();
            

            // If there is any method accessing a FileDescriptor type field to set, then this is a problem.
            // FileDescriptor field should only be set in constructor
            var instructions = method.Body.Instructions
                .Where(i => i.OpCode == OpCodes.Stsfld &&
                            i.Operand is FieldDefinition field &&
                            field.FieldType.Name == nameof(FileDescriptor)).ToArray();

            if (instructions.Any())
            {
                return instructions.Select(i => new FloatOpsValidationResult($"")
                    .WithInfo(method.Name, method.DeclaringType.Namespace, method.DeclaringType.Name, null)); 
            }

            return Enumerable.Empty<ValidationResult>();
        }
    }
}