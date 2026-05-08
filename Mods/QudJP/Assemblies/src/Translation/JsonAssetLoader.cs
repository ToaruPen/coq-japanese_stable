using System.IO;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace QudJP;

internal static class JsonAssetLoader
{
    internal static T LoadFromFile<T>(string path)
        where T : class
    {
        using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream);
        using var jsonReader = new JsonTextReader(reader);
        var serializer = new JsonSerializer();
        T? document;
        try
        {
            document = serializer.Deserialize<T>(jsonReader);
        }
        catch (JsonException ex)
        {
            throw new SerializationException($"Malformed JSON asset: {path}", ex);
        }

        if (document is null)
        {
            throw new InvalidDataException($"JSON asset deserialized to null: {path}");
        }

        return document;
    }
}
