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

    // Mensagens não lidas: até quando cada usuário leu cada conversa, fora dos chamados (como no banco).
    private readonly ConcurrentDictionary<(Guid ChamadoId, string UsuarioId), DateTimeOffset> _leituras = new();

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

        // Mensagens não lidas: com leitor, cada resumo traz quantas mensagens ele ainda não leu.
        return consulta
            .Select(c => filtro.LeitorId is { } leitor
                ? ResumoChamado.De(c) with { ComentariosNaoLidos = NaoLidas(c, leitor).Count }
                : ResumoChamado.De(c))
            .ToList();
    }

    public async Task<EstatisticasChamados> ObterEstatisticasAsync(string? abertoPorId = null,
        CancellationToken cancellationToken = default)
    {
        await SimularLatenciaAsync(cancellationToken);
        var chamados = _chamados.Values.Where(c => abertoPorId is null || c.FoiAbertoPor(abertoPorId)).ToList();

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

    public async Task<IReadOnlyList<ResumoChamado>> ListarPorResponsavelAsync(string responsavel, string? abertoPorId = null,
        CancellationToken cancellationToken = default)
    {
        await SimularLatenciaAsync(cancellationToken);
        var chamados = _chamados.Values
            .Where(c => c.Responsavel == responsavel)
            .Where(c => abertoPorId is null || c.FoiAbertoPor(abertoPorId));
        return Ordenar(chamados, OrdemChamados.Prioridade)
            .Select(ResumoChamado.De)
            .ToList();
    }

    public async Task<IReadOnlyList<ContagemPorResponsavel>> ContarPorResponsavelAsync(FiltroChamados filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        await SimularLatenciaAsync(cancellationToken);
        return Filtrar(filtro)
            .GroupBy(c => c.ResponsavelId)
            .Select(g => new ContagemPorResponsavel(g.Key, g.Max(c => c.Responsavel), g.Count()))
            .ToList();
    }

    public async Task<IReadOnlyList<ResolucaoChamado>> ListarResolucoesAsync(CancellationToken cancellationToken = default)
    {
        await SimularLatenciaAsync(cancellationToken);
        return _chamados.Values
            .Where(c => c.Status == StatusChamado.Resolvido)
            .Select(c => new ResolucaoChamado(c.Prioridade, c.CriadoEm, c.EncerradoEm!.Value))
            .ToList();
    }

    public Task<DateTimeOffset?> ObterLeituraAsync(Guid chamadoId, string usuarioId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_leituras.TryGetValue((chamadoId, usuarioId), out var ate) ? ate : (DateTimeOffset?)null);
    }

    public Task MarcarLeituraAsync(Guid chamadoId, string usuarioId, DateTimeOffset ate, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);
        cancellationToken.ThrowIfCancellationRequested();

        // Como no banco, a leitura de um chamado que não existe é recusada.
        if (!_chamados.ContainsKey(chamadoId))
        {
            throw new ChamadoNaoEncontradoException(chamadoId);
        }

        // A leitura só avança: entre o gravado e o novo, fica o mais recente.
        _leituras.AddOrUpdate((chamadoId, usuarioId), ate, (_, atual) => ate > atual ? ate : atual);
        return Task.CompletedTask;
    }

    private IReadOnlyList<Comentario> NaoLidas(Chamado chamado, string leitorId)
        => chamado.NaoLidasPor(leitorId, _leituras.TryGetValue((chamado.Id, leitorId), out var ate) ? ate : null);

    private IEnumerable<Chamado> Filtrar(FiltroChamados filtro) => _chamados.Values
        .Where(c => filtro.Status is null || c.Status == filtro.Status)
        .Where(c => filtro.PrioridadeMinima is null || c.Prioridade >= filtro.PrioridadeMinima)
        .Where(c => string.IsNullOrWhiteSpace(filtro.Texto)
                    || c.Titulo.Contains(filtro.Texto, StringComparison.OrdinalIgnoreCase)
                    || (c.Descricao?.Contains(filtro.Texto, StringComparison.OrdinalIgnoreCase) ?? false))
        // Prazos: só em andamento e abertos antes do corte da própria prioridade (mesma regra do banco).
        .Where(c => filtro.AbertosAntesDe is not { } cortes
                    || (!c.EstaEncerrado && c.CriadoEm < cortes[c.Prioridade]))
        // Meus chamados: só os que esse usuário abriu (chamados sem dono nunca entram).
        .Where(c => filtro.AbertoPorId is null || c.FoiAbertoPor(filtro.AbertoPorId))
        // Responsável atendente: só os que estão com esse atendente.
        .Where(c => filtro.ResponsavelId is null || c.FoiAtribuidoA(filtro.ResponsavelId))
        // Mensagens não lidas: só os que têm mensagem que o leitor ainda não leu (sem leitor, nenhum).
        .Where(c => !filtro.SomenteComNaoLidas || (filtro.LeitorId is { } leitor && NaoLidas(c, leitor).Count > 0));

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
