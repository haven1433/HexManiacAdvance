using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace HavenSoft.AutoImplement.Tool {
   /// <summary>
   /// Writing a Stub for a Class (as opposed to an interface) requires actually writing two types.
   /// One type will hold the new members that use the same names as the virtual, abstract, and overrides.
   /// The new members will allow for the implementation to be changed out on the fly.
   /// The other type will contain overrides for the class members that are virtual, abstract, or override.
   /// The second type will call the members of the first.
   /// The separation is needed because C# won't allow two members in the same type to have the same name.
   /// </summary>
   public class ClassStubBuilder : IPatternBuilder {

      private readonly List<string> implementedMethods = new List<string>();
      private readonly CSharpSourceWriter stubWriter, intermediateWriter;
      private IDisposable intermediateClassScope; // Open when the intermediateWriter is in the middle of writing its class.
      private string stubTypeName, genericInfo, constraints;

      public ClassStubBuilder(CSharpSourceWriter writer) {
         // the main writer will write the Stub class, which contains the new members.
         stubWriter = writer;
         writer.WriteUsings(
            "System",                        // Action, Func, Type, IDisposable
            "System.Collections.Generic",    // Dictionary
            "System.Delegation",             // PropertyImplementation, EventImplementation
            "System.Linq",                   // ConstructionCompletion uses Linq to get the correct constructor.
            "System.Runtime.Serialization"); // FormatterServices

         // the intermediateWriter will write the intermediate class, which contains the override members.
         intermediateWriter = new CSharpSourceWriter(writer.Indentation);
      }

      public string GetDesiredOutputFileName(Type type) {
         var (mainName, genericInformation) = type.Name.ExtractImplementationNameParts("`");
         return $"Stub{mainName}{genericInformation}.cs";
      }

      public string ClassDeclaration(Type type) {
         var typeName = type.CreateCsName(type.Namespace);
         string basename;
         (basename, genericInfo) = typeName.ExtractImplementationNameParts("<");
         constraints = MemberMetadata.GetGenericParameterConstraints(type.GetGenericArguments(), type.Namespace);

         intermediateWriter.Write($"public class IntermediateStub{basename}_DoNotUse{genericInfo} : {typeName}{constraints}");
         Debug.Assert(intermediateClassScope == null);
         intermediateClassScope = intermediateWriter.Scope;
         stubTypeName = $"Stub{basename}{genericInfo}";

         return $"{stubTypeName} : IntermediateStub{basename}_DoNotUse{genericInfo}{constraints}";
      }

      public void AppendExtraMembers(Type type) {
         // add in constructors
         var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Concat(type.GetConstructors(BindingFlags.Instance | BindingFlags.Public));

         foreach (var constructor in constructors) {
            var metadata = new MemberMetadata(constructor, type.Namespace);
            AppendConstructor(type, constructor, metadata);
         }

         implementedMethods.Clear();

         // add in fields
         var protectedFields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Where(field => field.IsFamily || field.IsFamilyOrAssembly);
         foreach (var field in protectedFields) {
            var metadata = new MemberMetadata(field, type.Namespace);
            AppendField(type, field, metadata);
         }
      }

      public void AppendMethod(MethodInfo info, MemberMetadata metadata) {
         if (info.IsStatic || info.IsPrivate || info.IsAssembly || info.IsFamilyAndAssembly) return;
         if (!info.IsVirtual && !info.IsAbstract) {
            if (metadata.Access == "protected") {
               // the member is protected. Make a public version.
               stubWriter.Write($"public new {metadata.ReturnType} {metadata.Name}{metadata.GenericParameters}({metadata.ParameterTypesAndNames}){metadata.GenericParameterConstraints}");
               using (stubWriter.Scope) {
                  stubWriter.Write($"{metadata.ReturnClause}base.{metadata.Name}({metadata.ParameterNames});");
               }
            }
            return;
         }
         if (info.IsGenericMethodDefinition) {
            AppendGenericMethod(info, metadata);
            return;
         }

         var delegateName = GetDelegateName(metadata.ReturnType, metadata.ParameterTypes);

         var typesExtension = StubBuilder.SanitizeMethodName(metadata.ParameterTypes);

         var methodsWithMatchingNameButNotSignature = implementedMethods.Where(name => name.Split('(')[0] == metadata.Name && name != $"{metadata.Name}({metadata.ParameterTypes})");
         string localImplementationName = methodsWithMatchingNameButNotSignature.Any() ? $"{metadata.Name}_{typesExtension}" : metadata.Name;

         if (info.GetParameters().Any(p => p.ParameterType.IsByRef)) {
            delegateName = $"{metadata.Name}Delegate_{typesExtension}";
            stubWriter.Write($"public delegate {metadata.ReturnType} {delegateName}({metadata.ParameterTypesAndNames});" + Environment.NewLine);
         }

         stubWriter.Write($"public new {delegateName} {localImplementationName};");

         WriteHelperBaseMethod(info, metadata);
         WriteHelperMethod(info, metadata, stubTypeName, localImplementationName);

         implementedMethods.Add($"{metadata.Name}({metadata.ParameterTypes})");
      }

      public void AppendEvent(EventInfo info, MemberMetadata metadata) {
         var methodInfo = info.AddMethod;
         if (methodInfo.IsStatic || methodInfo.IsPrivate || methodInfo.IsAssembly || methodInfo.IsFamilyAndAssembly) return;
         if (!methodInfo.IsVirtual && !methodInfo.IsAbstract) {
            if (metadata.Access == "protected") {
               // the member is protected. Make a public version.
               stubWriter.Write($"public new event {metadata.Name} {{ add {{ base.{metadata.Name} += value; }} remove {{ base.{metadata.Name} -= value; }} }}");
            }
            return;
         }

         stubWriter.Write($"public new EventImplementation<{metadata.HandlerArgsType}> {metadata.Name};");

         if (methodInfo.IsVirtual && !methodInfo.IsAbstract) {
            intermediateWriter.Write($"public void Base{metadata.Name}Add({metadata.HandlerType} e) {{ base.{metadata.Name} += e; }}");
            intermediateWriter.Write($"public void Base{metadata.Name}Remove({metadata.HandlerType} e) {{ base.{metadata.Name} -= e; }}");
         }

         intermediateWriter.Write($"{metadata.Access} override event {metadata.HandlerType} {metadata.Name}");
         using (intermediateWriter.Scope) {
            intermediateWriter.Write($"add {{ (({stubTypeName})this).{metadata.Name}.add(new EventHandler<{metadata.HandlerArgsType}>(value)); }}");
            intermediateWriter.Write($"remove {{ (({stubTypeName})this).{metadata.Name}.remove(new EventHandler<{metadata.HandlerArgsType}>(value)); }}");
         }
      }

      public void AppendProperty(PropertyInfo info, MemberMetadata metadata) {
         var methodInfo = info.GetMethod ?? info.SetMethod;
         if (methodInfo.IsStatic || methodInfo.IsPrivate || methodInfo.IsAssembly || methodInfo.IsFamilyAndAssembly) return;
         if (!methodInfo.IsVirtual && !methodInfo.IsAbstract) {
            if (metadata.Access == "protected") {
               // the member is protected. Make a public version.
               intermediateWriter.Write($"public new {metadata.ReturnType} {metadata.Name}");
               using (intermediateWriter.Scope) {
                  if (info.CanRead) intermediateWriter.Write($"get {{ return base.{metadata.Name}; }}");
                  if (CanWrite(info)) intermediateWriter.Write($"set {{ base.{metadata.Name} = value; }}");
               }
            }
            return;
         }

         stubWriter.Write($"public new PropertyImplementation<{metadata.ReturnType}> {metadata.Name};");

         if (methodInfo.IsVirtual && !methodInfo.IsAbstract) {
            intermediateWriter.Write($"public {metadata.ReturnType} Base{metadata.Name}");
            using (intermediateWriter.Scope) {
               if (info.CanRead) intermediateWriter.Write($"get {{ return base.{metadata.Name}; }}");
               if (CanWrite(info)) intermediateWriter.Write($"set {{ base.{metadata.Name} = value; }}");
            }
         }

         intermediateWriter.Write($"{metadata.Access} override {metadata.ReturnType} {metadata.Name}");
         using (intermediateWriter.Scope) {
            if (info.CanRead) intermediateWriter.Write($"get {{ return (({stubTypeName})this).{metadata.Name}.get(); }}");
            if (CanWrite(info)) {
               var setAccess = DeduceSetAccess(info);
               intermediateWriter.Write($"{setAccess}set {{ (({stubTypeName})this).{metadata.Name}.set(value); }}");
            }
         }
      }

      public void AppendItemProperty(PropertyInfo info, MemberMetadata metadata) {
         var methodInfo = info.GetMethod ?? info.SetMethod;
         if (methodInfo.IsStatic || methodInfo.IsPrivate || methodInfo.IsAssembly || methodInfo.IsFamilyAndAssembly) return;
         if (!methodInfo.IsVirtual && !methodInfo.IsAbstract) {
            // the member is protected. Make a public version.
            if (metadata.Access == "protected") {
               intermediateWriter.Write($"public new {metadata.ReturnType} this[{metadata.ParameterTypesAndNames}]");
               using (intermediateWriter.Scope) {
                  if (info.CanRead) intermediateWriter.Write($"get {{ return base[{metadata.ParameterNames}]; }}");
                  if (info.CanWrite) intermediateWriter.Write($"set {{ base[{metadata.ParameterNames}] = value; }}");
               }
            }
            return;
         }

         if (info.CanRead) stubWriter.Write($"public new Func<{metadata.ParameterTypes}, {metadata.ReturnType}> get_Item;");
         if (info.CanWrite) stubWriter.Write($"public new Action<{metadata.ParameterTypes}, {metadata.ReturnType}> set_Item;");

         if (methodInfo.IsVirtual && !methodInfo.IsAbstract) {
            if (info.CanRead) {
               intermediateWriter.Write($"public {metadata.ReturnType} Base_get_Item({metadata.ParameterTypesAndNames})");
               using (intermediateWriter.Scope) {
                  intermediateWriter.Write($"return base[{metadata.ParameterNames}];");
               }
            }
            if (info.CanWrite) {
               intermediateWriter.Write($"public void Base_set_Item({metadata.ParameterTypesAndNames}, {metadata.ReturnType} value)");
               using (intermediateWriter.Scope) {
                  intermediateWriter.Write($"base[{metadata.ParameterNames}] = value;");
               }
            }
         }

         intermediateWriter.Write($"{metadata.Access} override {metadata.ReturnType} this[{metadata.ParameterTypesAndNames}]");
         using (intermediateWriter.Scope) {
            if (info.CanRead) intermediateWriter.Write($"get {{ return (({stubTypeName})this).get_Item({metadata.ParameterNames}); }}");
            if (info.CanWrite) intermediateWriter.Write($"set {{ (({stubTypeName})this).set_Item({metadata.ParameterNames}, value); }}");
         }
      }

      public void BuildCompleted() {
         Debug.Assert(intermediateClassScope != null);
         intermediateClassScope.Dispose();
         intermediateClassScope = null;

         stubWriter.Write(intermediateWriter.ToString());
      }

      /// <summary>
      /// Normally, making a stub method on a baseclass requires two overrides with the same name.
      /// An override method, and a new delegate field with the same name.
      /// But for generic methods, a delegate field with the same name won't work.
      /// Instead, we have a generic delegate, a dictionary for overrides, a caller for the base method, an "ImplementMember" method, and an override for the method.
      /// Since all of these have different names, we can put all the overrides in a single class.
      /// So don't put anything in the helper intermediate class.
      /// </summary>
      private void AppendGenericMethod(MethodInfo info, MemberMetadata metadata) {
         var typesExtension = StubBuilder.SanitizeMethodName(metadata.ParameterTypes);
         var typeofList = info.GetGenericArguments().Select(type => $"typeof({type.Name})").Aggregate((a, b) => $"{a}, {b}");
         var createKey = $"var key = new Type[] {{ {typeofList} }};";

         var delegateName = $"{metadata.Name}Delegate_{typesExtension}{metadata.GenericParameters}";
         var dictionary = $"{metadata.Name}Delegates_{typesExtension}";
         var methodName = $"{metadata.Name}{metadata.GenericParameters}";

         stubWriter.Write($"public delegate {metadata.ReturnType} {delegateName}({metadata.ParameterTypesAndNames}){metadata.GenericParameterConstraints};");

         stubWriter.Write($"private readonly Dictionary<Type[], object> {dictionary} = new Dictionary<Type[], object>(new EnumerableEqualityComparer<Type>());");
         stubWriter.Write($"public void Implement{methodName}({delegateName} implementation){metadata.GenericParameterConstraints}");
         using (stubWriter.Scope) {
            stubWriter.Write(createKey);
            stubWriter.Write($"{dictionary}[key] = implementation;");
         }
         if (!info.IsAbstract) {
            stubWriter.Write($"public {metadata.ReturnType} Base{methodName}({metadata.ParameterTypesAndNames}){metadata.GenericParameterConstraints}");
            using (stubWriter.Scope) {
               stubWriter.Write($"{metadata.ReturnClause}base.{methodName}({metadata.ParameterNames});");
            }
         }
         stubWriter.Write($"{metadata.Access} override {metadata.ReturnType} {methodName}({metadata.ParameterTypesAndNames})");
         using (stubWriter.Scope) {
            stubWriter.AssignDefaultValuesToOutParameters(info.DeclaringType.Namespace, info.GetParameters());
            stubWriter.Write(createKey);
            stubWriter.Write("object implementation;");
            stubWriter.Write($"if ({dictionary}.TryGetValue(key, out implementation))");
            using (stubWriter.Scope) {
               stubWriter.Write($"{metadata.ReturnClause}(({delegateName})implementation).Invoke({metadata.ParameterNames});");
            }
            stubWriter.Write("else");
            using (stubWriter.Scope) {
               if (!info.IsAbstract) {
                  stubWriter.Write($"{metadata.ReturnClause}Base{methodName}({metadata.ParameterNames});");
               } else if (metadata.ReturnType != "void") {
                  stubWriter.Write($"return default({metadata.ReturnType});");
               }
            }
         }
      }

      private void WriteHelperBaseMethod(MethodInfo info, MemberMetadata metadata) {
         if (info.IsVirtual && !info.IsAbstract) {
            intermediateWriter.Write($"public {metadata.ReturnType} Base{metadata.Name}{metadata.GenericParameterConstraints}({metadata.ParameterTypesAndNames})");
            using (intermediateWriter.Scope) {
               intermediateWriter.Write($"{metadata.ReturnClause}base.{metadata.Name}({metadata.ParameterNames});");
            }
         }
      }

      private void WriteHelperMethod(MethodInfo info, MemberMetadata metadata, string stubTypeName, string localImplementationName) {
         intermediateWriter.Write($"{metadata.Access} override {metadata.ReturnType} {metadata.Name}({metadata.ParameterTypesAndNames}){metadata.GenericParameterConstraints}");
         using (intermediateWriter.Scope) {
            var call = $"(({stubTypeName})this).{localImplementationName}";
            intermediateWriter.AssignDefaultValuesToOutParameters(info.DeclaringType.Namespace, info.GetParameters());
            intermediateWriter.Write($"if ({call} != null)");
            using (intermediateWriter.Scope) {
               intermediateWriter.Write($"{metadata.ReturnClause}{call}({metadata.ParameterNames});");
            }
            if (metadata.ReturnType != "void") {
               intermediateWriter.Write("else");
               using (intermediateWriter.Scope) {
                  intermediateWriter.Write($"return default({metadata.ReturnType});");
               }
            }
         }
      }

      private void AppendConstructor(Type type, ConstructorInfo info, MemberMetadata constructorMetadata) {
         if (info.IsPrivate || info.IsStatic || info.IsFamilyAndAssembly || info.IsAssembly) return;
         var typeName = type.CreateCsName(type.Namespace);
         var (basename, genericInfo) = typeName.ExtractImplementationNameParts("<");
         var intermediateName = $"IntermediateStub{basename}_DoNotUse";
         var stubName = $"Stub{basename}";

         // for every constructor, make both a constructor and a static "DeferConstruction" method
         var deferWriter = new CSharpSourceWriter(stubWriter.Indentation);

         stubWriter.Write($"public {stubName}({constructorMetadata.ParameterTypesAndNames}) : base({constructorMetadata.ParameterNames})");
         var outParam = $"out {stubTypeName} uninitializedStub";
         var separator = constructorMetadata.ParameterTypesAndNames.Length > 0 ? ", " : string.Empty;
         deferWriter.Write($"public static IDisposable DeferConstruction({constructorMetadata.ParameterTypesAndNames}{separator}{outParam})");
         using (stubWriter.Scope) {
            using (deferWriter.Scope) {
               deferWriter.Write($"{stubTypeName} stub;");
               deferWriter.Write($"var disposable = ConstructionCompletion.CreateObjectWithDeferredConstruction<{stubTypeName}>(out stub{separator}{constructorMetadata.ParameterNames});");
               foreach (var member in Program.FindAllMembers(type)) {
                  var metadata = new MemberMetadata(member, type.Namespace);
                  switch (member.MemberType) {
                     case MemberTypes.Method: AppendToConstructorFromMethod((MethodInfo)member, metadata, deferWriter); break;
                     case MemberTypes.Event: AppendToConstructorFromEvent((EventInfo)member, metadata, deferWriter); break;
                     case MemberTypes.Property: AppendToConstructorFromProperty((PropertyInfo)member, metadata, deferWriter); break;
                     default:
                        // the only other options are Field, Type, NestedType, and Constructor
                        // none of those can be virtual/abstract, so we don't need to put anything in the constructor for them.
                        break;
                  }
               }
               deferWriter.Write($"uninitializedStub = stub;");
               deferWriter.Write($"return disposable;");
            }
         }

         stubWriter.Write(deferWriter.ToString());
         intermediateWriter.Write($"protected {intermediateName}({constructorMetadata.ParameterTypesAndNames}) : base({constructorMetadata.ParameterNames}) {{ }}");
      }

      private void AppendToConstructorFromMethod(MethodInfo info, MemberMetadata metadata, CSharpSourceWriter deferWriter) {
         if (info.IsSpecialName || info.IsStatic || info.IsPrivate || !info.IsVirtual || info.IsAbstract || info.IsAssembly || info.IsFamilyAndAssembly || info.IsGenericMethod) return;
         if (info.IsVirtual && info.Name == "Finalize") return; // Finalize is special in C#. Use a destructor instead.

         var typesExtension = StubBuilder.SanitizeMethodName(metadata.ParameterTypes);
         var methodsWithMatchingNameButNotSignature = implementedMethods.Where(name => name.Split('(')[0] == metadata.Name && name != $"{metadata.Name}({metadata.ParameterTypes})");
         string localImplementationName = methodsWithMatchingNameButNotSignature.Any() ? $"{metadata.Name}_{typesExtension}" : metadata.Name;

         stubWriter.Write($"if ({localImplementationName} == null) {localImplementationName} = Base{metadata.Name};");
         deferWriter.Write($"stub.{localImplementationName} = stub.Base{metadata.Name};");

         implementedMethods.Add($"{metadata.Name}({metadata.ParameterTypes})");
      }

      private void AppendToConstructorFromEvent(EventInfo info, MemberMetadata metadata, CSharpSourceWriter deferWriter) {
         var addMethod = info.AddMethod;
         if (addMethod.IsAbstract && !addMethod.IsAssembly && !addMethod.IsFamilyAndAssembly) {
            stubWriter.Write($"if ({metadata.Name} == null) {metadata.Name} = new EventImplementation<{metadata.HandlerArgsType}>();");
            deferWriter.Write($"stub.{metadata.Name} = new EventImplementation<{metadata.HandlerArgsType}>();");
         }
         if (addMethod.IsStatic || addMethod.IsPrivate || !addMethod.IsVirtual || addMethod.IsAbstract || addMethod.IsAssembly || addMethod.IsFamilyAndAssembly) return;

         stubWriter.Write($"if ({metadata.Name} == null)");
         using (stubWriter.Scope) {
            stubWriter.Write($"{metadata.Name} = new EventImplementation<{metadata.HandlerArgsType}>();");
            stubWriter.Write($"{metadata.Name}.add = value => Base{metadata.Name}Add(new {metadata.HandlerType}(value));");
            stubWriter.Write($"{metadata.Name}.remove = value => Base{metadata.Name}Remove(new {metadata.HandlerType}(value));");
         }

         deferWriter.Write($"stub.{metadata.Name} = new EventImplementation<{metadata.HandlerArgsType}>();");
         deferWriter.Write($"stub.{metadata.Name}.add = value => stub.Base{metadata.Name}Add(new {metadata.HandlerType}(value));");
         deferWriter.Write($"stub.{metadata.Name}.remove = value => stub.Base{metadata.Name}Remove(new {metadata.HandlerType}(value));");
      }

      private void AppendToConstructorFromProperty(PropertyInfo info, MemberMetadata metadata, CSharpSourceWriter deferWriter) {
         var method = info.GetMethod ?? info.SetMethod;
         if (method.IsAbstract && !method.IsAssembly && !method.IsFamilyAndAssembly) {
            if (info.Name == "Item" && info.GetIndexParameters().Length > 0) {
               // item property maps to two methods, get_Item and set_Item
               // but since the property is abstract, we have no base implementation
               // so just like methods, give no default values in the constructor (or defer construction call)
            } else {
               stubWriter.Write($"if ({metadata.Name} == null) {metadata.Name} = new PropertyImplementation<{metadata.ReturnType}>();");
               deferWriter.Write($"stub.{metadata.Name} = new PropertyImplementation<{metadata.ReturnType}>();");
            }
         }
         if (method.IsStatic || method.IsPrivate || !method.IsVirtual || method.IsAbstract || method.IsAssembly || method.IsFamilyAndAssembly) return;

         if (info.Name == "Item" && info.GetIndexParameters().Length > 0) {
            if (info.CanRead) stubWriter.Write($"if (get_Item == null) get_Item = Base_get_Item;");
            if (CanWrite(info)) stubWriter.Write($"if (set_Item == null) set_Item = Base_set_Item;");
            if (info.CanRead) deferWriter.Write($"stub.get_Item = stub.Base_get_Item;");
            if (CanWrite(info)) deferWriter.Write($"stub.set_Item = stub.Base_set_Item;");
         } else {
            stubWriter.Write($"if ({metadata.Name} == null)");
            using (stubWriter.Scope) {
               stubWriter.Write($"{metadata.Name} = new PropertyImplementation<{metadata.ReturnType}>();");
               if (info.CanRead) stubWriter.Write($"{metadata.Name}.get = () => Base{metadata.Name};");
               if (CanWrite(info)) stubWriter.Write($"{metadata.Name}.set = value => Base{metadata.Name} = value;");
            }
            deferWriter.Write($"stub.{metadata.Name} = new PropertyImplementation<{metadata.ReturnType}>();");
            if (info.CanRead) deferWriter.Write($"stub.{metadata.Name}.get = () => stub.Base{metadata.Name};");
            if (CanWrite(info)) deferWriter.Write($"stub.{metadata.Name}.set = value => stub.Base{metadata.Name} = value;");
         }
      }

      private void AppendField(Type type, FieldInfo info, MemberMetadata metadata) {
         stubWriter.Write($"public new {metadata.ReturnType} {metadata.Name}");
         using (stubWriter.Scope) {
            stubWriter.Write($"get {{ return base.{metadata.Name}; }}");
            stubWriter.Write($"set {{ base.{metadata.Name} = value; }}");
         }
      }

      /// <summary>
      /// the access will end in whitespace or be empty.
      /// This is to prevent a single space from occurring if the accesses match.
      /// </summary>
      private static string DeduceSetAccess(PropertyInfo info) {
         if (!info.CanRead || !info.CanWrite) return string.Empty;
         if (info.GetMethod.IsPublic && info.SetMethod.IsPublic) return string.Empty;
         if (info.GetMethod.IsFamily && info.SetMethod.IsFamily) return string.Empty;
         if (info.GetMethod.IsFamilyOrAssembly && info.SetMethod.IsFamilyOrAssembly) return string.Empty;

         if (info.SetMethod.IsPublic) return "public ";
         if (info.SetMethod.IsFamily) return "protected ";
         if (info.SetMethod.IsFamilyOrAssembly) return "protected ";

         throw new NotImplementedException();
      }

      /// <summary>
      /// We're interested in whether or not a subclass in another assembly should provide an overload.
      /// PropertyInfo.CanWrite only tells us if there's a setter, not if we can overwrite it.
      /// </summary>
      private static bool CanWrite(PropertyInfo info) {
         if (!info.CanWrite) return false;
         if (info.SetMethod.IsPrivate) return false;
         if (info.SetMethod.IsAssembly) return false;
         if (info.SetMethod.IsFamilyAndAssembly) return false;
         return true;
      }

      private string GetDelegateName(string returnType, string parameterTypes) {
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
   }
}
