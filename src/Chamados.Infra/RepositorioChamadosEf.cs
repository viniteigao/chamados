using Chamados.Domain;
using Microsoft.EntityFrameworkCore;

namespace Chamados.Infra;

/// <summary>
/// Repositório de chamados sobre o EF Core. Cada método cria e descarta o próprio DbContext:
/// no Blazor Server um mesmo circuito pode fazer duas operações ao mesmo tempo
/// (a fila e o resumo do cabeçalho), e um DbContext não aceita uso simultâneo.
/// </summary>
public sealed class RepositorioChamadosEf(IDbContextFactory<ChamadosDbContext> fabrica) : IChamadoRepositorio
{
    public async Task<Chamado?> ObterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        // Só leitura: AsNoTracking dispensa o EF de guardar uma cópia para detectar mudanças.
        // O histórico vem junto sem Include: entidade "owned" é carregada sempre com o dono.
        // Histórico das edições: com três coleções (histórico, conversa e edições), um JOIN só
        // multiplicaria as linhas (cada registro repetido a cada mensagem e a cada edição).
        // AsSplitQuery faz uma consulta por tabela; a ordem cronológica das listas é garantida
        // pelo próprio Chamado, que não depende da ordem em que o banco devolve as linhas.
        return await db.Chamados.AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task AdicionarAsync(Chamado chamado, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chamado);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        db.Chamados.Add(chamado);
        await db.SaveChangesAsync(cancellationToken);   // um INSERT por tabela, numa transação
    }

    public async Task<Chamado> AlterarAsync(Guid id, Action<Chamado> alteracao, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alteracao);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        // Com rastreamento: o EF guarda os valores lidos e, no SaveChanges, grava só o que mudou.
        var chamado = await db.Chamados.AsSplitQuery().FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ChamadoNaoEncontradoException(id);

        var versaoLida = chamado.Versao;
        alteracao(chamado);

        try
        {
            // UPDATE ... WHERE Id = @id AND Versao = @versaoLida, e o INSERT do novo registro de histórico,
            // na mesma transação: ou tudo grava, ou nada. Um comentário não muda a versão: grava só o
            // INSERT da mensagem, sem conferir versão, e não briga com quem está editando.
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Nenhuma linha atendeu ao WHERE: alguém gravou depois da nossa leitura (ou apagou o chamado).
            var versaoAtual = await db.Chamados.AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => (int?)c.Versao)
                .FirstOrDefaultAsync(cancellationToken);

            throw versaoAtual is { } atual
                ? new ConcorrenciaException(id, versaoLida, atual)
                : new ChamadoNaoEncontradoException(id);
        }

        return chamado;
    }

    public async Task<int> ContarAsync(FiltroChamados filtro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        return await Filtrar(db.Chamados, filtro).CountAsync(cancellationToken);   // SELECT COUNT(*)
    }

    public async Task<IReadOnlyList<ResumoChamado>> ListarAsync(
        FiltroChamados filtro, int pular = 0, int? pegar = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        var consulta = Ordenar(Filtrar(db.Chamados.AsNoTracking(), filtro), filtro.Ordem);
        if (pular > 0)
        {
            consulta = consulta.Skip(pular);
        }

        if (pegar is { } quantidade)
        {
            consulta = consulta.Take(quantidade);   // LIMIT/OFFSET no SQL
        }

        // Projeção: o SELECT traz só as colunas do resumo, sem o histórico nem a conversa.
        return await consulta
            .Select(ParaResumo)
            .ToListAsync(cancellationToken);
    }

    public async Task<EstatisticasChamados> ObterEstatisticasAsync(string? abertoPorId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        // Meus chamados: com abertoPorId, as três consultas abaixo ganham o mesmo WHERE.
        IQueryable<Chamado> chamados = db.Chamados;
        if (abertoPorId is not null)
        {
            chamados = chamados.Where(c => c.AbertoPorId == abertoPorId);
        }

        // GROUP BY no banco: só as contagens trafegam, não os chamados.
        var contagemStatus = await chamados
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);

        var porStatus = Enum.GetValues<StatusChamado>().ToDictionary(
            status => status,
            status => contagemStatus.FirstOrDefault(x => x.Status == status)?.Total ?? 0);

        var porPrioridade = await chamados
            .GroupBy(c => c.Prioridade)
            .Select(g => new { Prioridade = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Prioridade, x => x.Total, cancellationToken);

        // Subtrair datas não tem tradução para SQL no SQLite: trazemos só as duas colunas e calculamos aqui.
        var datas = await chamados
            .Where(c => c.Status == StatusChamado.Resolvido)
            .Select(c => new { c.CriadoEm, c.EncerradoEm })
            .ToListAsync(cancellationToken);

        TimeSpan? tempoMedio = datas.Count == 0
            ? null
            : TimeSpan.FromTicks((long)datas.Average(d => (d.EncerradoEm!.Value - d.CriadoEm).Ticks));

        return new EstatisticasChamados(porStatus.Values.Sum(), porStatus, porPrioridade, tempoMedio);
    }

    public async Task<IReadOnlyList<ResumoChamado>> ListarPorResponsavelAsync(
        string responsavel, string? abertoPorId = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responsavel);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        // Módulo 8: SQL escrito à mão, e mesmo assim seguro. FromSql recebe uma string interpolada
        // (FormattableString) e transforma cada {valor} num parâmetro (@p0): o texto digitado nunca
        // vira parte do comando. A versão vulnerável, com FromSqlRaw e concatenação, está no roteiro.
        var consulta = db.Chamados
            .FromSql($"SELECT * FROM \"Chamados\" WHERE \"Responsavel\" = {responsavel}")
            .AsNoTracking();

        // Meus chamados: o LINQ compõe em volta do SQL escrito à mão; o dono também vira parâmetro.
        if (abertoPorId is not null)
        {
            consulta = consulta.Where(c => c.AbertoPorId == abertoPorId);
        }

        return await consulta
            .OrderByDescending(c => c.Prioridade).ThenBy(c => c.CriadoEm)
            .Select(ParaResumo)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContagemPorResponsavel>> ContarPorResponsavelAsync(FiltroChamados filtro,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        // Responsável atendente: GROUP BY no banco; só as contagens trafegam. O nome é o gravado nos
        // chamados (MAX escolhe um, caso o mesmo atendente tenha sido gravado com nomes diferentes).
        return await Filtrar(db.Chamados.AsNoTracking(), filtro)
            .GroupBy(c => c.ResponsavelId)
            .Select(g => new ContagemPorResponsavel(g.Key, g.Max(c => c.Responsavel), g.Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResolucaoChamado>> ListarResolucoesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        // Prazos: três colunas dos resolvidos. Comparar a resolução com o vencimento exige somar o
        // prazo à abertura, e o SQLite não faz conta com datas: a comparação acontece no serviço.
        return await db.Chamados
            .AsNoTracking()
            .Where(c => c.Status == StatusChamado.Resolvido)
            .Select(c => new ResolucaoChamado(c.Prioridade, c.CriadoEm, c.EncerradoEm!.Value))
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<Chamado> Filtrar(IQueryable<Chamado> chamados, FiltroChamados filtro)
    {
        // Cada Where vira uma condição no WHERE do SQL; nada é filtrado em memória.
        if (filtro.Status is { } status)
        {
            chamados = chamados.Where(c => c.Status == status);
        }

        if (filtro.PrioridadeMinima is { } minima)
        {
            chamados = chamados.Where(c => c.Prioridade >= minima);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            // LIKE com parâmetro: o texto digitado nunca é concatenado ao SQL.
            // % e _ digitados pelo usuário são escapados para valerem como texto.
            var padrao = "%" + EscaparLike(filtro.Texto.Trim()) + "%";
            chamados = chamados.Where(c => EF.Functions.Like(c.Titulo, padrao, "\\")
                || (c.Descricao != null && EF.Functions.Like(c.Descricao, padrao, "\\")));
        }

        if (filtro.AbertosAntesDe is { } cortes)
        {
            // Prazos: "vencido" vira comparação de datas no WHERE, com um corte por prioridade
            // (cada corte = agora − prazo da prioridade, calculado pelo serviço). Os quatro cortes
            // vão como parâmetros; a data gravada como número (Módulo 7) ordena e compara direito.
            // Uma prioridade nova no enum exige uma linha nova aqui (a política já exige o prazo dela).
            var baixa = cortes[Prioridade.Baixa];
            var media = cortes[Prioridade.Media];
            var alta = cortes[Prioridade.Alta];
            var critica = cortes[Prioridade.Critica];
            chamados = chamados.Where(c =>
                (c.Status == StatusChamado.Aberto || c.Status == StatusChamado.EmAtendimento)
                && ((c.Prioridade == Prioridade.Baixa && c.CriadoEm < baixa)
                    || (c.Prioridade == Prioridade.Media && c.CriadoEm < media)
                    || (c.Prioridade == Prioridade.Alta && c.CriadoEm < alta)
                    || (c.Prioridade == Prioridade.Critica && c.CriadoEm < critica)));
        }

        if (filtro.AbertoPorId is { } dono)
        {
            // Meus chamados: comparação exata, com índice na coluna (migration AutoriaDosChamados).
            chamados = chamados.Where(c => c.AbertoPorId == dono);
        }

        if (filtro.ResponsavelId is { } responsavel)
        {
            // Responsável atendente: "Meus atendimentos", comparação exata com índice.
            chamados = chamados.Where(c => c.ResponsavelId == responsavel);
        }

        return chamados;
    }

    private static IQueryable<Chamado> Ordenar(IQueryable<Chamado> chamados, OrdemChamados ordem) => ordem switch
    {
        OrdemChamados.MaisRecentes => chamados.OrderByDescending(c => c.CriadoEm),
        OrdemChamados.MaisAntigos => chamados.OrderBy(c => c.CriadoEm),
        OrdemChamados.Titulo => chamados.OrderBy(c => c.Titulo),
        _ => chamados.OrderByDescending(c => c.Prioridade).ThenBy(c => c.CriadoEm)
    };

    // A mesma projeção para as listas. Comentários: a quantidade de mensagens vira um
    // SELECT COUNT(*) correlacionado; as mensagens em si não saem do banco.
    private static readonly System.Linq.Expressions.Expression<Func<Chamado, ResumoChamado>> ParaResumo =
        c => new ResumoChamado(c.Id, c.Titulo, c.Status, c.Prioridade, c.Responsavel, c.CriadoEm, c.Versao,
            c.EncerradoEm, c.AbertoPorNome, c.Comentarios.Count);

    private static string EscaparLike(string texto)
        => texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
