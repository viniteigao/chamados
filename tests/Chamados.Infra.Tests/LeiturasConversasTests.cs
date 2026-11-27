using System.Data.Common;
using System.Text.RegularExpressions;
using Chamados.Domain;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chamados.Infra.Tests;

/// <summary>
/// Mensagens não lidas no banco: a regra de MensagensNaoLidas traduzida para SQL (contagem e filtro),
/// a gravação da leitura (só avança, mesmo com outra requisição gravando ao mesmo tempo) e a migration
/// LeiturasDasConversas, que marca como lida a conversa de quem já tinha escrito nela.
/// </summary>
public partial class LeiturasConversasTests
{
    private static readonly DateTimeOffset Abertura = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly Autor Bruno = new("id-bruno", "Bruno");
    private static readonly Autor Carla = new("id-carla", "Carla");
    private static readonly Autor Ana = new("id-ana", "Ana");
    private static readonly Autor Davi = new("id-davi", "Davi");

    [Fact]
    public async Task Nao_lidas_sao_contadas_e_filtradas_no_banco_com_a_mesma_regra_do_dominio()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);
        var conversa = new Chamado("Tomada solta", criadoEm: Abertura, abertoPor: Bruno);
        conversa.IniciarAtendimento(Ana, Abertura.AddMinutes(1), Ana);
        var primeira = conversa.Comentar("Chego às 10h.", Abertura.AddMinutes(2), Ana);
        conversa.Comentar("Combinado.", Abertura.AddMinutes(3), Bruno);
        conversa.Comentar("Cheguei.", Abertura.AddMinutes(4), Ana);
        var semConversa = new Chamado("Lâmpada queimada", criadoEm: Abertura, abertoPor: Bruno);
        var daCarla = new Chamado("Wi-Fi instável", criadoEm: Abertura, abertoPor: Carla);
        daCarla.Comentar("Cai a cada 15 minutos.", Abertura.AddMinutes(5), Carla);
        foreach (var chamado in new[] { conversa, semConversa, daCarla })
        {
            await repositorio.AdicionarAsync(chamado);
        }

        await repositorio.MarcarLeituraAsync(conversa.Id, Bruno.Id, primeira.Em);   // o Bruno leu até a primeira
        var lido = (await repositorio.ObterAsync(conversa.Id))!;

        // Bruno: só a terceira (a segunda é dele). Ana: a do Bruno (ela nunca marcou leitura). Davi e
        // Carla não acompanham a conversa. Na conversa da Carla, só ela acompanha, e a mensagem é dela.
        foreach (var (leitor, lidoAte, esperado) in new[] { (Bruno, primeira.Em, 1), (Ana, (DateTimeOffset?)null, 1), (Davi, null, 0), (Carla, null, 0) })
        {
            var resumos = await repositorio.ListarAsync(new FiltroChamados(LeitorId: leitor.Id));
            Assert.Equal(esperado, resumos.Single(c => c.Id == conversa.Id).ComentariosNaoLidos);
            Assert.Equal(lido.NaoLidasPor(leitor.Id, lidoAte).Count, esperado);   // a mesma regra do domínio
            Assert.Equal(0, resumos.Single(c => c.Id == semConversa.Id).ComentariosNaoLidos);
            Assert.Equal(0, resumos.Single(c => c.Id == daCarla.Id).ComentariosNaoLidos);
        }

        var comNovas = await repositorio.ListarAsync(new FiltroChamados(LeitorId: Bruno.Id, SomenteComNaoLidas: true));
        Assert.Equal(conversa.Id, Assert.Single(comNovas).Id);
        Assert.Equal(1, await repositorio.ContarAsync(new FiltroChamados(LeitorId: Bruno.Id, SomenteComNaoLidas: true)));

        // Sem leitor: o resumo não traz contagem, e o filtro não traz nenhum chamado.
        Assert.All(await repositorio.ListarAsync(FiltroChamados.Todos), c => Assert.Null(c.ComentariosNaoLidos));
        Assert.Equal(0, await repositorio.ContarAsync(new FiltroChamados(SomenteComNaoLidas: true)));

        // Lida até a última: nada mais é novo para o Bruno.
        await repositorio.MarcarLeituraAsync(conversa.Id, Bruno.Id, lido.Comentarios[^1].Em);
        Assert.Empty(await repositorio.ListarAsync(new FiltroChamados(LeitorId: Bruno.Id, SomenteComNaoLidas: true)));
    }

    [Fact]
    public async Task Leitura_so_avanca_e_cada_usuario_tem_a_sua()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura, abertoPor: Bruno);
        await repositorio.AdicionarAsync(chamado);

        await repositorio.MarcarLeituraAsync(chamado.Id, Bruno.Id, Abertura.AddMinutes(2));
        await repositorio.MarcarLeituraAsync(chamado.Id, Bruno.Id, Abertura.AddMinutes(1));   // aba antiga: não volta
        var depoisDaAbaAntiga = await repositorio.ObterLeituraAsync(chamado.Id, Bruno.Id);
        await repositorio.MarcarLeituraAsync(chamado.Id, Bruno.Id, Abertura.AddMinutes(3));

        Assert.Equal(Abertura.AddMinutes(2), depoisDaAbaAntiga);
        Assert.Equal(Abertura.AddMinutes(3), await repositorio.ObterLeituraAsync(chamado.Id, Bruno.Id));
        Assert.Null(await repositorio.ObterLeituraAsync(chamado.Id, Ana.Id));
    }

    [Fact]
    public async Task Leitura_de_chamado_inexistente_e_recusada()
    {
        await using var banco = await BancoEmMemoria.CriarAsync();
        var repositorio = new RepositorioChamadosEf(banco);

        await Assert.ThrowsAsync<ChamadoNaoEncontradoException>(
            () => repositorio.MarcarLeituraAsync(Guid.NewGuid(), Bruno.Id, Abertura));
    }

    [Theory]
    [InlineData(-5, 0)]   // a outra aba leu menos: a leitura avança até a desta
    [InlineData(5, 5)]    // a outra aba leu mais: fica a dela
    public async Task Leitura_gravada_por_outra_requisicao_no_meio_da_marcacao_fica_com_a_mais_recente(
        int minutosDaOutraAba, int minutosEsperados)
    {
        var desta = Abertura.AddMinutes(10);
        var outraAba = new OutraAbaGravaAntes(desta.AddMinutes(minutosDaOutraAba));
        await using var banco = await BancoEmMemoria.CriarAsync(outraAba);
        var repositorio = new RepositorioChamadosEf(banco);
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura, abertoPor: Bruno);
        await repositorio.AdicionarAsync(chamado);

        // Nenhuma leitura ainda: o repositório tenta o INSERT, e a "outra aba" grava a dela logo antes.
        outraAba.Armada = true;
        await repositorio.MarcarLeituraAsync(chamado.Id, Bruno.Id, desta);

        Assert.True(outraAba.Gravou);
        Assert.Equal(desta.AddMinutes(minutosEsperados), await repositorio.ObterLeituraAsync(chamado.Id, Bruno.Id));
    }

    [Fact]
    public async Task Migration_marca_como_lida_a_conversa_de_quem_ja_tinha_escrito()
    {
        // Um banco de antes das leituras, com uma conversa gravada.
        await using var banco = await BancoEmMemoria.CriarAteAsync("20261127064620_ResponsavelAtendente");
        var repositorio = new RepositorioChamadosEf(banco);
        var chamado = new Chamado("Tomada solta", criadoEm: Abertura, abertoPor: Bruno);
        chamado.Comentar("Chego às 10h.", Abertura.AddMinutes(1), Ana);
        var doBruno = chamado.Comentar("Combinado.", Abertura.AddMinutes(2), Bruno);
        var ultimaDaAna = chamado.Comentar("Cheguei.", Abertura.AddMinutes(3), Ana);
        await repositorio.AdicionarAsync(chamado);

        await banco.MigrarAsync();

        // Cada autor leu até a própria última mensagem; quem não escreveu não tem leitura.
        Assert.Equal(ultimaDaAna.Em, await repositorio.ObterLeituraAsync(chamado.Id, Ana.Id));
        Assert.Equal(doBruno.Em, await repositorio.ObterLeituraAsync(chamado.Id, Bruno.Id));
        Assert.Null(await repositorio.ObterLeituraAsync(chamado.Id, Carla.Id));
        var paraBruno = await repositorio.ListarAsync(new FiltroChamados(LeitorId: Bruno.Id));
        Assert.Equal(1, Assert.Single(paraBruno).ComentariosNaoLidos);   // só a última da Ana
    }

    /// <summary>
    /// Simula outra requisição do mesmo usuário (outra aba) gravando a leitura entre a consulta do
    /// repositório e o INSERT dele: logo antes do INSERT em LeiturasConversas, grava a mesma linha com
    /// outro instante, na mesma conexão. O INSERT do repositório então bate na chave primária.
    /// Começa desarmado: a migration também faz um INSERT nessa tabela (o das conversas antigas).
    /// </summary>
    private sealed partial class OutraAbaGravaAntes(DateTimeOffset lidoAte) : DbCommandInterceptor
    {
        private static readonly DateTimeOffsetToBinaryConverter Conversor = new();

        public bool Armada { get; set; }

        public bool Gravou { get; private set; }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            await GravarAntesAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await GravarAntesAsync(command, cancellationToken);
            return result;
        }

        // SQL fixo (CA2100 vale também nos testes): só os valores vêm do INSERT do repositório.
        private const string GravacaoDaOutraAba =
            """INSERT INTO "LeiturasConversas" ("ChamadoId", "UsuarioId", "LidoAte") VALUES ($chamado, $usuario, $lidoAte)""";

        [GeneratedRegex("""INSERT INTO "LeiturasConversas" \(([^)]*)\)""")]
        private static partial Regex InsertDeLeitura();

        private async Task GravarAntesAsync(DbCommand comando, CancellationToken cancellationToken)
        {
            var insert = InsertDeLeitura().Match(comando.CommandText);
            if (!Armada || Gravou || !insert.Success)
            {
                return;
            }

            Gravou = true;

            // O chamado e o usuário do INSERT do repositório: os parâmetros seguem a ordem das colunas.
            var colunas = insert.Groups[1].Value.Split(',').Select(c => c.Trim().Trim('"')).ToList();
            await using var outro = comando.Connection!.CreateCommand();
            outro.Transaction = comando.Transaction;
            outro.CommandText = GravacaoDaOutraAba;
            Parametro(outro, "$chamado", comando.Parameters[colunas.IndexOf("ChamadoId")].Value);
            Parametro(outro, "$usuario", comando.Parameters[colunas.IndexOf("UsuarioId")].Value);
            Parametro(outro, "$lidoAte", Conversor.ConvertToProvider(lidoAte));

            await outro.ExecuteNonQueryAsync(cancellationToken);
        }

        private static void Parametro(DbCommand comando, string nome, object? valor)
        {
            var parametro = comando.CreateParameter();
            parametro.ParameterName = nome;
            parametro.Value = valor;
            comando.Parameters.Add(parametro);
        }
    }
}
