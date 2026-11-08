using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Architecture.Tests;

internal static class ArchitectureRules
{
    private const string ModuleInterface = "Tools.Modularity.IModule";

    public static string[] Check(IEnumerable<AssemblyDefinition> assemblies)
    {
        List<string> errors = [];
        AssemblyDefinition[] modules = assemblies.ToArray();
        Dictionary<string, string> roots = new(StringComparer.Ordinal);
        foreach (AssemblyDefinition module in modules)
        {
            TypeDefinition[] implementations = module.MainModule.GetTypes()
                .Where(type => !type.IsAbstract && type.Interfaces.Any(i => i.InterfaceType.FullName == ModuleInterface)).ToArray();
            if (implementations.Length == 0)
            {
                errors.Add($"{module.Name.Name}: missing IModule implementation");
                continue;
            }
            string root = implementations[0].Namespace;
            roots.Add(module.Name.Name, root);
            TypeDefinition[] types = module.MainModule.GetTypes().ToArray();
            foreach (TypeDefinition implementation in implementations.Where(IsVisible))
                errors.Add($"{implementation.FullName}: IModule implementation must not be public");
            if (!types.Any(type => IsEntryPoint(type, root)))
            {
                errors.Add($"{module.Name.Name}: missing public registration entry point returning IModule");
            }
            foreach (TypeDefinition type in types.Where(type => type.IsPublic || type.IsNestedPublic))
            {
                if (IsVisible(type) && !Within(SourceNamespace(type), root + ".PublicApi") && !IsEntryPoint(type, root))
                {
                    errors.Add($"{type.FullName}: public surface outside registration or PublicApi");
                }
                if (IsEntryPoint(type, root))
                {
                    foreach (MethodDefinition method in type.Methods.Where(method => method.IsPublic && method.ReturnType.FullName != ModuleInterface))
                        errors.Add($"{type.FullName}::{method.Name}: public surface outside registration");
                    foreach (FieldDefinition field in type.Fields.Where(field => field.IsPublic))
                        errors.Add($"{type.FullName}::{field.Name}: public surface outside registration");
                }
            }
        }

        foreach (AssemblyDefinition module in modules.Where(m => roots.ContainsKey(m.Name.Name)))
        {
            string root = roots[module.Name.Name];
            foreach (TypeDefinition source in module.MainModule.GetTypes())
            {
                string sourceNamespace = SourceNamespace(source);
                foreach (TypeReference target in Dependencies(source).DistinctBy(t => t.FullName))
                {
                    string targetName = target.FullName;
                    string targetNamespace = ReferenceNamespace(target);
                    if (Within(sourceNamespace, root + ".Domain") &&
                        ((Within(targetNamespace, root) && !Within(targetNamespace, root + ".Domain")) ||
                         Within(targetNamespace, "Microsoft.EntityFrameworkCore") || Within(targetNamespace, "Microsoft.AspNetCore")))
                    {
                        errors.Add($"{source.FullName}: Domain depends on {targetName}");
                    }
                    if (Within(sourceNamespace, root + ".Application") &&
                        (Within(targetNamespace, root + ".Infrastructure") || Within(targetNamespace, root + ".Presentation")))
                    {
                        errors.Add($"{source.FullName}: Application depends on {targetName}");
                    }
                    foreach ((string name, string otherRoot) in roots.Where(pair => pair.Key != module.Name.Name))
                    {
                        AssemblyDefinition other = modules.First(item => item.Name.Name == name);
                        TypeDefinition? definition = other.MainModule.GetTypes().FirstOrDefault(type => type.FullName == targetName);
                        if (target.Scope?.Name == name &&
                            (!Within(targetNamespace, otherRoot + ".PublicApi") || definition is null || !IsVisible(definition)))
                        {
                            errors.Add($"{source.FullName}: cross-module dependency on {targetName}");
                        }
                    }
                }
            }
        }
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool Within(string value, string prefix) => value == prefix || value.StartsWith(prefix + ".", StringComparison.Ordinal);

    private static bool IsEntryPoint(TypeDefinition type, string root) =>
        type.IsPublic && type.Namespace == root && type.IsAbstract && type.IsSealed &&
        type.Methods.Any(method => method.IsPublic && method.IsStatic && method.ReturnType.FullName == ModuleInterface);

    private static bool IsVisible(TypeDefinition type) => type.IsNested ? type.IsNestedPublic && IsVisible(type.DeclaringType) : type.IsPublic;

    private static string SourceNamespace(TypeDefinition type) => type.IsNested ? SourceNamespace(type.DeclaringType) : type.Namespace;

    private static string ReferenceNamespace(TypeReference type) => type.IsNested ? ReferenceNamespace(type.DeclaringType) : type.Namespace;

    private static IEnumerable<TypeReference> Dependencies(TypeDefinition type)
    {
        foreach (TypeReference dependency in Attributes(type.CustomAttributes)) yield return dependency;
        foreach (TypeReference dependency in Constraints(type.GenericParameters)) yield return dependency;
        if (type.BaseType is not null) foreach (TypeReference dependency in Expand(type.BaseType)) yield return dependency;
        foreach (InterfaceImplementation item in type.Interfaces)
            foreach (TypeReference dependency in Expand(item.InterfaceType)) yield return dependency;
        foreach (FieldDefinition field in type.Fields)
        {
            foreach (TypeReference dependency in Expand(field.FieldType)) yield return dependency;
            foreach (TypeReference dependency in Attributes(field.CustomAttributes)) yield return dependency;
        }
        foreach (PropertyDefinition property in type.Properties)
        {
            foreach (TypeReference dependency in Expand(property.PropertyType)) yield return dependency;
            foreach (TypeReference dependency in Attributes(property.CustomAttributes)) yield return dependency;
        }
        foreach (EventDefinition item in type.Events)
            foreach (TypeReference dependency in Expand(item.EventType)) yield return dependency;
        foreach (MethodDefinition method in type.Methods)
        {
            foreach (TypeReference dependency in Attributes(method.CustomAttributes)) yield return dependency;
            foreach (TypeReference dependency in Constraints(method.GenericParameters)) yield return dependency;
            foreach (TypeReference dependency in Expand(method.ReturnType)) yield return dependency;
            foreach (ParameterDefinition parameter in method.Parameters)
            {
                foreach (TypeReference dependency in Expand(parameter.ParameterType)) yield return dependency;
                foreach (TypeReference dependency in Attributes(parameter.CustomAttributes)) yield return dependency;
            }
            if (!method.HasBody) continue;
            foreach (VariableDefinition variable in method.Body.Variables)
                foreach (TypeReference dependency in Expand(variable.VariableType)) yield return dependency;
            foreach (ExceptionHandler handler in method.Body.ExceptionHandlers)
                if (handler.CatchType is not null)
                    foreach (TypeReference dependency in Expand(handler.CatchType)) yield return dependency;
            foreach (Instruction instruction in method.Body.Instructions)
            {
                IEnumerable<TypeReference> references = instruction.Operand switch
                {
                    TypeReference reference => Expand(reference),
                    MethodReference reference => MethodTypes(reference),
                    FieldReference reference => Expand(reference.DeclaringType).Concat(Expand(reference.FieldType)),
                    _ => []
                };
                foreach (TypeReference dependency in references) yield return dependency;
            }
        }
    }

    private static IEnumerable<TypeReference> Attributes(IEnumerable<CustomAttribute> attributes)
    {
        foreach (CustomAttribute attribute in attributes)
            foreach (TypeReference type in Expand(attribute.AttributeType)) yield return type;
    }

    private static IEnumerable<TypeReference> Constraints(IEnumerable<GenericParameter> parameters)
    {
        foreach (GenericParameter parameter in parameters)
            foreach (GenericParameterConstraint constraint in parameter.Constraints)
                foreach (TypeReference type in Expand(constraint.ConstraintType)) yield return type;
    }

    private static IEnumerable<TypeReference> MethodTypes(MethodReference method)
    {
        foreach (TypeReference type in Expand(method.DeclaringType)) yield return type;
        foreach (TypeReference type in Expand(method.ReturnType)) yield return type;
        foreach (ParameterDefinition parameter in method.Parameters)
            foreach (TypeReference type in Expand(parameter.ParameterType)) yield return type;
        if (method is GenericInstanceMethod generic)
            foreach (TypeReference argument in generic.GenericArguments)
                foreach (TypeReference type in Expand(argument)) yield return type;
    }

    private static IEnumerable<TypeReference> Expand(TypeReference type)
    {
        if (type is GenericInstanceType generic)
            foreach (TypeReference argument in generic.GenericArguments)
                foreach (TypeReference item in Expand(argument)) yield return item;
        if (type is TypeSpecification specification)
        {
            foreach (TypeReference item in Expand(specification.ElementType)) yield return item;
        }
        else if (type is not GenericParameter)
        {
            yield return type;
        }
    }
}
