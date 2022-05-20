using System.Reflection;
using System.Diagnostics.CodeAnalysis;

// general info needed by all assemblies in the solution
[assembly: AssemblyCompany("HavenSoft")]
[assembly: AssemblyProduct("HavenSoft")]
[assembly: AssemblyCopyright("Copyright © HavenSoft 2022")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("0.4.3.7")]
[assembly: AssemblyFileVersion("0.4.3.7")]


// AutoImplement style issues are expected, since it's compatible with earlier versions of C#.
[assembly: SuppressMessage("Style", "IDE0034:Simplify 'default' expression", Justification = "CodeGen", Scope = "module")]
[assembly: SuppressMessage("Style", "IDE1005:Delegate invocation can be simplified.", Justification = "CodeGen", Scope = "module")]

// Suppress rules that decrease code readability.
[assembly: SuppressMessage("Style", "IDE0017:Simplify object initialization", Justification = "Arrange / Act initialization can be separate", Scope = "module")]
