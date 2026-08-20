using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Csag.Blueprint.SourceGenerators
{
    /// <summary>
    /// Roslyn incremental source generator that scans the <c>TranslationDefaults</c> class hierarchy
    /// and generates a registry of all translation keys mapped to their English default values,
    /// plus a <c>TranslationKeys</c> class with the same structure where each constant holds
    /// the dot-separated key path for direct database lookup.
    /// </summary>
    [Generator]
    public class TranslationKeysGenerator : IIncrementalGenerator
    {
        private const string TranslationKeysClassName = "TranslationDefaults";

        private const string DiagnosticCategory = "CsagBlueprint.SourceGenerators";

        /// <summary>
        /// The generated registry is a partial declaration of the user's <c>TranslationDefaults</c>
        /// class, so a non-partial declaration would make the consuming compilation fail with CS0260.
        /// The generator skips such classes and points the author at the missing modifier instead.
        /// </summary>
        private static readonly DiagnosticDescriptor NonPartialClassDescriptor = new DiagnosticDescriptor(
            id: "CSAGGEN001",
            title: "TranslationDefaults class must be partial",
            messageFormat: "The class '{0}' must be declared partial so the generated translation registry can extend it; translation key generation was skipped for this class",
            category: DiagnosticCategory,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <summary>
        /// Entries from every <c>TranslationDefaults</c> class in the compilation are merged into a
        /// single registry, with the class that appears first in source order winning the target
        /// namespace and any duplicate keys. That merge is easy to trigger accidentally, so it is
        /// surfaced as a warning naming every participating class.
        /// </summary>
        private static readonly DiagnosticDescriptor MultipleClassesDescriptor = new DiagnosticDescriptor(
            id: "CSAGGEN002",
            title: "Multiple TranslationDefaults classes are merged",
            messageFormat: "Multiple TranslationDefaults classes were found ({0}); their entries are merged into a single registry where the first class in source order determines the namespace and wins duplicate keys",
            category: DiagnosticCategory,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        /// <inheritdoc/>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var translationKeysClasses = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => node is ClassDeclarationSyntax cds
                        && cds.Identifier.Text == TranslationKeysClassName,
                    transform: static (ctx, _) => GetTranslationEntries(ctx))
                .Where(static x => x != null)
                .Collect();

            context.RegisterSourceOutput(translationKeysClasses, static (spc, classes) => GenerateSource(spc, classes!));
        }

        private static TranslationKeysInfo? GetTranslationEntries(GeneratorSyntaxContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
            if (symbol == null)
            {
                return null;
            }

            var entries = new List<TranslationEntry>();
            CollectEntries(symbol, string.Empty, entries);

            if (entries.Count == 0)
            {
                return null;
            }

            // The global namespace has no legal declaration syntax, so it is represented as an
            // empty string and the generated sources simply omit the namespace declaration.
            var namespaceName = symbol.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : symbol.ContainingNamespace.ToDisplayString();

            return new TranslationKeysInfo(
                namespaceName,
                symbol.ToDisplayString(),
                classDeclaration.Identifier.GetLocation(),
                classDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword),
                entries);
        }

        private static void CollectEntries(INamedTypeSymbol typeSymbol, string prefix, List<TranslationEntry> entries)
        {
            foreach (var member in typeSymbol.GetMembers())
            {
                if (member is IFieldSymbol field
                    && field.IsConst
                    && field.Type.SpecialType == SpecialType.System_String
                    && field.ConstantValue is string value)
                {
                    var key = string.IsNullOrEmpty(prefix) ? field.Name : prefix + "." + field.Name;
                    entries.Add(new TranslationEntry(key, value, field.Locations.FirstOrDefault()));
                }

                if (member is INamedTypeSymbol nestedType && nestedType.IsStatic)
                {
                    var nestedPrefix = string.IsNullOrEmpty(prefix) ? nestedType.Name : prefix + "." + nestedType.Name;
                    CollectEntries(nestedType, nestedPrefix, entries);
                }
            }
        }

        private static void GenerateSource(SourceProductionContext context, ImmutableArray<TranslationKeysInfo?> classes)
        {
            var allEntries = new Dictionary<string, TranslationEntry>(StringComparer.Ordinal);
            string? namespaceName = null;

            // Partial declarations of the same class all resolve to the same symbol and therefore
            // share a namespace, so tracking one class info per namespace distinguishes genuinely
            // separate TranslationDefaults classes from partial declarations of a single one.
            var distinctClasses = new List<TranslationKeysInfo>();

            foreach (var classInfo in classes)
            {
                if (classInfo == null)
                {
                    continue;
                }

                if (!classInfo.IsPartial)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        NonPartialClassDescriptor,
                        classInfo.ClassLocation,
                        classInfo.ClassDisplayName));
                    continue;
                }

                if (!distinctClasses.Any(c => string.Equals(c.Namespace, classInfo.Namespace, StringComparison.Ordinal)))
                {
                    distinctClasses.Add(classInfo);
                }

                namespaceName ??= classInfo.Namespace;

                foreach (var entry in classInfo.Entries)
                {
                    // Deduplicate entries from multiple partial declarations
                    if (!allEntries.ContainsKey(entry.Key))
                    {
                        allEntries[entry.Key] = entry;
                    }
                }
            }

            if (distinctClasses.Count > 1)
            {
                var classNames = string.Join(", ", distinctClasses.Select(c => "'" + c.ClassDisplayName + "'"));
                context.ReportDiagnostic(Diagnostic.Create(
                    MultipleClassesDescriptor,
                    distinctClasses[0].ClassLocation,
                    distinctClasses.Skip(1).Select(c => c.ClassLocation),
                    classNames));
            }

            if (namespaceName == null || allEntries.Count == 0)
            {
                return;
            }

            var sortedEntries = allEntries.Values.OrderBy(e => e.Key, StringComparer.Ordinal).ToList();

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            AppendNamespaceDeclaration(sb, namespaceName);
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Auto-generated registry of all translation keys and their English default values.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("public static partial class TranslationDefaults");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Gets all registered translation keys mapped to their English default values.");
            sb.AppendLine("    /// Key = dot-separated path (e.g., \"Validation.EmailRequired\"), Value = English default text.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    public static System.Collections.Generic.IReadOnlyDictionary<string, string> All { get; } = new System.Collections.Generic.Dictionary<string, string>");
            sb.AppendLine("    {");

            foreach (var entry in sortedEntries)
            {
                var escapedKey = EscapeString(entry.Key);
                var escapedValue = EscapeString(entry.DefaultValue);
                sb.AppendLine("        [\"" + escapedKey + "\"] = \"" + escapedValue + "\",");
            }

            sb.AppendLine("    };");
            sb.AppendLine("}");

            context.AddSource("TranslationDefaults.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));

            GenerateTranslationKeyPathsSource(context, namespaceName, sortedEntries);
            GenerateTranslationValuesSource(context, namespaceName, sortedEntries);
        }

        private static void AppendNamespaceDeclaration(StringBuilder sb, string namespaceName)
        {
            // An empty name means the TranslationDefaults class lives in the global namespace,
            // which cannot be declared explicitly; the generated types stay in the global
            // namespace by omitting the declaration entirely.
            if (namespaceName.Length == 0)
            {
                return;
            }

            sb.AppendLine("namespace " + namespaceName + ";");
            sb.AppendLine();
        }

        private static string EscapeString(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
            {
                return name;
            }

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static TranslationTreeNode BuildTree(List<TranslationEntry> entries)
        {
            var root = new TranslationTreeNode();
            foreach (var entry in entries)
            {
                var parts = entry.Key.Split('.');
                var current = root;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (!current.Children.TryGetValue(parts[i], out var child))
                    {
                        child = new TranslationTreeNode();
                        current.Children[parts[i]] = child;
                    }

                    current = child;
                }

                current.Leaves.Add(new TranslationLeaf(parts[parts.Length - 1], entry.Key));
            }

            return root;
        }

        private static void GenerateTranslationKeyPathsSource(SourceProductionContext context, string namespaceName, List<TranslationEntry> sortedEntries)
        {
            var root = BuildTree(sortedEntries);

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            AppendNamespaceDeclaration(sb, namespaceName);
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Auto-generated key path constants mirroring the <see cref=\"TranslationDefaults\"/> hierarchy.");
            sb.AppendLine("/// Pass these to <c>IStringLocalizer</c> for database-backed translation lookup.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("/// <remarks>");
            sb.AppendLine("/// Each constant holds the dot-separated key path used for direct database lookup.");
            sb.AppendLine("/// The English fallback for each key is available via <see cref=\"TranslationDefaults.All\"/>.");
            sb.AppendLine("/// </remarks>");
            sb.AppendLine("public static class TranslationKeys");
            sb.AppendLine("{");

            foreach (var leaf in root.Leaves.OrderBy(l => l.PropertyName, StringComparer.Ordinal))
            {
                var escapedKey = EscapeString(leaf.FullKey);
                sb.AppendLine("    public const string " + leaf.PropertyName + " = \"" + escapedKey + "\";");
                sb.AppendLine();
            }

            foreach (var child in root.Children.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                GenerateKeyPathsNestedClass(sb, child.Key, child.Value, "    ");
            }

            sb.AppendLine("}");

            context.AddSource("TranslationKeys.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static void GenerateKeyPathsNestedClass(StringBuilder sb, string name, TranslationTreeNode node, string indent)
        {
            sb.AppendLine(indent + "public static class " + name);
            sb.AppendLine(indent + "{");

            foreach (var leaf in node.Leaves.OrderBy(l => l.PropertyName, StringComparer.Ordinal))
            {
                var escapedKey = EscapeString(leaf.FullKey);
                sb.AppendLine(indent + "    public const string " + leaf.PropertyName + " = \"" + escapedKey + "\";");
                sb.AppendLine();
            }

            foreach (var child in node.Children.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                GenerateKeyPathsNestedClass(sb, child.Key, child.Value, indent + "    ");
            }

            sb.AppendLine(indent + "}");
            sb.AppendLine();
        }

        private static void GenerateTranslationValuesSource(SourceProductionContext context, string namespaceName, List<TranslationEntry> sortedEntries)
        {
            var root = BuildTree(sortedEntries);

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            AppendNamespaceDeclaration(sb, namespaceName);
            sb.AppendLine("/// <summary>");
            sb.AppendLine("/// Strongly-typed translation values DTO mirroring the <c>TranslationDefaults</c> hierarchy.");
            sb.AppendLine("/// Generated automatically by the <c>TranslationKeysGenerator</c>.");
            sb.AppendLine("/// </summary>");
            sb.AppendLine("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Design\", \"CA1034:Nested types should not be visible\", Justification = \"Nested classes mirror the TranslationDefaults hierarchy for strongly-typed frontend translation delivery.\")]");
            sb.AppendLine("public sealed class TranslationValues");
            sb.AppendLine("{");

            foreach (var child in root.Children.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                var typeName = child.Key + "Values";
                sb.AppendLine("    /// <summary>");
                sb.AppendLine("    /// Gets or sets the " + child.Key + " translations.");
                sb.AppendLine("    /// </summary>");
                sb.AppendLine("    [System.Text.Json.Serialization.JsonPropertyName(\"" + ToCamelCase(child.Key) + "\")]");
                sb.AppendLine("    public " + typeName + " " + child.Key + " { get; set; } = new " + typeName + "();");
                sb.AppendLine();
            }

            foreach (var leaf in root.Leaves.OrderBy(l => l.PropertyName, StringComparer.Ordinal))
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine("    /// Gets or sets the translated value for " + leaf.PropertyName + ".");
                sb.AppendLine("    /// </summary>");
                sb.AppendLine("    [System.Text.Json.Serialization.JsonPropertyName(\"" + ToCamelCase(leaf.PropertyName) + "\")]");
                sb.AppendLine("    public string " + leaf.PropertyName + " { get; set; } = string.Empty;");
                sb.AppendLine();
            }

            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    /// Creates a new <see cref=\"TranslationValues\" /> instance populated from a flat key-value dictionary.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine("    /// <param name=\"translations\">The flat translation dictionary with dot-separated keys.</param>");
            sb.AppendLine("    /// <returns>A populated <see cref=\"TranslationValues\" /> instance.</returns>");
            sb.AppendLine("    public static TranslationValues FromDictionary(System.Collections.Generic.IReadOnlyDictionary<string, string> translations)");
            sb.AppendLine("    {");
            sb.AppendLine("        var result = new TranslationValues();");
            sb.AppendLine();

            int varIndex = 0;
            GenerateFromDictionaryAssignments(sb, root, "result", ref varIndex, "        ");

            sb.AppendLine("        return result;");
            sb.AppendLine("    }");
            sb.AppendLine();

            foreach (var child in root.Children.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                GenerateNestedClass(sb, child.Key, child.Value, "    ");
            }

            sb.AppendLine("}");

            context.AddSource("TranslationValues.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static void GenerateNestedClass(StringBuilder sb, string name, TranslationTreeNode node, string indent)
        {
            var typeName = name + "Values";
            sb.AppendLine(indent + "/// <summary>");
            sb.AppendLine(indent + "/// Strongly-typed translations for the " + name + " category.");
            sb.AppendLine(indent + "/// </summary>");
            sb.AppendLine(indent + "public sealed class " + typeName);
            sb.AppendLine(indent + "{");

            foreach (var child in node.Children.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                var childTypeName = child.Key + "Values";
                sb.AppendLine(indent + "    /// <summary>");
                sb.AppendLine(indent + "    /// Gets or sets the " + child.Key + " translations.");
                sb.AppendLine(indent + "    /// </summary>");
                sb.AppendLine(indent + "    [System.Text.Json.Serialization.JsonPropertyName(\"" + ToCamelCase(child.Key) + "\")]");
                sb.AppendLine(indent + "    public " + childTypeName + " " + child.Key + " { get; set; } = new " + childTypeName + "();");
                sb.AppendLine();
            }

            foreach (var leaf in node.Leaves.OrderBy(l => l.PropertyName, StringComparer.Ordinal))
            {
                sb.AppendLine(indent + "    /// <summary>");
                sb.AppendLine(indent + "    /// Gets or sets the translated value for " + leaf.PropertyName + ".");
                sb.AppendLine(indent + "    /// </summary>");
                sb.AppendLine(indent + "    [System.Text.Json.Serialization.JsonPropertyName(\"" + ToCamelCase(leaf.PropertyName) + "\")]");
                sb.AppendLine(indent + "    public string " + leaf.PropertyName + " { get; set; } = string.Empty;");
                sb.AppendLine();
            }

            foreach (var child in node.Children.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                GenerateNestedClass(sb, child.Key, child.Value, indent + "    ");
            }

            sb.AppendLine(indent + "}");
            sb.AppendLine();
        }

        private static void GenerateFromDictionaryAssignments(StringBuilder sb, TranslationTreeNode node, string path, ref int varIndex, string indent)
        {
            foreach (var leaf in node.Leaves.OrderBy(l => l.PropertyName, StringComparer.Ordinal))
            {
                var varName = "v" + varIndex++;
                var escapedKey = EscapeString(leaf.FullKey);
                sb.AppendLine(indent + "if (translations.TryGetValue(\"" + escapedKey + "\", out var " + varName + "))");
                sb.AppendLine(indent + "{");
                sb.AppendLine(indent + "    " + path + "." + leaf.PropertyName + " = " + varName + ";");
                sb.AppendLine(indent + "}");
                sb.AppendLine();
            }

            foreach (var child in node.Children.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                GenerateFromDictionaryAssignments(sb, child.Value, path + "." + child.Key, ref varIndex, indent);
            }
        }

        private sealed class TranslationKeysInfo
        {
            public TranslationKeysInfo(string ns, string classDisplayName, Location classLocation, bool isPartial, List<TranslationEntry> entries)
            {
                this.Namespace = ns;
                this.ClassDisplayName = classDisplayName;
                this.ClassLocation = classLocation;
                this.IsPartial = isPartial;
                this.Entries = entries;
            }

            /// <summary>
            /// Gets the containing namespace, or an empty string for the global namespace.
            /// </summary>
            public string Namespace { get; }

            /// <summary>
            /// Gets the fully qualified class name used in diagnostic messages.
            /// </summary>
            public string ClassDisplayName { get; }

            /// <summary>
            /// Gets the location of the class identifier that diagnostics point at.
            /// </summary>
            public Location ClassLocation { get; }

            /// <summary>
            /// Gets a value indicating whether the inspected declaration carries the partial modifier.
            /// </summary>
            public bool IsPartial { get; }

            public List<TranslationEntry> Entries { get; }
        }

        private sealed class TranslationEntry
        {
            public TranslationEntry(string key, string defaultValue, Location? location)
            {
                this.Key = key;
                this.DefaultValue = defaultValue;
                this.Location = location;
            }

            public string Key { get; }

            public string DefaultValue { get; }

            public Location? Location { get; }
        }

        private sealed class TranslationTreeNode
        {
            public TranslationTreeNode()
            {
                this.Children = new Dictionary<string, TranslationTreeNode>();
                this.Leaves = new List<TranslationLeaf>();
            }

            public Dictionary<string, TranslationTreeNode> Children { get; }

            public List<TranslationLeaf> Leaves { get; }
        }

        private sealed class TranslationLeaf
        {
            public TranslationLeaf(string propertyName, string fullKey)
            {
                this.PropertyName = propertyName;
                this.FullKey = fullKey;
            }

            public string PropertyName { get; }

            public string FullKey { get; }
        }
    }
}
