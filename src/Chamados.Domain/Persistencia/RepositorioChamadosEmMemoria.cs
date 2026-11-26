using System.Collections.Concurrent;
using System.Globalization;

namespace Chamados.Domain;

/// <summary>
/// Armazenamento em memória, perdido ao reiniciar. Desde o Módulo 7 a aplicação usa o
/// banco (Chamados.Infra); esta implementação continua servindo aos testes do domínio,
/// rápidos e sem arquivo de banco. A latência simulada existe para observar assincronismo.
/// </summary>
public sealed class RepositorioChamadosEmMemoria(TimeProvider relogio, TimeSpan latenciaSimulada = default)
    : IChamadoRepositorio
{
    // Comparação de textos em português: "Água" fica junto de "agua", antes de "Bebedouro".
    private static readonly StringComparer ComparadorTitulo =
        StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true);

    private readonly ConcurrentDictionary<Guid, Chamado> _chamados = new();

    public Task<Chamado?> ObterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_chamados.GetValueOrDefault(id));
    }

    public Task AdicionarAsync(Chamado chamado, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chamado);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_chamados.TryAdd(chamado.Id, chamado))
        {
            throw new InvalidOperationException($"Já existe um chamado com o Id {chamado.Id}.");
        }

        return Task.CompletedTask;
    }

    public Task<Chamado> AlterarAsync(Guid id, Action<Chamado> alteracao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alteracao);
        cancellationToken.ThrowIfCancellationRequested();

        // Em memória o objeto é o próprio armazenamento: alterar já é gravar.
        var chamado = _chamados.GetValueOrDefault(id) ?? throw new ChamadoNaoEncontradoException(id);
        alteracao(chamado);
        return Task.FromResult(chamado);
    }

    public async Task<int> ContarAsync(FiltroChamados filtro, CancellationToken cancellationToken = default)
    {
        await SimularLatenciaAsync(cancellationToken);
        return Filtrar(filtro).Count();
    }

    public async Task<IReadOnlyList<ResumoChamado>> ListarAsync(
        FiltroChamados filtro, int pular = 0, int? pegar = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        await SimularLatenciaAsync(cancellationToken);

        var consulta = Ordenar(Filtrar(filtro), filtro.Ordem).Skip(pular);
        if (pegar is { } quantidade)
        {
            consulta = consulta.Take(quantidade);
        }

        return consulta.Select(ResumoChamado.De).ToList();
    }

    public async Task<EstatisticasChamados> ObterEstatisticasAsync(CancellationToken cancellationToken = default)
    {
        await SimularLatenciaAsync(cancellationToken);
        var chamados = _chamados.Values.ToList();

        // Todos os status aparecem, mesmo com zero: parte do enum, não dos dados.
        var porStatus = Enum.GetValues<StatusChamado>()
            .ToDictionary(status => status, status => chamados.Count(c => c.Status == status));

        // Só as prioridades presentes aparecem: GroupBy agrupa o que existe.
        var porPrioridade = chamados
            .GroupBy(c => c.Prioridade)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Count());

        var tempos = chamados
            .Where(c => c.Status == StatusChamado.Resolvido)
            .Select(c => c.TempoAteEncerramento!.Value)
            .ToList();

        TimeSpan? tempoMedio = tempos.Count == 0 ? null : TimeSpan.FromTicks((long)tempos.Average(t => t.Ticks));

        return new EstatisticasChamados(chamados.Count, porStatus, porPrioridade, tempoMedio);
    }

    public async Task<IReadOnlyList<ResumoChamado>> ListarPorResponsavelAsync(string responsavel, CancellationToken cancellationToken = default)
    {
        await SimularLatenciaAsync(cancellationToken);
        return Ordenar(_chamados.Values.Where(c => c.Responsavel == responsavel), OrdemChamados.Prioridade)
            .Select(ResumoChamado.De)
            .ToList();
    }

    private IEnumerable<Chamado> Filtrar(FiltroChamados filtro) => _chamados.Values
        .Where(c => filtro.Status is null || c.Status == filtro.Status)
        .Where(c => filtro.PrioridadeMinima is null || c.Prioridade >= filtro.PrioridadeMinima)
        .Where(c => string.IsNullOrWhiteSpace(filtro.Texto)
                    || c.Titulo.Contains(filtro.Texto, StringComparison.OrdinalIgnoreCase)
                    || (c.Descricao?.Contains(filtro.Texto, StringComparison.OrdinalIgnoreCase) ?? false));

    private static IEnumerable<Chamado> Ordenar(IEnumerable<Chamado> chamados, OrdemChamados ordem) => ordem switch
    {
        OrdemChamados.MaisRecentes => chamados.OrderByDescending(c => c.CriadoEm),
        OrdemChamados.MaisAntigos => chamados.OrderBy(c => c.CriadoEm),
        OrdemChamados.Titulo => chamados.OrderBy(c => c.Titulo, ComparadorTitulo),
        _ => chamados.OrderByDescending(c => c.Prioridade).ThenBy(c => c.CriadoEm)
    };

    private Task SimularLatenciaAsync(CancellationToken cancellationToken)
    {
        if (latenciaSimulada > TimeSpan.Zero)
        {
            // Task.Delay com TimeProvider: em testes, um relógio falso controla a espera.
            return Task.Delay(latenciaSimulada, relogio, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
