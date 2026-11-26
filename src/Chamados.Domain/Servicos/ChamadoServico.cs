namespace Chamados.Domain;

/// <summary>
/// Casos de uso dos chamados. Depende de abstrações recebidas por injeção:
/// o repositório (onde guardar), o relógio (que horas são) e, desde o Módulo 8,
/// as métricas (opcionais: os testes do domínio passam sem elas).
/// </summary>
public sealed class ChamadoServico(IChamadoRepositorio repositorio, TimeProvider relogio, MetricasChamados? metricas = null)
{
    public async Task<Chamado> AbrirAsync(NovoChamado comando, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comando);

        var chamado = new Chamado(comando.Titulo, comando.Prioridade, comando.Descricao, relogio.GetUtcNow());
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

    public Task<Chamado> IniciarAtendimentoAsync(Guid id, string responsavel, CancellationToken cancellationToken = default)
        => TransicionarAsync(id, chamado => chamado.IniciarAtendimento(responsavel, relogio.GetUtcNow()), cancellationToken);

    public Task<Chamado> ResolverAsync(Guid id, string solucao, CancellationToken cancellationToken = default)
        => TransicionarAsync(id, chamado => chamado.Resolver(solucao, relogio.GetUtcNow()), cancellationToken);

    public Task<Chamado> CancelarAsync(Guid id, string motivo, CancellationToken cancellationToken = default)
        => TransicionarAsync(id, chamado => chamado.Cancelar(motivo, relogio.GetUtcNow()), cancellationToken);

    /// <summary>Chamados atribuídos a um responsável (Módulo 8), do mais urgente ao menos.</summary>
    public Task<IReadOnlyList<ResumoChamado>> ListarPorResponsavelAsync(string responsavel, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responsavel);
        return repositorio.ListarPorResponsavelAsync(responsavel.Trim(), cancellationToken);
    }

    public Task<IReadOnlyList<ResumoChamado>> ListarAsync(
        FiltroChamados filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        return repositorio.ListarAsync(filtro, cancellationToken: cancellationToken);
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

        var total = await repositorio.ContarAsync(filtro, cancellationToken);
        var totalPaginas = Math.Max(1, (total + tamanhoPagina - 1) / tamanhoPagina);
        numero = Math.Clamp(numero, 1, totalPaginas);

        var itens = await repositorio.ListarAsync(
            filtro, (numero - 1) * tamanhoPagina, tamanhoPagina, cancellationToken);

        return new Pagina<ResumoChamado>(itens, numero, tamanhoPagina, total);
    }

    public Task<EstatisticasChamados> ObterEstatisticasAsync(CancellationToken cancellationToken = default)
        => repositorio.ObterEstatisticasAsync(cancellationToken);

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
