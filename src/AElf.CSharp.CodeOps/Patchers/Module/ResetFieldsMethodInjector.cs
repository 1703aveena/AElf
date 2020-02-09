using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Org.BouncyCastle.Utilities.Collections;

namespace AElf.CSharp.CodeOps.Patchers.Module
{
    public class ResetFieldsMethodInjector : IPatcher<ModuleDefinition>
    {
        public void Patch(ModuleDefinition module)
        {
            foreach (var type in module.Types.Where(t => !t.IsContractImplementation()))
            {
                InjectTypeWithResetFields(module, type);
                AddCallToSubClassResetFields(type);
            }

            InjectContractWithResetFields(module);
        }

        private bool InjectTypeWithResetFields(ModuleDefinition module, TypeDefinition type)
        {
            var callToNestedResetNeeded = false;
            
            // Inject for nested types first
            foreach (var nestedType in type.NestedTypes)
            {
                callToNestedResetNeeded = InjectTypeWithResetFields(module, nestedType);
            }
            
            // Get static non-initonly, non-constant fields
            // TODO: Deal with FileDescriptor types in a proper way
            var fields = type.Fields
                .Where(f => f.FieldType.Name != "FileDescriptor" && f.IsStatic && !(f.IsInitOnly || f.HasConstant)).ToList();

            callToNestedResetNeeded |= fields.Any();

            if (callToNestedResetNeeded)
            {
                // Inject reset fields method in type
                var resetFieldsMethod = ConstructResetFieldsMethod(module, fields, false);

                type.Methods.Add(resetFieldsMethod);
            }

            return callToNestedResetNeeded;
        }

        private void AddCallToSubClassResetFields(TypeDefinition type)
        {
            var superClassResetMethod = type.Methods.FirstOrDefault(m => m.Name == nameof(ResetFields));

            if (superClassResetMethod == null)
                return;
            
            var il = superClassResetMethod.Body.GetILProcessor();

            var ret = il.Body.Instructions.Last();

            // Get reference of the methods to nested types' reset fields methods
            var callRequired = type.NestedTypes
                .Where(t => t.Methods.Any(m => m.Name == nameof(ResetFields)))
                .Select(t => t.Methods.Single(m => m.Name == nameof(ResetFields)));
            
            foreach (var subResetMethod in callRequired)
            {
                // Add a call to sub class' reset fields method
                il.InsertBefore(ret, il.Create(OpCodes.Call, subResetMethod));
            }

            // Do the same for nested types
            foreach (var nestedType in type.NestedTypes)
            {
                AddCallToSubClassResetFields(nestedType);
            }
        }

        private void InjectContractWithResetFields(ModuleDefinition module)
        {
            var contractImplementation = module.Types.Single(t => t.IsContractImplementation());
            
            foreach (var nestedType in contractImplementation.NestedTypes)
            {
                InjectTypeWithResetFields(module, nestedType);
                AddCallToSubClassResetFields(nestedType);
            }
            
            var otherTypes = module.Types.Where(t => !t.IsContractImplementation()).ToList();

            // Add contract's nested types as well
            otherTypes.AddRange(contractImplementation.NestedTypes);
            
            // TODO: Handle nullable fields' default value
            var resetFieldsMethod = ConstructResetFieldsMethod(module, 
                contractImplementation.Fields.Where(f => !(f.IsInitOnly || f.HasConstant)), true);

            //var resetFieldsMethod = ConstructResetFieldsMethod(module, Enumerable.Empty<FieldDefinition>(), true);

            var il = resetFieldsMethod.Body.GetILProcessor();

            var ret = il.Body.Instructions.Last();

            // Call other types' reset fields method from contract implementation reset method
            foreach (var type in otherTypes)
            {
                var resetMethodRef = type.Methods.FirstOrDefault(m => m.Name == nameof(ResetFields));
                if (resetMethodRef == null)
                    continue;
                il.InsertBefore(ret, il.Create(OpCodes.Call, resetMethodRef));
            }
            
            contractImplementation.Methods.Add(resetFieldsMethod);
        }

        private MethodDefinition ConstructResetFieldsMethod(ModuleDefinition module, IEnumerable<FieldDefinition> fields, bool instanceMethod)
        {
            var attributes = instanceMethod
                ? MethodAttributes.Public | MethodAttributes.HideBySig
                : MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static;
            
            var resetFieldsMethod = new MethodDefinition(
                nameof(ResetFields), 
                attributes, 
                module.ImportReference(typeof(void))
            );
            
            resetFieldsMethod.Body.Variables.Add(new VariableDefinition(module.ImportReference(typeof(bool))));
            var il = resetFieldsMethod.Body.GetILProcessor();

            foreach (var field in fields)
            {
                if (instanceMethod && !field.IsStatic)
                    il.Emit(OpCodes.Ldarg_0); // this
                LoadDefaultValue(il, field);
                il.Emit(field.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, field);
            }

            il.Emit(OpCodes.Ret);

            return resetFieldsMethod;
        }

        private static void LoadDefaultValue(ILProcessor il, FieldReference field)
        {
            il.Emit(field.FieldType.IsValueType ? OpCodes.Ldc_I4_0 : OpCodes.Ldnull);
        }

        public static void ResetFields()
        {
            // This method is to use its name only
        }
    }
}