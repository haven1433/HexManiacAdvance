using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HavenSoft.AutoImplement.Tool {
   /// <summary>
   /// Automatic Stub implementations make use of C#'s 'explicit interface implementation' feature.
   /// Mutable members with the interface's member's names are added to the type.
   /// Then the interface is implemented with exlicit implementations that point back to those mutable members.
   /// The result is a type that can be changed at whim, but looks identical to the interface it's implementing.
   /// </summary>
   public class StubBuilder : IPatternBuilder {
      private readonly List<string> implementedMethods = new List<string>();
      private readonly List<string> implementedProperties = new List<string>();

      private readonly CSharpSourceWriter writer;

      public StubBuilder(CSharpSourceWriter writer) {
         this.writer = writer;
         writer.WriteUsings(
            "System",                              // Action, Func, Type
            "System.Collections.Generic",          // Dictionary
            "HavenSoft.AutoImplement.Delegation"); // PropertyImplementation, EventImplementation
      }

      /// <summary>
      /// When converting type lists into extensions to put on the end of method names,
      /// we have to sanitize them by removing characters that are illegal in C# member names.
      /// </summary>
      public static string SanitizeMethodName(string name) {
         return name
            .Replace(", ", "_")
            .Replace(">", "_")
            .Replace("<", "_")
            .Replace(".", "_");
      }

      public string GetDesiredOutputFileName(Type interfaceType) {
         var (mainName, genericInformation) = interfaceType.Name.ExtractImplementationNameParts("`");
         return $"Stub{mainName}{genericInformation}.cs";
      }

      public string ClassDeclaration(Type interfaceType) {
         var interfaceName = interfaceType.CreateCsName(interfaceType.Namespace);
         var (basename, genericInfo) = interfaceName.ExtractImplementationNameParts("<");
         var constraints = MemberMetadata.GetGenericParameterConstraints(interfaceType.GetGenericArguments(), interfaceType.Namespace);

         return $"Stub{basename}{genericInfo} : {interfaceName}{constraints}";
      }

      public void AppendExtraMembers(Type interfaceType) { }

      // <example>
      // public Func<int, int, int> Max { get; set; }
      // int ICalculator.Max(int a, int b)
      // {
      //    if (Max != null)
      //    {
      //       return this.Max(a, b);
      //    }
      //    else
      //    {
      //       return default(int);
      //    }
      // }
      // 
      // public Func<double, double, double> Max_double_double { get; set; }
      // double ICalculator.Max(double a, double b)
      // {
      //    if (Max_double_double != null)
      //    {
      //       return this.Max_double_double(a, b);
      //    }
      //    else
      //    {
      //       return default(double);
      //    }
      // }
      // </example>
      /// <remarks>
      /// Methods in interfaces are replaced with delegate properties.
      /// Assigning one of those delegates a value will change the behavior of that method.
      /// You can call the delegate just like the method.
      /// When the interface method is called, it will call the delegate method if possible.
      /// If there is no delegate, it returns default.
      /// </remarks>
      public void AppendMethod(MethodInfo info, MemberMetadata method) {
         if (info.IsGenericMethodDefinition) {
            AppendGenericMethod(info, method);
            return;
         }

         var delegateName = GetStubName(method.ReturnType, method.ParameterTypes);
         var typesExtension = SanitizeMethodName(method.ParameterTypes);

         var methodsWithMatchingNameButNotSignature = implementedMethods.Where(name => name.Split('(')[0] == method.Name && name != $"{method.Name}({method.ParameterTypes})");
         string localImplementationName = methodsWithMatchingNameButNotSignature.Any() ? $"{method.Name}_{typesExtension}" : method.Name;

         if (info.GetParameters().Any(p => p.ParameterType.IsByRef)) {
            localImplementationName = $"{method.Name}_{typesExtension}";
            delegateName = $"{method.Name}Delegate_{typesExtension}";
            writer.Write($"public delegate {method.ReturnType} {delegateName}({method.ParameterTypesAndNames});" + Environment.NewLine);
         }

         // only add a delegation property for the first method with a given signature
         // this is important for IEnumerable<T>.GetEnumerator() and IEnumerable.GetEnumerator() -> same name, same signature
         if (!implementedMethods.Any(name => name == $"{method.Name}({method.ParameterTypes})")) {
            writer.Write($"public {delegateName} {localImplementationName} {{ get; set; }}" + Environment.NewLine);
         }

         ImplementInterfaceMethod(info, localImplementationName, method);
         writer.Write(string.Empty);
         implementedMethods.Add($"{method.Name}({method.ParameterTypes})");
      }

      // <example>
      // public EventImplementation<EventArgs> ValueChanged = new EventImplementation<EventHandler>();
      // 
      // event EventHandler INotifyValueChanged.ValueChanged
      // {
      //    add
      //    {
      //       ValueChanged.add(new EventHandler<EventArgs>(value));
      //    }
      //    remove
      //    {
      //       ValueChanged.remove(new EventHandler<EventArgs>(value));
      //    }
      // }
      // </example>
      // <remarks>
      // Events are replaces with an EventImplementation field.
      // Explicit interface implementations then call that EventImplementation.
      // 
      // EventImplementation exposes add, remove, handlers, and +/- operators along with an Invoke method.
      // This allows you to assign custom add/remove handlers to the Stub, or make decision based on the individual added handlers,
      // or use +=, -=, and .Invoke as if the EventImplementation were actually an event.
      // 
      // Note that the explicit implementation always casts added/removed delegates to EventHandler<T>.
      // This is to avoid having to deal with .net's 2 types of EventHandlers separately.
      // Example: RoutedEventHandler vs EventHandler<RoutedEventArgs>.
      // </remarks>
      public void AppendEvent(EventInfo info, MemberMetadata eventData) {
         writer.Write($"public EventImplementation<{eventData.HandlerArgsType}> {info.Name} = new EventImplementation<{eventData.HandlerArgsType}>();");
         writer.Write(string.Empty);
         writer.Write($"event {eventData.HandlerType} {eventData.DeclaringType}.{info.Name}");
         using (writer.Scope) {
            writer.Write("add");
            using (writer.Scope) {
               writer.Write($"{info.Name}.add(new EventHandler<{eventData.HandlerArgsType}>(value));");
            }
            writer.Write("remove");
            using (writer.Scope) {
               writer.Write($"{info.Name}.remove(new EventHandler<{eventData.HandlerArgsType}>(value));");
            }
         }
      }

      /// <remarks>
      /// Stub properties are similar to Stub events. In both cases, a special type has been created
      /// to help make a public field act like that sort of member.
      /// 
      /// PropertyImplementation provides .get, .set, and .value.
      /// It also provides implicit casting, allowing you to carelessly use the lazy syntax of treating the implementation exactly as the property.
      /// Example: stub.SomeIntProperty = 7;
      /// (as opposed to): stub.SomeIntProperty.value = 7;
      /// 
      /// The explicit implementation just forwards to the public field's get/set members.
      /// </remarks>
      public void AppendProperty(PropertyInfo info, MemberMetadata property) {

         // define the backing field if this property hasn't been implemented yet
         if (!implementedProperties.Contains(property.Name)) {
            writer.Write($"public PropertyImplementation<{property.ReturnType}> {property.Name} = new PropertyImplementation<{property.ReturnType}>();" + Environment.NewLine);
            implementedProperties.Add(property.Name);
         }

         // define the explicit interface implementation
         // this may run multiple times if the same property is defined on multiple interfaces (example, IReadOnlyList and IList)
         writer.Write($"{property.ReturnType} {property.DeclaringType}.{property.Name}");
         using (writer.Scope) {
            if (info.CanRead) {
               writer.Write("get");
               using (writer.Scope) {
                  writer.Write($"return this.{property.Name}.get();");
               }
            }
            if (info.CanWrite) {
               writer.Write("set");
               using (writer.Scope) {
                  writer.Write($"this.{property.Name}.set(value);");
               }
            }
         }
      }

      /// <remarks>
      /// Since Item properties in .net have parameters, the Item property has to be handled specially.
      /// Instead of using a PropertyImplementation object, two separate delegates are exposed, named get_Item and set_Item.
      /// The get and set of the Item property forward to these two public fields.
      /// If no implementation is provided, get_Item will just return default.
      /// </remarks>
      public void AppendItemProperty(PropertyInfo info, MemberMetadata property) {
         // define the backing get/set_Item methods if this property hasn't been implemented yet
         if (!implementedProperties.Contains(property.Name)) {
            if (info.CanRead) {
               writer.Write($"public Func<{property.ParameterTypes}, {property.ReturnType}> get_Item = ({property.ParameterNames}) => default({property.ReturnType});" + Environment.NewLine);
            }

            if (info.CanWrite) {
               writer.Write($"public Action<{property.ParameterTypes}, {property.ReturnType}> set_Item = ({property.ParameterNames}, value) => {{}};" + Environment.NewLine);
            }

            implementedProperties.Add(property.Name);
         }

         // define the explicit interface implementation
         // this may run multiple times if the same property is defined on multiple interfaces (example, IReadOnlyList and IList)
         writer.Write($"{property.ReturnType} {property.DeclaringType}.this[{property.ParameterTypesAndNames}]");
         using (writer.Scope) {
            if (info.CanRead) {
               writer.Write("get");
               using (writer.Scope) {
                  writer.Write($"return get_Item({property.ParameterNames});");
               }
            }
            if (info.CanWrite) {
               writer.Write("set");
               using (writer.Scope) {
                  writer.Write($"set_Item({property.ParameterNames}, value);");
               }
            }
         }
      }

      public void BuildCompleted() { }

      // <example>
      // public delegate void MethodWithGenericInputDelegate_T<T>(T input);
      // private readonly Dictionary<Type[], object> MethodWithGenericInputDelegates_T = new Dictionary<Type[], object>();
      // public void ImplementMethodWithGenericInput<T>(MethodWithGenericInputDelegate_T<T> implementation)
      // {
      //    var key = new Type[] { typeof(T) };
      //    MethodWithGenericInputDelegates[key] = implementation;
      // }
      // public void MethodWithGenericInput<T>(T input)
      // {
      //    var key = new Type[] { typeof(T) };
      //    object implementation;
      //    if (MethodWithGenericInputDelegates.TryGetValue(key, out implementation))
      //    {
      //       ((MethodWithGenericInputDelegate<T>)implementation).Invoke(input);
      //    }
      // }
      //</example>
      private void AppendGenericMethod(MethodInfo info, MemberMetadata method) {
         var typesExtension = SanitizeMethodName(method.ParameterTypes);
         var typeofList = info.GetGenericArguments().Select(type => $"typeof({type.Name})").Aggregate((a, b) => $"{a}, {b}");
         var createKey = $"var key = new Type[] {{ {typeofList} }};";

         var delegateName = $"{method.Name}Delegate_{typesExtension}{method.GenericParameters}";
         var dictionary = $"{method.Name}Delegates_{typesExtension}";
         var methodName = $"{method.Name}{method.GenericParameters}";

         writer.Write($"public delegate {method.ReturnType} {delegateName}({method.ParameterTypesAndNames}){method.GenericParameterConstraints};");
         writer.Write($"private readonly Dictionary<Type[], object> {dictionary} = new Dictionary<Type[], object>(new EnumerableEqualityComparer<Type>());");
         writer.Write($"public void Implement{methodName}({delegateName} implementation){method.GenericParameterConstraints}");
         using (writer.Scope) {
            writer.Write(createKey);
            writer.Write($"{dictionary}[key] = implementation;");
         }
         writer.Write($"public {method.ReturnType} {methodName}({method.ParameterTypesAndNames}){method.GenericParameterConstraints}");
         using (writer.Scope) {
            writer.AssignDefaultValuesToOutParameters(info.DeclaringType.Namespace, info.GetParameters());
            writer.Write(createKey);
            writer.Write("object implementation;");
            writer.Write($"if ({dictionary}.TryGetValue(key, out implementation))");
            using (writer.Scope) {
               writer.Write($"{method.ReturnClause}(({delegateName})implementation).Invoke({method.ParameterNames});");
            }
            if (method.ReturnType != "void") {
               writer.Write("else");
               using (writer.Scope) {
                  writer.Write($"return default({method.ReturnType});");
               }
            }
         }

         writer.Write(string.Empty);
      }

      private string GetStubName(string returnType, string parameterTypes) {
         if (returnType == "void") {
            var delegateName = "Action";
            if (parameterTypes != string.Empty) delegateName += $"<{parameterTypes}>";
            return delegateName;
         } else {
            var delegateName = "Func";
            delegateName += parameterTypes == string.Empty ? $"<{returnType}>" : $"<{parameterTypes}, {returnType}>";
            return delegateName;
         }
      }

      private static string GetDefaultClause(string returnType) {
         return returnType == "void" ? string.Empty : $"default({returnType});";
      }

      private void ImplementInterfaceMethod(MethodInfo info, string localImplementationName, MemberMetadata method) {
         var call = $"this.{localImplementationName}";

         writer.Write($"{method.ReturnType} {method.DeclaringType}.{method.Name}({method.ParameterTypesAndNames})");
         using (writer.Scope) {
            writer.AssignDefaultValuesToOutParameters(info.DeclaringType.Namespace, info.GetParameters());

            writer.Write($"if ({call} != null)");
            using (writer.Scope) {
               writer.Write($"{method.ReturnClause}{call}({method.ParameterNames});");
            }
            if (method.ReturnType != "void") {
               writer.Write("else");
               using (writer.Scope) {
                  writer.Write($"return default({method.ReturnType});");
               }
            }
         }
      }
   }
}
