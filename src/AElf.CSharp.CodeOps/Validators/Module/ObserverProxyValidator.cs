using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AElf.CSharp.CodeOps.Validators.Module
{
    public class ObserverProxyValidator : IValidator<ModuleDefinition>
    {
        private static readonly TypeDefinition CounterProxyTypeRef =
            AssemblyDefinition.ReadAssembly(typeof(ExecutionObserverProxy).Assembly.Location)
                .MainModule.Types.SingleOrDefault(t => t.Name == nameof(ExecutionObserverProxy));

        private TypeDefinition _injProxyType;
        private MethodDefinition _injProxySetObserver;
        private MethodDefinition _injProxyCount;

        public IEnumerable<ValidationResult> Validate(ModuleDefinition module)
        {
            var errors = new List<ValidationResult>();
            
            // Get counter type
            _injProxyType = module.Types.SingleOrDefault(t => t.Name == nameof(ExecutionObserverProxy));

            if (_injProxyType == null)
                return new List<ValidationResult>
                {
                    new ObserverProxyValidationResult("Could not find execution observer proxy in contract.")
                };

            CheckObserverProxyIsNotTampered(errors);
            
            _injProxySetObserver =
                _injProxyType.Methods.SingleOrDefault(m => m.Name == nameof(ExecutionObserverProxy.SetObserver));
            _injProxyCount =
                _injProxyType.Methods.SingleOrDefault(m => m.Name == nameof(ExecutionObserverProxy.Count));

            foreach (var typ in module.Types)
            {
                CheckCallsFromTypes(errors, typ);
            }
            
            return errors;
        }

        private void CheckObserverProxyIsNotTampered(List<ValidationResult> errors)
        {
            if (!_injProxyType.HasSameFields(CounterProxyTypeRef))
            {
                errors.Add(new ObserverProxyValidationResult(_injProxyType.Name + " type has different fields."));
            }

            foreach (var refMethod in CounterProxyTypeRef.Methods)
            {
                var injMethod = _injProxyType.Methods.SingleOrDefault(m => m.Name == refMethod.Name);

                if (injMethod == null)
                {
                    errors.Add(new ObserverProxyValidationResult(refMethod.Name + " is not implemented in observer proxy."));
                }
                
                #if UNIT_TEST
                refMethod.RemoveCoverLetInjectedInstructions();
                #endif

                if (!injMethod.HasSameBody(refMethod))
                {
                    var contractMethodBody = string.Join("\n", injMethod?.Body.Instructions.Select(i => i.ToString()).ToArray());
                    var referenceMethodBody = string.Join("\n", refMethod?.Body.Instructions.Select(i => i.ToString()).ToArray());
                    
                    errors.Add(new ObserverProxyValidationResult( 
                        $"{refMethod.Name} proxy method body is tampered.\n" +
                        $"Injected Contract: \n{contractMethodBody}\n\n" +
                        $"Reference:\n{referenceMethodBody}"));
                }
                
                if (!injMethod.HasSameParameters(refMethod))
                {
                    errors.Add(new ObserverProxyValidationResult(refMethod.Name + " proxy method accepts different parameters."));
                }
            }
            
            if (_injProxyType.Methods.Count != CounterProxyTypeRef.Methods.Count)
                errors.Add(new ObserverProxyValidationResult("Observer type contains unusual number of methods."));
        }

        private void CheckCallsFromTypes(List<ValidationResult> errors, TypeDefinition typ)
        {
            if (typ == _injProxyType) // Do not need to validate calls from the injected proxy
                return;
            
            // Patch the methods in the type
            foreach (var method in typ.Methods)
            {
                CheckCallsFromMethods(errors, method);
            }

            // Patch if there is any nested type within the type
            foreach (var nestedType in typ.NestedTypes)
            {
                CheckCallsFromTypes(errors, nestedType);
            }
        }

        private void CheckCallsFromMethods(List<ValidationResult> errors, MethodDefinition method)
        {
            if (!method.HasBody)
                return;

            // Should be a call placed before each branching opcode
            foreach (var instruction in method.Body.Instructions)
            {
                if (Consts.JumpingOps.Contains(instruction.OpCode))
                {
                    var proxyCallInstruction = instruction.Previous; // Previous instruction should be proxy call

                    if (!(proxyCallInstruction.OpCode == OpCodes.Call && proxyCallInstruction.Operand == _injProxyCount))
                    {
                        errors.Add(new ObserverProxyValidationResult($"Missing execution observer call detected. " +
                                                                     $"[{method.DeclaringType.Name} > {method.Name}]"));
                    }                    
                }

                // Calling SetObserver method within contract is a breach
                if (instruction.OpCode == OpCodes.Call && instruction.Operand == _injProxySetObserver)
                {
                    errors.Add(new ObserverProxyValidationResult($"Proxy initialize call detected from within the contract. " +
                                                                 $"[{method.DeclaringType.Name} > {method.Name}]"));
                }
            }
        }
    }
    
    public class ObserverProxyValidationResult : ValidationResult
    {
        public ObserverProxyValidationResult(string message) : base(message)
        {
        }
    }
}