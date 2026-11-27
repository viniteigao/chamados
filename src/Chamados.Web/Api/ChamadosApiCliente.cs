using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chamados.Domain;

namespace Chamados.Web.Api;

/// <summary>
/// Cliente HTTP tipado da API de chamados. Registrado com AddHttpClient: o HttpClient vem
/// configurado (endereço base) e o pool de conexões é gerenciado pela fábrica.
/// </summary>
public sealed class ChamadosApiCliente(HttpClient http)
{
    // As mesmas regras de JSON do servidor: nomes em camelCase e enums como texto ("EmAtendimento").
    private static readonly JsonSerializerOptions OpcoesJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<EstatisticasChamados> ObterEstatisticasAsync(CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<EstatisticasChamados>("api/chamados/estatisticas", OpcoesJson, cancellationToken)
           ?? throw new InvalidOperationException("A API respondeu sem conteúdo.");

    /// <summary>Responsável atendente: GET /api/chamados/carga.</summary>
    public async Task<IReadOnlyList<CargaResposta>> ObterCargaAsync(CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<List<CargaResposta>>("api/chamados/carga", OpcoesJson, cancellationToken)
           ?? throw new InvalidOperationException("A API respondeu sem conteúdo.");

    /// <summary>Prazos: GET /api/chamados/prazos.</summary>
    public async Task<IndicadoresDePrazo> ObterIndicadoresDePrazoAsync(CancellationToken cancellationToken = default)
        => await http.GetFromJsonAsync<IndicadoresDePrazo>("api/chamados/prazos", OpcoesJson, cancellationToken)
           ?? throw new InvalidOperationException("A API respondeu sem conteúdo.");
}
