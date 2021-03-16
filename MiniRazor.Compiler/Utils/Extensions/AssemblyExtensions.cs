using System.Reflection;
using Microsoft.CodeAnalysis;

namespace MiniRazor.Compiler.Utils.Extensions
{
    internal static class AssemblyExtensions
    {
        public static MetadataReference ToMetadataReference(this Assembly assembly) =>
            MetadataReference.CreateFromFile(assembly.Location);
    }
}