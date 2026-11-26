using System.Diagnostics.Metrics;

namespace Chamados.Domain;

/// <summary>
/// Métricas de negócio (Módulo 8): quantos chamados são abertos e quantas mudanças de status
/// acontecem. Usa só System.Diagnostics.Metrics, da própria plataforma: o domínio continua sem
/// pacotes externos, e qualquer coletor (dotnet-counters, OpenTelemetry) lê o medidor "Chamados".
/// </summary>
public sealed class MetricasChamados
{
    public const string NomeDoMedidor = "Chamados";

    private readonly Counter<long> _abertos;
    private readonly Counter<long> _transicoes;

    public MetricasChamados(IMeterFactory fabrica)
    {
        ArgumentNullException.ThrowIfNull(fabrica);

        // A fábrica (registrada pelo host) cuida do ciclo de vida do medidor e isola os testes.
        var medidor = fabrica.Create(NomeDoMedidor);
        _abertos = medidor.CreateCounter<long>(
            "chamados.abertos", unit: "{chamado}", description: "Chamados abertos, por prioridade");
        _transicoes = medidor.CreateCounter<long>(
            "chamados.transicoes", unit: "{transicao}", description: "Mudanças de status, por status de destino");
    }

    // Etiquetas (tags) com poucos valores possíveis: prioridade e status. Nunca Id, título ou nome
    // de pessoa: cada valor distinto vira uma série nova no coletor, e dado pessoal não entra em métrica.
    public void ChamadoAberto(Prioridade prioridade)
        => _abertos.Add(1, new KeyValuePair<string, object?>("prioridade", prioridade.ToString()));

    public void StatusMudou(StatusChamado para)
        => _transicoes.Add(1, new KeyValuePair<string, object?>("status", para.ToString()));
}
