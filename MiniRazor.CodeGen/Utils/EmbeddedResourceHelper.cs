using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace MiniRazor.CodeGen.Utils.Extensions
{
    internal static class EmbeddedResourceHelper
    {
        // https://stackoverflow.com/a/56753308
        public static string ReadManifestData(string embeddedFileName) {
            var assembly = typeof(EmbeddedResourceHelper).GetTypeInfo().Assembly;
            var resourceName = assembly.GetManifestResourceNames().First(s => s.EndsWith(embeddedFileName, StringComparison.CurrentCultureIgnoreCase));

            using (var stream = assembly.GetManifestResourceStream(resourceName)) {
                if (stream == null) {
                    throw new InvalidOperationException("Could not load manifest resource stream.");
                }
                using (var reader = new StreamReader(stream)) {
                    return reader.ReadToEnd();
                }
            }
        }

        public static string ReadManifestSourceFile(string embeddedFileName, string newNamespace)
            => ReadManifestData(embeddedFileName).Replace("namespace MiniRazor", "namespace " + newNamespace);
    }
}