namespace Csag.Blueprint.SourceGenerators.UnitTests;

using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

/// <summary>
/// Driver-based tests for <see cref="TranslationKeysGenerator"/>: each test feeds a compilation
/// containing a <c>TranslationDefaults</c> class through a <see cref="CSharpGeneratorDriver"/> and
/// asserts on the generated sources, their compilability, and their runtime behaviour.
/// </summary>
public sealed class TranslationKeysGeneratorTests
{
    private const string HappyPathSource = """
        namespace TestApp.Translations;

        public static partial class TranslationDefaults
        {
            public const string AppName = "Blueprint";

            public static class Validation
            {
                public const string EmailRequired = "Email is required";
                public const string PasswordRequired = "Password is required";
            }

            public static class Common
            {
                public const string Cancel = "Cancel";

                public static class ErrorBoundary
                {
                    public const string Title = "Whoops!";
                }
            }
        }
        """;

    private const string NoTranslationDefaultsSource = """
        namespace TestApp.Translations;

        public static class SomethingElse
        {
            public const string Key = "value";
        }
        """;

    private const string EmptyTranslationDefaultsSource = """
        namespace TestApp.Translations;

        public static class TranslationDefaults
        {
        }
        """;

    private const string OnlyIneligibleMembersSource = """
        namespace TestApp.Translations;

        public static class TranslationDefaults
        {
            public const int Number = 42;
        }
        """;

    private const string IneligibleMembersSource = """
        namespace TestApp.Translations;

        public static partial class TranslationDefaults
        {
            public const string Kept = "kept";
            public const int AnswerNumber = 42;
            public const string Absent = null;
            public static readonly string NotConstant = "not constant";
            public static string ComputedValue => "computed";

            public class NonStaticNested
            {
                public const string Skipped = "skipped";
            }
        }
        """;

    private const string PartialDeclarationsSource = """
        namespace TestApp.Translations;

        public static partial class TranslationDefaults
        {
            public static class First
            {
                public const string Alpha = "A";
            }
        }

        public static partial class TranslationDefaults
        {
            public static class Second
            {
                public const string Beta = "B";
            }
        }
        """;

    private const string DuplicateKeyAcrossNamespacesSource = """
        namespace First
        {
            public static partial class TranslationDefaults
            {
                public const string Greeting = "Hello";
            }
        }

        namespace Second
        {
            public static partial class TranslationDefaults
            {
                public const string Greeting = "Goodbye";
            }
        }
        """;

    private const string EscapingSource = """
        namespace TestApp.Translations;

        public static partial class TranslationDefaults
        {
            public const string Tricky = "He said \"hi\"\nTab\there\\end\r";
        }
        """;

    private const string UnsortedKeysSource = """
        namespace TestApp.Translations;

        public static partial class TranslationDefaults
        {
            public const string Zebra = "Z";
            public const string Ärger = "trouble";
            public const string Apple = "A";

            public static class Middle
            {
                public const string Item = "M";
            }
        }
        """;

    private const string GlobalNamespaceSource = """
        public static partial class TranslationDefaults
        {
            public const string Key = "value";
        }
        """;

    private const string NonPartialClassSource = """
        namespace TestApp.Translations;

        public static class TranslationDefaults
        {
            public const string Key = "value";
        }
        """;

    private static readonly ImmutableArray<MetadataReference> MetadataReferences = CreateMetadataReferences();

    [Fact]
    public void RunGenerator_WithTranslationDefaultsClass_GeneratesThreeCompilableSources()
    {
        // Act
        var (runResult, outputCompilation, generatorDiagnostics) = RunGenerator(HappyPathSource);

        // Assert — three well-known hint names, no generator diagnostics, and the output compiles cleanly.
        generatorDiagnostics.ShouldBeEmpty();
        var result = runResult.Results.ShouldHaveSingleItem();
        result.Exception.ShouldBeNull();
        result.GeneratedSources.Select(s => s.HintName).ShouldBe(
            ["TranslationDefaults.g.cs", "TranslationKeys.g.cs", "TranslationValues.g.cs"],
            ignoreOrder: true);
        outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .ShouldBeEmpty();
    }

    [Fact]
    public void RunGenerator_WithTranslationDefaultsClass_GeneratesDefaultsDictionary()
    {
        // Act
        var (runResult, _, _) = RunGenerator(HappyPathSource);

        // Assert — the registry lives in the declaring namespace and maps dot-separated keys to defaults.
        var defaults = GetGeneratedText(runResult, "TranslationDefaults.g.cs");
        defaults.ShouldContain("namespace TestApp.Translations;");
        defaults.ShouldContain("public static partial class TranslationDefaults");
        defaults.ShouldContain("""["AppName"] = "Blueprint",""");
        defaults.ShouldContain("""["Validation.EmailRequired"] = "Email is required",""");
        defaults.ShouldContain("""["Validation.PasswordRequired"] = "Password is required",""");
        defaults.ShouldContain("""["Common.Cancel"] = "Cancel",""");
        defaults.ShouldContain("""["Common.ErrorBoundary.Title"] = "Whoops!",""");
        CountOccurrences(defaults, "[\"").ShouldBe(5);
    }

    [Fact]
    public void RunGenerator_WithNestedClasses_GeneratesHierarchicalKeyPathConstants()
    {
        // Act
        var (runResult, _, _) = RunGenerator(HappyPathSource);

        // Assert — TranslationKeys mirrors the class hierarchy; each constant holds the full key path.
        var keys = GetGeneratedText(runResult, "TranslationKeys.g.cs");
        keys.ShouldContain("public static class TranslationKeys");
        keys.ShouldContain("""public const string AppName = "AppName";""");
        keys.ShouldContain("public static class Validation");
        keys.ShouldContain("""public const string EmailRequired = "Validation.EmailRequired";""");
        keys.ShouldContain("public static class ErrorBoundary");
        keys.ShouldContain("""public const string Title = "Common.ErrorBoundary.Title";""");
    }

    [Fact]
    public void RunGenerator_WithNestedClasses_GeneratesTranslationValuesDto()
    {
        // Act
        var (runResult, _, _) = RunGenerator(HappyPathSource);

        // Assert — the DTO mirrors the hierarchy with camel-cased JSON names and a FromDictionary factory.
        var values = GetGeneratedText(runResult, "TranslationValues.g.cs");
        values.ShouldContain("public sealed class TranslationValues");
        values.ShouldContain("public ValidationValues Validation { get; set; } = new ValidationValues();");
        values.ShouldContain("""[System.Text.Json.Serialization.JsonPropertyName("validation")]""", Case.Sensitive);
        values.ShouldContain("""[System.Text.Json.Serialization.JsonPropertyName("emailRequired")]""", Case.Sensitive);
        values.ShouldContain("public sealed class ValidationValues");
        values.ShouldContain("public sealed class ErrorBoundaryValues");
        values.ShouldContain("public string EmailRequired { get; set; } = string.Empty;");
        values.ShouldContain("""if (translations.TryGetValue("Validation.EmailRequired", out var """);
        values.ShouldContain("result.Common.ErrorBoundary.Title = ");
    }

    [Fact]
    public void RunGenerator_GeneratedSources_CompileAndBehaveAtRuntime()
    {
        // Arrange & Act — emit the output compilation and load it, then exercise the generated API.
        var (_, outputCompilation, _) = RunGenerator(HappyPathSource);
        var assembly = EmitAndLoad(outputCompilation);

        // Assert — the defaults registry contains every key with its English text.
        var defaultsType = assembly.GetType("TestApp.Translations.TranslationDefaults").ShouldNotBeNull();
        var all = (IReadOnlyDictionary<string, string>)defaultsType.GetProperty("All").ShouldNotBeNull().GetValue(null)!;
        all.Count.ShouldBe(5);
        all["AppName"].ShouldBe("Blueprint");
        all["Validation.EmailRequired"].ShouldBe("Email is required");
        all["Common.ErrorBoundary.Title"].ShouldBe("Whoops!");

        // The key-path constants hold the dot-separated lookup keys.
        var rootKeysType = assembly.GetType("TestApp.Translations.TranslationKeys").ShouldNotBeNull();
        rootKeysType.GetField("AppName").ShouldNotBeNull().GetRawConstantValue().ShouldBe("AppName");
        var validationKeysType = assembly.GetType("TestApp.Translations.TranslationKeys+Validation").ShouldNotBeNull();
        validationKeysType.GetField("EmailRequired").ShouldNotBeNull().GetRawConstantValue().ShouldBe("Validation.EmailRequired");
        var errorBoundaryKeysType = assembly.GetType("TestApp.Translations.TranslationKeys+Common+ErrorBoundary").ShouldNotBeNull();
        errorBoundaryKeysType.GetField("Title").ShouldNotBeNull().GetRawConstantValue().ShouldBe("Common.ErrorBoundary.Title");

        // FromDictionary populates matching leaves and leaves missing ones at string.Empty.
        var valuesType = assembly.GetType("TestApp.Translations.TranslationValues").ShouldNotBeNull();
        var translations = new Dictionary<string, string>
        {
            ["Validation.EmailRequired"] = "E-Mail ist erforderlich",
            ["Common.ErrorBoundary.Title"] = "Hoppla!",
        };
        var values = valuesType.GetMethod("FromDictionary").ShouldNotBeNull().Invoke(null, [translations]).ShouldNotBeNull();
        var validation = GetPropertyValue(values, "Validation");
        GetPropertyValue(validation, "EmailRequired").ShouldBe("E-Mail ist erforderlich");
        GetPropertyValue(validation, "PasswordRequired").ShouldBe(string.Empty);
        var common = GetPropertyValue(values, "Common");
        GetPropertyValue(GetPropertyValue(common, "ErrorBoundary"), "Title").ShouldBe("Hoppla!");
        GetPropertyValue(values, "AppName").ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData(NoTranslationDefaultsSource)]
    [InlineData(EmptyTranslationDefaultsSource)]
    [InlineData(OnlyIneligibleMembersSource)]
    public void RunGenerator_WithoutEligibleTranslationEntries_GeneratesNothing(string source)
    {
        // Act
        var (runResult, _, generatorDiagnostics) = RunGenerator(source);

        // Assert — no TranslationDefaults class (or no string constants in it) means no output at all.
        runResult.GeneratedTrees.ShouldBeEmpty();
        generatorDiagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void RunGenerator_WithIneligibleMembers_SkipsThem()
    {
        // Act
        var (runResult, _, _) = RunGenerator(IneligibleMembersSource);

        // Assert — only non-null string constants count; non-string constants, non-const fields,
        // properties, and members of non-static nested classes are all ignored.
        var defaults = GetGeneratedText(runResult, "TranslationDefaults.g.cs");
        defaults.ShouldContain("""["Kept"] = "kept",""");
        defaults.ShouldNotContain("AnswerNumber");
        defaults.ShouldNotContain("Absent");
        defaults.ShouldNotContain("NotConstant");
        defaults.ShouldNotContain("ComputedValue");
        defaults.ShouldNotContain("Skipped");
        CountOccurrences(defaults, "[\"").ShouldBe(1);
    }

    [Fact]
    public void RunGenerator_WithPartialDeclarations_DeduplicatesEntries()
    {
        // Act — each partial declaration resolves to the same symbol, so every entry is collected
        // once per declaration; the generator must fold them back into a single entry per key.
        var (runResult, _, generatorDiagnostics) = RunGenerator(PartialDeclarationsSource);

        // Assert — partial declarations of one class are not "multiple classes", so no diagnostic.
        generatorDiagnostics.ShouldBeEmpty();
        var defaults = GetGeneratedText(runResult, "TranslationDefaults.g.cs");
        CountOccurrences(defaults, """["First.Alpha"]""").ShouldBe(1);
        CountOccurrences(defaults, """["Second.Beta"]""").ShouldBe(1);
    }

    [Fact]
    public void RunGenerator_WithSameKeyInMultipleClasses_FirstClassWinsAndReportsWarning()
    {
        // Act — two unrelated TranslationDefaults classes in different namespaces produce the same key.
        var (runResult, _, generatorDiagnostics) = RunGenerator(DuplicateKeyAcrossNamespacesSource);

        // Assert — the entry from the class that appears first in source order wins, and the output
        // is emitted into that class's namespace.
        var defaults = GetGeneratedText(runResult, "TranslationDefaults.g.cs");
        defaults.ShouldContain("namespace First;");
        defaults.ShouldContain("""["Greeting"] = "Hello",""");
        defaults.ShouldNotContain("Goodbye");
        CountOccurrences(defaults, """["Greeting"]""").ShouldBe(1);

        // The merge is surfaced as a warning that names both classes and carries both locations.
        var diagnostic = generatorDiagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("CSAGGEN002");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        message.ShouldContain("First.TranslationDefaults");
        message.ShouldContain("Second.TranslationDefaults");
        diagnostic.Location.ShouldNotBe(Location.None);
        diagnostic.AdditionalLocations.ShouldHaveSingleItem();
    }

    [Fact]
    public void RunGenerator_WithSpecialCharactersInValues_EscapesThemAndRoundTrips()
    {
        // Act
        var (runResult, outputCompilation, _) = RunGenerator(EscapingSource);

        // Assert — quotes, backslashes, and control characters are escaped in the generated literal...
        var defaults = GetGeneratedText(runResult, "TranslationDefaults.g.cs");
        defaults.ShouldContain("""["Tricky"] = "He said \"hi\"\nTab\there\\end\r",""");

        // ...and the compiled constant round-trips to the original value.
        var assembly = EmitAndLoad(outputCompilation);
        var defaultsType = assembly.GetType("TestApp.Translations.TranslationDefaults").ShouldNotBeNull();
        var all = (IReadOnlyDictionary<string, string>)defaultsType.GetProperty("All").ShouldNotBeNull().GetValue(null)!;
        all["Tricky"].ShouldBe("He said \"hi\"\nTab\there\\end\r");
    }

    [Fact]
    public void RunGenerator_WithUnsortedInput_OrdersDictionaryEntriesByOrdinalKey()
    {
        // Act
        var (runResult, _, _) = RunGenerator(UnsortedKeysSource);

        // Assert — entries are emitted sorted by full key using ordinal comparison, not in
        // declaration order: "Ärger" (U+00C4) sorts after every ASCII key, whereas a
        // culture-sensitive comparison would place it right after "Apple".
        var defaults = GetGeneratedText(runResult, "TranslationDefaults.g.cs");
        var appleIndex = defaults.IndexOf("""["Apple"]""", StringComparison.Ordinal);
        var middleIndex = defaults.IndexOf("""["Middle.Item"]""", StringComparison.Ordinal);
        var zebraIndex = defaults.IndexOf("""["Zebra"]""", StringComparison.Ordinal);
        var aergerIndex = defaults.IndexOf("""["Ärger"]""", StringComparison.Ordinal);
        appleIndex.ShouldBeGreaterThanOrEqualTo(0);
        middleIndex.ShouldBeGreaterThan(appleIndex);
        zebraIndex.ShouldBeGreaterThan(middleIndex);
        aergerIndex.ShouldBeGreaterThan(zebraIndex);
    }

    [Fact]
    public void RunGenerator_RunTwiceWithUnchangedInput_ProducesIdenticalOutput()
    {
        // Arrange
        var compilation = CreateCompilation(HappyPathSource);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new TranslationKeysGenerator());

        // Act — re-run the same driver (exercising the incremental cache) and a fresh driver.
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var firstRun = CaptureGeneratedSources(driver);
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        var secondRun = CaptureGeneratedSources(driver);
        var freshDriver = CSharpGeneratorDriver.Create(new TranslationKeysGenerator())
            .RunGenerators(compilation, TestContext.Current.CancellationToken);
        var freshRun = CaptureGeneratedSources(freshDriver);

        // Assert
        firstRun.Count.ShouldBe(3);
        secondRun.ShouldBe(firstRun);
        freshRun.ShouldBe(firstRun);
    }

    [Fact]
    public void RunGenerator_WithClassInGlobalNamespace_EmitsCompilableSourcesWithoutNamespaceDeclaration()
    {
        // Act — a TranslationDefaults class declared outside any namespace.
        var (runResult, outputCompilation, generatorDiagnostics) = RunGenerator(GlobalNamespaceSource);

        // Assert — the generated sources omit the namespace declaration entirely so the types land
        // in the global namespace alongside the input class, and the output compiles cleanly.
        generatorDiagnostics.ShouldBeEmpty();
        runResult.GeneratedTrees.Length.ShouldBe(3);
        var defaults = GetGeneratedText(runResult, "TranslationDefaults.g.cs");
        defaults.ShouldNotContain("namespace");
        outputCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .ShouldBeEmpty();

        var assembly = EmitAndLoad(outputCompilation);
        var defaultsType = assembly.GetType("TranslationDefaults").ShouldNotBeNull();
        var all = (IReadOnlyDictionary<string, string>)defaultsType.GetProperty("All").ShouldNotBeNull().GetValue(null)!;
        all["Key"].ShouldBe("value");
        assembly.GetType("TranslationKeys").ShouldNotBeNull();
        assembly.GetType("TranslationValues").ShouldNotBeNull();
    }

    [Fact]
    public void RunGenerator_WithNonPartialClass_ReportsWarningAndSkipsGeneration()
    {
        // Act — a TranslationDefaults class with translation entries but no partial modifier, which
        // the generated registry (a partial declaration of the same class) could not merge with.
        var (runResult, _, generatorDiagnostics) = RunGenerator(NonPartialClassSource);

        // Assert — the class is skipped entirely and the author is pointed at the missing modifier.
        runResult.GeneratedTrees.ShouldBeEmpty();
        var diagnostic = generatorDiagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("CSAGGEN001");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        var message = diagnostic.GetMessage(CultureInfo.InvariantCulture);
        message.ShouldContain("TestApp.Translations.TranslationDefaults");
        message.ShouldContain("partial");
        diagnostic.Location.ShouldNotBe(Location.None);
    }

    private static (GeneratorDriverRunResult RunResult, Compilation OutputCompilation, ImmutableArray<Diagnostic> GeneratorDiagnostics) RunGenerator(string source)
    {
        var compilation = CreateCompilation(source);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new TranslationKeysGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics,
            TestContext.Current.CancellationToken);
        return (driver.GetRunResult(), outputCompilation, generatorDiagnostics);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken);
        return CSharpCompilation.Create(
            "TranslationsTestAssembly",
            [syntaxTree],
            MetadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    private static ImmutableArray<MetadataReference> CreateMetadataReferences()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        return
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDirectory, "System.Collections.dll")),
            MetadataReference.CreateFromFile(typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute).Assembly.Location),
        ];
    }

    private static string GetGeneratedText(GeneratorDriverRunResult runResult, string hintName)
    {
        var generatedSource = runResult.Results
            .SelectMany(r => r.GeneratedSources)
            .Single(s => s.HintName == hintName);
        return generatedSource.SourceText.ToString();
    }

    private static List<(string HintName, string Text)> CaptureGeneratedSources(GeneratorDriver driver)
    {
        return driver.GetRunResult().Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => (s.HintName, Text: s.SourceText.ToString()))
            .ToList();
    }

    private static int CountOccurrences(string text, string value)
    {
        return text.Split(value, StringSplitOptions.None).Length - 1;
    }

    private static Assembly EmitAndLoad(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        emitResult.Success.ShouldBeTrue(string.Join(Environment.NewLine, emitResult.Diagnostics));
        return Assembly.Load(stream.ToArray());
    }

    private static object GetPropertyValue(object instance, string propertyName)
    {
        return instance.GetType().GetProperty(propertyName).ShouldNotBeNull().GetValue(instance).ShouldNotBeNull();
    }
}
