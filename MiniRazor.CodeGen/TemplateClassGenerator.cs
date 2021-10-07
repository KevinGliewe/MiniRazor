﻿using System.Text;
using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using MiniRazor.CodeGen.Utils.Extensions;

namespace MiniRazor.CodeGen
{
    [Generator]
    public partial class TemplateClassGenerator : ISourceGenerator
    {
        private static readonly string GeneratorVersion =
            typeof(TemplateClassGenerator).Assembly.GetName().Version.ToString(3);

        private static string? TryGetModelTypeName(string code, string className) =>
            Regex.Match(
                    code,
                    $@"\s*{Regex.Escape(className)}\s*:\s*MiniRazor\.TemplateBase<(.+)>",
                    RegexOptions.Multiline, TimeSpan.FromSeconds(1)
                )
                .Groups[1]
                .Value
                .NullIfWhiteSpace();

        private static string? TryGetNamespace(string code) =>
            Regex.Match(
                    code,
                    @"namespace (\S+)",
                    RegexOptions.Multiline, TimeSpan.FromSeconds(1)
                )
                .Groups[1]
                .Value
                .NullIfWhiteSpace();

        // Checksum directive contains a file name which might not match the hint name
        // of the generated file. This can cause issues where the PDB file might reference
        // a compilation unit that doesn't actually exist. Also, the directive isn't very
        // useful to us anyway so it's best to just remove it.
        private static string StripChecksumDirectives(string code) =>
            Regex.Replace(
                code,
                @"^\s*#pragma checksum\s+.+$", "",
                RegexOptions.Multiline, TimeSpan.FromSeconds(1)
            );

        // Avoid nullability warnings in the generated code
        private static string StripNullableDirectives(string code) =>
            Regex.Replace(
                code,
                @"^\s*#nullable\s+(restore|disable)\s*$", "",
                RegexOptions.Multiline, TimeSpan.FromSeconds(1)
            );

        // Line directives may be useful but we're going to remove them for now
        private static string StripLineDirectives(string code) =>
            Regex.Replace(
                code,
                @"^\s*#line\s+.+$", "",
                RegexOptions.Multiline, TimeSpan.FromSeconds(1)
            );

        private void ProcessFile(
            GeneratorExecutionContext context,
            string filePath,
            string accessModifier,
            string content)
        {
            // Generate class name from file name
            var className = SanitizeIdentifier(Path.GetFileNameWithoutExtension(filePath));

            var code = Razor.Transpile(content, accessModifier, options =>
            {
                options.ConfigureClass((_, node) =>
                {
                    node.ClassName = className;
                });
            });

            // Get model type from the template's base class
            var modelTypeName = TryGetModelTypeName(code, className) ?? "dynamic";

            var @namespace = TryGetNamespace(code);

            // Remove junk generated by Razor compiler
            code = StripChecksumDirectives(code);
            code = StripNullableDirectives(code).Insert(0, "#nullable disable" + Environment.NewLine);
            code = StripLineDirectives(code);

            // Add documentation to the class
            code = code.Insert(code.IndexOf($"{accessModifier} partial class", StringComparison.Ordinal), $@"
/// <summary>Template: {filePath}</summary>
/// <remarks>Generated by MiniRazor v{GeneratorVersion} on {DateTimeOffset.Now}.</remarks>
");

            // Extend the template with some additional code
            code = code.Insert(code.IndexOf("public async override", StringComparison.Ordinal), $@"
/// <summary>Renders the template using the specified writer.</summary>
public static async global::System.Threading.Tasks.Task RenderAsync(global::System.IO.TextWriter output, {modelTypeName} model, global::System.Threading.CancellationToken cancellationToken = default)
{{
    var template = new {className}();
    template.Output = output;
    template.Model = model;
    template.CancellationToken = cancellationToken;

    await template.ExecuteAsync().ConfigureAwait(false);
}}

/// <summary>Renders the template to a string.</summary>
public static async global::System.Threading.Tasks.Task<string> RenderAsync({modelTypeName} model, global::System.Threading.CancellationToken cancellationToken = default)
{{
    using (var output = new global::System.IO.StringWriter())
    {{
        await RenderAsync(output, model, cancellationToken).ConfigureAwait(false);
        return output.ToString();
    }}
}}
");

            var hintName = !string.IsNullOrWhiteSpace(@namespace)
                ? $"{@namespace}.{className}.g.cs"
                : className;

            context.AddSource(hintName, code);
        }

        /// <inheritdoc />
        public void Execute(GeneratorExecutionContext context)
        {
            foreach (var file in context.AdditionalFiles)
            {
                var isRazorTemplate = string.Equals(
                    context.AnalyzerConfigOptions
                        .GetOptions(file)
                        .TryGetAdditionalFileMetadataValue("IsRazorTemplate"),
                    "true",
                    StringComparison.OrdinalIgnoreCase
                );

                if (!isRazorTemplate)
                    continue;

                var content = file.GetText(context.CancellationToken)?.ToString();
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                var accessModifier = context.AnalyzerConfigOptions
                    .GetOptions(file)
                    .TryGetAdditionalFileMetadataValue("AccessModifier") ?? "internal";

                ProcessFile(context, file.Path, accessModifier, content);
            }
        }

        /// <inheritdoc />
        public void Initialize(GeneratorInitializationContext context)
        {
        }
    }

    public partial class TemplateClassGenerator
    {
        private static string SanitizeIdentifier(string identifier)
        {
            var buffer = new StringBuilder(identifier);

            // Must start with a letter or an underscore
            if (buffer.Length > 0 && buffer[0] != '_' && !char.IsLetter(buffer[0]))
            {
                buffer.Insert(0, '_');
            }

            // Replace all other prohibited characters with underscores
            for (var i = 0; i < buffer.Length; i++)
            {
                if (!char.IsLetterOrDigit(buffer[i]))
                    buffer[i] = '_';
            }

            return buffer.ToString();
        }
    }
}