namespace Chamados.Domain;

/// <summary>
/// Contrato de armazenamento de chamados. Duas implementações: em memória (testes do domínio)
/// e com Entity Framework Core sobre SQLite (Módulo 7, projeto Chamados.Infra).
/// As consultas recebem o filtro e devolvem só o que a tela precisa: quem implementa decide
/// como filtrar, ordenar e paginar (em memória com LINQ, no banco com SQL).
/// </summary>
public interface IChamadoRepositorio
{
    /// <summary>Um chamado completo, com o histórico, só para leitura.</summary>
    Task<Chamado?> ObterAsync(Guid id, CancellationToken cancellationToken = default);

    Task AdicionarAsync(Chamado chamado, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carrega o chamado, aplica a alteração e grava, tudo numa única unidade de trabalho.
    /// Lança <see cref="ChamadoNaoEncontradoException"/> se o Id não existir e
    /// <see cref="ConcorrenciaException"/> se outra pessoa gravou antes.
    /// </summary>
    Task<Chamado> AlterarAsync(Guid id, Action<Chamado> alteracao, CancellationToken cancellationToken = default);

    Task<int> ContarAsync(FiltroChamados filtro, CancellationToken cancellationToken = default);

    /// <summary>Resumos filtrados e ordenados; <paramref name="pular"/> e <paramref name="pegar"/> paginam.</summary>
    Task<IReadOnlyList<ResumoChamado>> ListarAsync(
        FiltroChamados filtro, int pular = 0, int? pegar = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Totais por status e prioridade. Meus chamados: com <paramref name="abertoPorId"/>, só os
    /// chamados que esse usuário abriu.
    /// </summary>
    Task<EstatisticasChamados> ObterEstatisticasAsync(string? abertoPorId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumos dos chamados de um responsável (comparação exata), por prioridade.
    /// Meus chamados: com <paramref name="abertoPorId"/>, só entre os que esse usuário abriu.
    /// </summary>
    Task<IReadOnlyList<ResumoChamado>> ListarPorResponsavelAsync(
        string responsavel, string? abertoPorId = null, CancellationToken cancellationToken = default);

    /// <summary>Prazos: prioridade, abertura e resolução de cada chamado resolvido.</summary>
    Task<IReadOnlyList<ResolucaoChamado>> ListarResolucoesAsync(CancellationToken cancellationToken = default);
}
