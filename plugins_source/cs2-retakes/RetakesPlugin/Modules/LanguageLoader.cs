using System.Reflection;
using System.Text.Json;

namespace RetakesPlugin.Modules;

public static class LanguageLoader
{
    public static Dictionary<string, string> LoadTranslations(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = GetResourceFullName(assembly, fileName);

        if (resourceName is null)
            throw new FileNotFoundException($"Arquivo '{fileName}' não foi encontrado como recurso embutido.");

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException($"Erro ao carregar o recurso embutido '{resourceName}'.");

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

        return translations ?? new();
    }

    private static string? GetResourceFullName(Assembly assembly, string fileName)
    {
        return assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
    }
}