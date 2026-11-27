namespace Chamados.Domain;

/// <summary>
/// Casos de uso dos chamados. Depende de abstrações recebidas por injeção:
/// o repositório (onde guardar), o relógio (que horas são) e, desde o Módulo 8,
/// as métricas (opcionais: os testes do domínio passam sem elas).
/// Prazos: a política de prazos também é opcional; sem ela, vale <see cref="PoliticaDePrazos.Padrao"/>.
/// </summary>
public sealed class ChamadoServico(
    IChamadoRepositorio repositorio,
    TimeProvider relogio,
    MetricasChamados? metricas = null,
    PoliticaDePrazos? prazos = null)
{
    /// <summary>A política de prazos em vigor.</summary>
    public PoliticaDePrazos Prazos { get; } = prazos ?? PoliticaDePrazos.Padrao;

    public async Task<Chamado> AbrirAsync(NovoChamado comando, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var chamado = new Chamado(comando.Titulo, comando.Prioridade, comando.Descricao, relogio.GetUtcNow(), comando.AbertoPor);
        await repositorio.AdicionarAsync(chamado, cancellationToken);
        metricas?.ChamadoAberto(chamado.Prioridade);
        return chamado;
    }

    /// <summary>Consulta um chamado. Devolve null quando o Id não existe.</summary>
    public Task<Chamado?> ObterAsync(Guid id, CancellationToken cancellationToken = default)
        => repositorio.ObterAsync(id, cancellationToken);

    /// <summary>
    /// Edita título, prioridade e descrição, desde que ninguém tenha alterado
    /// o chamado depois que o usuário o leu.
    /// </summary>
    public Task<Chamado> EditarAsync(Guid id, EdicaoChamado comando, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        return AlterarAsync(id, chamado =>
        {
            if (chamado.Versao != comando.VersaoEsperada)
            {
                throw new ConcorrenciaException(id, comando.VersaoEsperada, chamado.Versao);
            }

            chamado.Editar(comando.Titulo, comando.Prioridade, comando.Descricao);
        }, cancellationToken);
    }

    // Meus chamados: "por" é quem faz a mudança, gravado no histórico. Opcional para os testes do
    // domínio e para a semente; a camada Web sempre informa o usuário conectado.
    public Task<Chamado> IniciarAtendimentoAsync(Guid id, string responsavel, Autor? por = null,
        CancellationToken cancellationToken = default)
        => TransicionarAsync(id, chamado => chamado.IniciarAtendimento(responsavel, relogio.GetUtcNow(), por), cancellationToken);

    public async Task<Chamado> ResolverAsync(Guid id, string solucao, Autor? por = null,
        CancellationToken cancellationToken = default)
    {
        var chamado = await TransicionarAsync(id, c => c.Resolver(solucao, relogio.GetUtcNow(), por), cancellationToken);

        // Prazos: a métrica conta cada resolução, separando as que cumpriram o prazo.
        metricas?.ChamadoResolvido(PrazoDe(chamado)?.Situacao == SituacaoPrazo.Cumprido);
        return chamado;
    }

    public Task<Chamado> CancelarAsync(Guid id, string motivo, Autor? por = null,
        CancellationToken cancellationToken = default)
        => TransicionarAsync(id, chamado => chamado.Cancelar(motivo, relogio.GetUtcNow(), por), cancellationToken);

    /// <summary>
    /// Chamados atribuídos a um responsável (Módulo 8), do mais urgente ao menos.
    /// Meus chamados: com <paramref name="abertoPorId"/>, só entre os que esse usuário abriu.
    /// </summary>
    public Task<IReadOnlyList<ResumoChamado>> ListarPorResponsavelAsync(string responsavel, string? abertoPorId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responsavel);
        return repositorio.ListarPorResponsavelAsync(responsavel.Trim(), abertoPorId, cancellationToken);
    }

    public Task<IReadOnlyList<ResumoChamado>> ListarAsync(
        FiltroChamados filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        return repositorio.ListarAsync(TraduzirPrazos(filtro), cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Uma página da listagem. Número de página fora do intervalo é ajustado
    /// para o mais próximo válido: ?pagina=99 mostra a última, não um erro.
    /// Duas consultas: uma conta, outra traz só os itens da página.
    /// </summary>
    public async Task<Pagina<ResumoChamado>> ListarPaginaAsync(
        FiltroChamados filtro,
        int numero,
        int tamanhoPagina,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        ArgumentOutOfRangeException.ThrowIfLessThan(tamanhoPagina, 1);

        // O mesmo instante para contar e para listar: as duas consultas veem os mesmos vencidos.
        filtro = TraduzirPrazos(filtro);
        var total = await repositorio.ContarAsync(filtro, cancellationToken);
        var totalPaginas = Math.Max(1, (total + tamanhoPagina - 1) / tamanhoPagina);
        numero = Math.Clamp(numero, 1, totalPaginas);

        var itens = await repositorio.ListarAsync(
            filtro, (numero - 1) * tamanhoPagina, tamanhoPagina, cancellationToken);

        return new Pagina<ResumoChamado>(itens, numero, tamanhoPagina, total);
    }

    /// <summary>Totais. Meus chamados: com <paramref name="abertoPorId"/>, só os desse usuário.</summary>
    public Task<EstatisticasChamados> ObterEstatisticasAsync(string? abertoPorId = null,
        CancellationToken cancellationToken = default)
        => repositorio.ObterEstatisticasAsync(abertoPorId, cancellationToken);

    /// <summary>Prazo de um chamado agora; null para cancelados.</summary>
    public PrazoDoChamado? PrazoDe(Chamado chamado) => Prazos.Avaliar(chamado, relogio.GetUtcNow());

    /// <summary>Prazo de um chamado da lista agora; null para cancelados.</summary>
    public PrazoDoChamado? PrazoDe(ResumoChamado chamado) => Prazos.Avaliar(chamado, relogio.GetUtcNow());

    /// <summary>
    /// Quantos chamados em andamento estão com o prazo vencido agora (contagem no banco).
    /// Meus chamados: com <paramref name="abertoPorId"/>, só os desse usuário.
    /// </summary>
    public Task<int> ContarVencidosAsync(string? abertoPorId = null, CancellationToken cancellationToken = default)
        => repositorio.ContarAsync(
            TraduzirPrazos(new FiltroChamados(SomenteVencidos: true, AbertoPorId: abertoPorId)), cancellationToken);

    /// <summary>
    /// Indicadores de prazo. Vencidos e perto de vencer são contados no banco; o cumprimento dos
    /// resolvidos é calculado aqui, porque o SQLite não subtrai datas.
    /// </summary>
    public async Task<IndicadoresDePrazo> ObterIndicadoresDePrazoAsync(CancellationToken cancellationToken = default)
    {
        var agora = relogio.GetUtcNow();
        var vencidos = await repositorio.ContarAsync(
            new FiltroChamados { AbertosAntesDe = Prazos.AberturasVencidas(agora) }, cancellationToken);
        var emAlerta = await repositorio.ContarAsync(
            new FiltroChamados { AbertosAntesDe = Prazos.AberturasEmAlerta(agora) }, cancellationToken);

        var resolucoes = await repositorio.ListarResolucoesAsync(cancellationToken);
        var noPrazo = resolucoes.Count(r =>
            Prazos.Avaliar(StatusChamado.Resolvido, r.Prioridade, r.CriadoEm, r.ResolvidoEm, agora)?.Situacao
                == SituacaoPrazo.Cumprido);

        // Em alerta inclui os vencidos: "perto de vencer" é só a diferença.
        return new IndicadoresDePrazo(vencidos, emAlerta - vencidos, noPrazo, resolucoes.Count - noPrazo, Prazos.Prazos);
    }

    // "Só vencidos" vira, para o repositório, um instante de corte por prioridade, calculado agora.
    private FiltroChamados TraduzirPrazos(FiltroChamados filtro)
        => filtro.SomenteVencidos && filtro.AbertosAntesDe is null
            ? filtro with { AbertosAntesDe = Prazos.AberturasVencidas(relogio.GetUtcNow()) }
            : filtro;

    // Carregar, alterar e gravar numa só unidade de trabalho é tarefa do repositório:
    // com banco de dados, é ali que o EF Core rastreia o objeto e confere a versão.
    private Task<Chamado> AlterarAsync(Guid id, Action<Chamado> alteracao, CancellationToken cancellationToken)
        => repositorio.AlterarAsync(id, alteracao, cancellationToken);

    // A métrica só conta depois de gravar: uma transição recusada pelo domínio ou pelo banco não entra.
    private async Task<Chamado> TransicionarAsync(Guid id, Action<Chamado> transicao, CancellationToken cancellationToken)
    {
        var chamado = await AlterarAsync(id, transicao, cancellationToken);
        metricas?.StatusMudou(chamado.Status);
        return chamado;
    }
}
