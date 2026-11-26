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
        return await db.Chamados.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
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
        var chamado = await db.Chamados.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ChamadoNaoEncontradoException(id);

        var versaoLida = chamado.Versao;
        alteracao(chamado);

        try
        {
            // UPDATE ... WHERE Id = @id AND Versao = @versaoLida, e o INSERT do novo registro de histórico,
            // na mesma transação: ou tudo grava, ou nada.
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

        // Projeção: o SELECT traz só as colunas do resumo, sem o histórico.
        return await consulta
            .Select(c => new ResumoChamado(c.Id, c.Titulo, c.Status, c.Prioridade, c.Responsavel, c.CriadoEm, c.Versao))
            .ToListAsync(cancellationToken);
    }

    public async Task<EstatisticasChamados> ObterEstatisticasAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        // GROUP BY no banco: só as contagens trafegam, não os chamados.
        var contagemStatus = await db.Chamados
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);

        var porStatus = Enum.GetValues<StatusChamado>().ToDictionary(
            status => status,
            status => contagemStatus.FirstOrDefault(x => x.Status == status)?.Total ?? 0);

        var porPrioridade = await db.Chamados
            .GroupBy(c => c.Prioridade)
            .Select(g => new { Prioridade = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Prioridade, x => x.Total, cancellationToken);

        // Subtrair datas não tem tradução para SQL no SQLite: trazemos só as duas colunas e calculamos aqui.
        var datas = await db.Chamados
            .Where(c => c.Status == StatusChamado.Resolvido)
            .Select(c => new { c.CriadoEm, c.EncerradoEm })
            .ToListAsync(cancellationToken);

        TimeSpan? tempoMedio = datas.Count == 0
            ? null
            : TimeSpan.FromTicks((long)datas.Average(d => (d.EncerradoEm!.Value - d.CriadoEm).Ticks));

        return new EstatisticasChamados(porStatus.Values.Sum(), porStatus, porPrioridade, tempoMedio);
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

        return chamados;
    }

    private static IQueryable<Chamado> Ordenar(IQueryable<Chamado> chamados, OrdemChamados ordem) => ordem switch
    {
        OrdemChamados.MaisRecentes => chamados.OrderByDescending(c => c.CriadoEm),
        OrdemChamados.MaisAntigos => chamados.OrderBy(c => c.CriadoEm),
        OrdemChamados.Titulo => chamados.OrderBy(c => c.Titulo),
        _ => chamados.OrderByDescending(c => c.Prioridade).ThenBy(c => c.CriadoEm)
    };

    private static string EscaparLike(string texto)
        => texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
