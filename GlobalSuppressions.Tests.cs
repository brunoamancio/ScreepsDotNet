// This file is used by Code Analysis to maintain SuppressMessage attributes
// that apply to all test assemblies linking this file.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Naming",
                           "CA1707:Identifiers should not contain underscores",
                           Scope = "namespaceanddescendants",
                           Target = "~N:ScreepsDotNet.Backend.Http.Tests",
                           Justification = "Test names use underscores for readability and expressive intent.")]

[assembly: SuppressMessage("Naming",
                           "CA1707:Identifiers should not contain underscores",
                           Scope = "namespaceanddescendants",
                           Target = "~N:ScreepsDotNet.Backend.Cli.Tests",
                           Justification = "Test names use underscores for readability and expressive intent.")]
