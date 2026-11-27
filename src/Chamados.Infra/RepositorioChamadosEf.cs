using Chamados.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Chamados.Infra;

/// <summary>
/// Repositório de chamados sobre o EF Core. Cada método cria e descarta o próprio DbContext:
/// no Blazor Server um mesmo circuito pode fazer duas operações ao mesmo tempo
/// (a fila e o resumo do cabeçalho), e um DbContext não aceita uso simultâneo.
/// </summary>
public sealed class RepositorioChamadosEf(IDbContextFactory<ChamadosDbContext> fabrica) : IChamadoRepositorio
{
    // Mensagens não lidas: códigos estendidos do SQLite para a violação de chave primária e de chave estrangeira.
    private const int ChaveRepetida = 1555;      // SQLITE_CONSTRAINT_PRIMARYKEY
    private const int ChamadoInexistente = 787;  // SQLITE_CONSTRAINT_FOREIGNKEY

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

        return await Filtrar(db.Chamados, filtro, db.Leituras).CountAsync(cancellationToken);   // SELECT COUNT(*)
    }

    public async Task<IReadOnlyList<ResumoChamado>> ListarAsync(
        FiltroChamados filtro, int pular = 0, int? pegar = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        var consulta = Ordenar(Filtrar(db.Chamados.AsNoTracking(), filtro, db.Leituras), filtro.Ordem);
        if (pular > 0)
        {
            consulta = consulta.Skip(pular);
        }

        if (pegar is { } quantidade)
        {
            consulta = consulta.Take(quantidade);   // LIMIT/OFFSET no SQL
        }

        // Projeção: o SELECT traz só as colunas do resumo, sem o histórico nem a conversa.
        // Mensagens não lidas: com leitor, a contagem das não lidas entra no mesmo SELECT.
        return await consulta
            .Select(filtro.LeitorId is { } leitor ? ParaResumoDoLeitor(db.Leituras, leitor) : ParaResumo)
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
        return await Filtrar(db.Chamados.AsNoTracking(), filtro, db.Leituras)
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

    public async Task<DateTimeOffset?> ObterLeituraAsync(Guid chamadoId, string usuarioId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        return await db.Leituras
            .AsNoTracking()
            .Where(l => l.ChamadoId == chamadoId && l.UsuarioId == usuarioId)
            .Select(l => (DateTimeOffset?)l.LidoAte)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task MarcarLeituraAsync(Guid chamadoId, string usuarioId, DateTimeOffset ate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        // Primeiro tenta avançar a leitura que já existe. Se nada mudou, ou ela não existe, ou já está
        // num instante igual ou posterior (outra aba leu depois): no segundo caso, não há o que fazer.
        if (await AvancarAsync(db, chamadoId, usuarioId, ate, cancellationToken) > 0
            || await db.Leituras.AnyAsync(l => l.ChamadoId == chamadoId && l.UsuarioId == usuarioId, cancellationToken))
        {
            return;
        }

        db.Leituras.Add(new LeituraConversa(chamadoId, usuarioId, ate));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: ChaveRepetida })
        {
            // Outra requisição do mesmo usuário gravou a leitura entre a consulta e o INSERT: avança a dela.
            await AvancarAsync(db, chamadoId, usuarioId, ate, cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: ChamadoInexistente })
        {
            throw new ChamadoNaoEncontradoException(chamadoId);
        }
    }

    // UPDATE ... SET LidoAte = @ate WHERE ChamadoId = @id AND UsuarioId = @usuario AND LidoAte < @ate.
    // A condição vai no próprio UPDATE: a leitura só avança, mesmo com duas abas gravando ao mesmo tempo.
    private static Task<int> AvancarAsync(ChamadosDbContext db, Guid chamadoId, string usuarioId, DateTimeOffset ate,
        CancellationToken cancellationToken)
        => db.Leituras
            .Where(l => l.ChamadoId == chamadoId && l.UsuarioId == usuarioId && l.LidoAte < ate)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.LidoAte, ate), cancellationToken);

    private static IQueryable<Chamado> Filtrar(IQueryable<Chamado> chamados, FiltroChamados filtro,
        IQueryable<LeituraConversa> leituras)
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

        if (filtro.SomenteComNaoLidas)
        {
            // Mensagens não lidas: a regra de MensagensNaoLidas, em SQL. O chamado entra se o leitor
            // acompanha a conversa (abriu, atende ou já escreveu) e se há mensagem de outra pessoa sem
            // leitura igual ou posterior a ela (NOT EXISTS na tabela LeiturasConversas). Sem leitor, nenhum.
            chamados = filtro.LeitorId is { } leitor
                ? chamados.Where(c =>
                    (c.AbertoPorId == leitor || c.ResponsavelId == leitor || c.Comentarios.Any(m => m.AutorId == leitor))
                    && c.Comentarios.Any(m => m.AutorId != leitor
                        && !leituras.Any(l => l.ChamadoId == c.Id && l.UsuarioId == leitor && l.LidoAte >= m.Em)))
                : chamados.Where(c => false);
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

    // Mensagens não lidas: a mesma projeção, mais as não lidas do leitor, num CASE com um COUNT(*)
    // correlacionado (a mesma regra do filtro SomenteComNaoLidas). Quem não acompanha a conversa: zero.
    private static System.Linq.Expressions.Expression<Func<Chamado, ResumoChamado>> ParaResumoDoLeitor(
        IQueryable<LeituraConversa> leituras, string leitor)
        => c => new ResumoChamado(c.Id, c.Titulo, c.Status, c.Prioridade, c.Responsavel, c.CriadoEm, c.Versao,
            c.EncerradoEm, c.AbertoPorNome, c.Comentarios.Count,
            c.AbertoPorId == leitor || c.ResponsavelId == leitor || c.Comentarios.Any(m => m.AutorId == leitor)
                ? c.Comentarios.Count(m => m.AutorId != leitor
                    && !leituras.Any(l => l.ChamadoId == c.Id && l.UsuarioId == leitor && l.LidoAte >= m.Em))
                : 0);

    private static string EscaparLike(string texto)
        => texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
