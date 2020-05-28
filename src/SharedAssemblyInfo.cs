using System.Reflection;
using System.Diagnostics.CodeAnalysis;

// general info needed by all assemblies in the solution
[assembly: AssemblyCompany("HavenSoft")]
[assembly: AssemblyProduct("HavenSoft")]
[assembly: AssemblyCopyright("Copyright © HavenSoft 2020")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: AssemblyVersion("0.3.5.1")]
[assembly: AssemblyFileVersion("0.3.5.1")]


// AutoImplement style issues are expected, since it's compatible with earlier versions of C#.
[assembly: SuppressMessage("Style", "IDE0034:Simplify 'default' expression", Justification = "CodeGen", Scope = "module")]
[assembly: SuppressMessage("Style", "IDE1005:Delegate invocation can be simplified.", Justification = "CodeGen", Scope = "module")]
