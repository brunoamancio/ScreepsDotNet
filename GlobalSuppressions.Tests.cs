// This file is used by Code Analysis to maintain SuppressMessage attributes
// that apply to all test assemblies linking this file.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Naming",
                           "CA1707:Identifiers should not contain underscores",
                           Scope = "module",
                           Justification = "Test names use underscores for readability and expressive intent.")]
[assembly: SuppressMessage("Naming",
                           "IDE1006:Naming Styles",
                           Scope = "module",
                           Justification = "Tests follow BDD-style names and async suffix is not required.")]
