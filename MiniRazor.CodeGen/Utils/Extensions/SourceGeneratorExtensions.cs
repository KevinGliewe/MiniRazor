﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MiniRazor.CodeGen.Utils.Extensions
{
    internal static class SourceGeneratorExtensions
    {
        public static string? GetMSBuildProperty(
            this GeneratorExecutionContext context,
            string name)
        {
            context.AnalyzerConfigOptions.GlobalOptions.TryGetValue($"build_property.{name}", out var value);
            return value;
        }

        public static string? TryGetValue(this AnalyzerConfigOptions options, string key) =>
            options.TryGetValue(key, out var value) ? value : null;

        public static string? TryGetAdditionalFileMetadataValue(this AnalyzerConfigOptions options, string propertyName) =>
            options.TryGetValue($"build_metadata.AdditionalFiles.{propertyName}");
    }
}