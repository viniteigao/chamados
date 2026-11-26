using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chamados.Web.Tests;

/// <summary>As mesmas regras de JSON da API: camelCase e enums como texto.</summary>
internal static class Json
{
    public static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
