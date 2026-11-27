using System.Net;
using System.Text.RegularExpressions;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chamados.Web.Tests;

/// <summary>
/// Gestão de usuários: cadastro pela coordenação, perfil, desativação, senha e "Meu perfil".
/// Os testes que mudam contas usam usuários novos, cadastrados aqui: ana, bruno e carla ficam como estão.
/// </summary>
public sealed partial class GestaoDeUsuariosTests(FabricaChamados fabrica) : IClassFixture<FabricaChamados>
{
    // Senha dos usuários cadastrados nestes testes: nova a cada execução, como a da fábrica.
    private static readonly string SenhaInicial = $"Inicial#1{Guid.NewGuid():N}";

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenAntifalsificacao();

    [Fact]
    public async Task So_a_coordenacao_abre_a_gestao_de_usuarios()
    {
        var carla = await fabrica.ClienteConectadoAsync(Acesso.Coordenadora);
        var ana = await fabrica.ClienteConectadoAsync(Acesso.Atendente);
        var bruno = await fabrica.ClienteConectadoAsync(Acesso.Solicitante);

        var lista = WebUtility.HtmlDecode(await carla.GetStringAsync(Rota("/Usuarios")));
        var paraAna = await ana.GetAsync(Rota("/Usuarios"));
        var paraBruno = await bruno.GetAsync(Rota("/Usuarios/Novo"));
        var menuDaCarla = await carla.GetStringAsync(Rota("/"));
        var menuDaAna = await ana.GetStringAsync(Rota("/"));

        Assert.Contains("<td>Atende chamados</td>", lista, StringComparison.Ordinal);   // ana
        Assert.Contains("<td>Abre chamados</td>", lista, StringComparison.Ordinal);     // bruno
        Assert.Contains("<td>Coordenação</td>", lista, StringComparison.Ordinal);       // carla
        Assert.Contains("/Conta/AcessoNegado", paraAna.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Contains("/Conta/AcessoNegado", paraBruno.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Contains("href=\"/Usuarios\">Usuários</a>", menuDaCarla, StringComparison.Ordinal);
        Assert.DoesNotContain(">Usuários</a>", menuDaAna, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Coordenacao_cadastra_quem_entra_pelo_email_e_aparece_pelo_nome()
    {
        var carla = await fabrica.ClienteConectadoAsync(Acesso.Coordenadora);

        var cadastro = await CadastrarAsync(carla, "Diego", "diego@chamados.local");
        var (diego, login) = await EntrarAsync("diego@chamados.local", SenhaInicial);
        var inicio = await diego.GetStringAsync(Rota("/"));
        var gestao = await diego.GetAsync(Rota("/Usuarios"));

        Assert.Equal(HttpStatusCode.Redirect, cadastro.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Contains("title=\"diego@chamados.local\">Diego</a>", inicio, StringComparison.Ordinal);
        Assert.Contains("/Conta/AcessoNegado", gestao.Headers.Location!.ToString(), StringComparison.Ordinal);   // só abre chamados
        Assert.Contains(fabrica.Registros.Todos, r => r.EventoId == 1010);
    }

    [Fact]
    public async Task Erros_do_cadastro_aparecem_em_portugues_e_nada_e_criado()
    {
        var carla = await fabrica.ClienteConectadoAsync(Acesso.Coordenadora);

        var repetido = await HtmlAsync(await CadastrarAsync(carla, "Outro Bruno", Acesso.Solicitante));
        var fraca = await HtmlAsync(await CadastrarAsync(carla, "Elaine", "elaine@chamados.local", senha: "senhafraca1"));

        Assert.Contains($"Já existe um usuário com o e-mail {Acesso.Solicitante}.", repetido, StringComparison.Ordinal);
        Assert.DoesNotContain("Já existe um usuário com o login", repetido, StringComparison.Ordinal);   // o erro repetido sai
        Assert.Contains("A senha deve ter pelo menos uma letra maiúscula.", fraca, StringComparison.Ordinal);
        Assert.Contains("A senha deve ter pelo menos um símbolo", fraca, StringComparison.Ordinal);
        Assert.Null(await UsuarioAsync("elaine@chamados.local"));
    }

    [Fact]
    public async Task Mudar_o_acesso_vale_no_proximo_login_e_derruba_o_token_antigo()
    {
        var carla = await fabrica.ClienteConectadoAsync(Acesso.Coordenadora);
        await CadastrarAsync(carla, "Fabio", "fabio@chamados.local");
        var tokenAntigo = await fabrica.ClienteComTokenAsync("fabio@chamados.local", SenhaInicial);
        var antes = await tokenAntigo.GetAsync(Rota("/api/chamados"));
        var pagina = $"/Usuarios/Editar/{(await UsuarioAsync("fabio@chamados.local"))!.Id}";

        var salvar = await carla.PostAsync(Rota(pagina), await FormularioAsync(carla, pagina, new()
        {
            ["Perfil.Nome"] = "Fábio",
            ["Perfil.Atende"] = "true",
        }));
        var depois = await tokenAntigo.GetAsync(Rota("/api/chamados"));
        var (fabio, _) = await EntrarAsync("fabio@chamados.local", SenhaInicial);
        var fila = await fabio.GetAsync(Rota("/atendimento"));

        Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, salvar.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, depois.StatusCode);   // o token de antes não vale mais
        Assert.Equal(HttpStatusCode.OK, fila.StatusCode);                // no login novo, já atende
        Assert.Contains(fabrica.Registros.Todos, r => r.EventoId == 1011);
        Assert.Contains(fabrica.Registros.Todos, r => r.EventoId == 1016);
    }

    [Fact]
    public async Task Conta_desativada_nao_entra_perde_o_token_e_pode_ser_reativada()
    {
        var carla = await fabrica.ClienteConectadoAsync(Acesso.Coordenadora);
        await CadastrarAsync(carla, "Gabriela", "gabriela@chamados.local");
        var token = await fabrica.ClienteComTokenAsync("gabriela@chamados.local", SenhaInicial);
        var pagina = $"/Usuarios/Editar/{(await UsuarioAsync("gabriela@chamados.local"))!.Id}";

        var desativar = await carla.PostAsync(Rota($"{pagina}?handler=Desativar"), await FormularioAsync(carla, pagina, []));
        var tokenDepois = await token.GetAsync(Rota("/api/chamados"));
        var (_, senhaCerta) = await EntrarAsync("gabriela@chamados.local", SenhaInicial);
        var (_, senhaErrada) = await EntrarAsync("gabriela@chamados.local", "Errada#1234567");
        var lista = WebUtility.HtmlDecode(await carla.GetStringAsync(Rota("/Usuarios")));
        var reativar = await carla.PostAsync(Rota($"{pagina}?handler=Reativar"), await FormularioAsync(carla, pagina, []));
        var (_, depoisDeReativar) = await EntrarAsync("gabriela@chamados.local", SenhaInicial);

        Assert.Equal(HttpStatusCode.Redirect, desativar.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, tokenDepois.StatusCode);
        Assert.Contains("Esta conta foi desativada. Fale com a coordenação.", await HtmlAsync(senhaCerta), StringComparison.Ordinal);
        // Sem a senha certa, nada indica que a conta existe ou que foi desativada.
        var errada = await HtmlAsync(senhaErrada);
        Assert.Contains("E-mail ou senha incorretos.", errada, StringComparison.Ordinal);
        Assert.DoesNotContain("desativada", errada, StringComparison.Ordinal);
        Assert.Contains("<span class=\"situacao-desativado\">Desativado</span>", lista, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, reativar.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, depoisDeReativar.StatusCode);
        Assert.Contains(fabrica.Registros.Todos, r => r.EventoId == 1017);
    }

    [Fact]
    public async Task Coordenadora_nao_tira_a_propria_coordenacao_nem_se_desativa()
    {
        var carla = await fabrica.ClienteConectadoAsync(Acesso.Coordenadora);
        var pagina = $"/Usuarios/Editar/{(await UsuarioAsync(Acesso.Coordenadora))!.Id}";

        var tela = WebUtility.HtmlDecode(await carla.GetStringAsync(Rota(pagina)));
        // POSTs montados à mão: a tela não oferece nenhum dos dois.
        var tirar = await HtmlAsync(await carla.PostAsync(Rota(pagina),
            await FormularioAsync(carla, pagina, new() { ["Perfil.Nome"] = Acesso.NomeCoordenadora })));
        var desativar = await HtmlAsync(await carla.PostAsync(Rota($"{pagina}?handler=Desativar"),
            await FormularioAsync(carla, pagina, [])));

        Assert.DoesNotContain("Desativar conta", tela, StringComparison.Ordinal);
        Assert.Contains("<li>Você não pode tirar a sua própria coordenação. Peça a outra pessoa da coordenação.</li>", tirar,
            StringComparison.Ordinal);
        Assert.Contains("<li>Você não pode desativar a sua própria conta.</li>", desativar, StringComparison.Ordinal);

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var conta = (await usuarios.FindByEmailAsync(Acesso.Coordenadora))!;
        Assert.Contains(await usuarios.GetClaimsAsync(conta),
            c => c.Type == Politicas.ClaimCargo && c.Value == Politicas.CargoCoordenacao);
        Assert.False(GestaoDeUsuarios.EstaDesativado(conta, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Coordenacao_redefine_a_senha_de_quem_esqueceu()
    {
        var carla = await fabrica.ClienteConectadoAsync(Acesso.Coordenadora);
        await CadastrarAsync(carla, "Heitor", "heitor@chamados.local");
        var pagina = $"/Usuarios/Editar/{(await UsuarioAsync("heitor@chamados.local"))!.Id}";
        var outra = $"Outra#2{Guid.NewGuid():N}";

        var redefinir = await carla.PostAsync(Rota($"{pagina}?handler=RedefinirSenha"), await FormularioAsync(carla, pagina, new()
        {
            ["NovaSenha.Senha"] = outra,
            ["NovaSenha.Confirmacao"] = outra,
        }));
        var (_, comAAntiga) = await EntrarAsync("heitor@chamados.local", SenhaInicial);
        var (_, comANova) = await EntrarAsync("heitor@chamados.local", outra);

        Assert.Equal(HttpStatusCode.Redirect, redefinir.StatusCode);
        Assert.Equal(HttpStatusCode.OK, comAAntiga.StatusCode);   // volta ao formulário, com erro
        Assert.Equal(HttpStatusCode.Redirect, comANova.StatusCode);
        Assert.Contains(fabrica.Registros.Todos, r => r.EventoId == 1014);
    }

    [Fact]
    public async Task Meu_perfil_troca_o_nome_na_hora_e_a_senha_so_com_a_senha_atual()
    {
        var carla = await fabrica.ClienteConectadoAsync(Acesso.Coordenadora);
        await CadastrarAsync(carla, "Iara", "iara@chamados.local");
        var (iara, _) = await EntrarAsync("iara@chamados.local", SenhaInicial);
        var outra = $"Outra#3{Guid.NewGuid():N}";

        var nome = await iara.PostAsync(Rota("/Perfil?handler=Nome"),
            await FormularioAsync(iara, "/Perfil", new() { ["Nome.Nome"] = "Iara Souza" }));
        var cabecalho = WebUtility.HtmlDecode(await iara.GetStringAsync(Rota("/Perfil")));
        var senhaErrada = await HtmlAsync(await iara.PostAsync(Rota("/Perfil?handler=Senha"), await FormularioAsync(iara, "/Perfil", new()
        {
            ["Senha.Atual"] = "Errada#1234567",
            ["Senha.Nova"] = outra,
            ["Senha.Confirmacao"] = outra,
        })));
        var senhaCerta = await iara.PostAsync(Rota("/Perfil?handler=Senha"), await FormularioAsync(iara, "/Perfil", new()
        {
            ["Senha.Atual"] = SenhaInicial,
            ["Senha.Nova"] = outra,
            ["Senha.Confirmacao"] = outra,
        }));
        var (_, comANova) = await EntrarAsync("iara@chamados.local", outra);

        Assert.Equal(HttpStatusCode.Redirect, nome.StatusCode);
        Assert.Contains("title=\"iara@chamados.local\">Iara Souza</a>", cabecalho, StringComparison.Ordinal);
        Assert.Contains("Senha incorreta.", senhaErrada, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, senhaCerta.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, comANova.StatusCode);
        Assert.Contains(fabrica.Registros.Todos, r => r.EventoId == 1015);
    }

    [Fact]
    public async Task Primeiro_coordenador_vem_da_configuracao_uma_vez_so()
    {
        await PrimeiroCoordenador.GarantirAsync(fabrica.Services, Configuracao(new()
        {
            ["PrimeiroCoordenador:Email"] = "joana@chamados.local",
            ["PrimeiroCoordenador:Nome"] = "Joana",
            ["PrimeiroCoordenador:Senha"] = SenhaInicial,
        }));
        // Num segundo início, já sem a senha na configuração: a conta existe, nada muda e nada falha.
        await PrimeiroCoordenador.GarantirAsync(fabrica.Services, Configuracao(new()
        {
            ["PrimeiroCoordenador:Email"] = "joana@chamados.local",
        }));

        var (joana, login) = await EntrarAsync("joana@chamados.local", SenhaInicial);
        var gestao = await joana.GetAsync(Rota("/Usuarios"));
        var inicio = await joana.GetStringAsync(Rota("/"));

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, gestao.StatusCode);
        Assert.Contains(">Joana</a>", inicio, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Primeiro_coordenador_sem_configuracao_nao_faz_nada_e_sem_senha_explica_o_que_falta()
    {
        await PrimeiroCoordenador.GarantirAsync(fabrica.Services, Configuracao([]));

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => PrimeiroCoordenador.GarantirAsync(
            fabrica.Services, Configuracao(new() { ["PrimeiroCoordenador:Email"] = "kaique@chamados.local" })));

        Assert.Contains("PrimeiroCoordenador:Senha", erro.Message, StringComparison.Ordinal);
        Assert.Null(await UsuarioAsync("kaique@chamados.local"));
    }

    private static Uri Rota(string caminho) => new(caminho, UriKind.Relative);

    private static IConfiguration Configuracao(Dictionary<string, string?> valores)
        => new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    private static async Task<string> HtmlAsync(HttpResponseMessage resposta)
        => WebUtility.HtmlDecode(await resposta.Content.ReadAsStringAsync());

    // Cadastro pela tela da coordenação, como no navegador.
    private static async Task<HttpResponseMessage> CadastrarAsync(HttpClient coordenacao, string nome, string email,
        string? senha = null)
    {
        senha ??= SenhaInicial;
        return await coordenacao.PostAsync(Rota("/Usuarios/Novo"), await FormularioAsync(coordenacao, "/Usuarios/Novo", new()
        {
            ["Input.Nome"] = nome,
            ["Input.Email"] = email,
            ["Input.Senha"] = senha,
            ["Input.ConfirmacaoSenha"] = senha,
        }));
    }

    // Login pelo formulário com uma senha qualquer; devolve o cliente e a resposta (302 = entrou).
    private async Task<(HttpClient Cliente, HttpResponseMessage Resposta)> EntrarAsync(string email, string senha)
    {
        var cliente = fabrica.ClienteAnonimo();
        var resposta = await cliente.PostAsync(Rota("/Conta/Entrar"),
            await Acesso.FormularioDeLoginAsync(cliente, email, senha, "/"));
        return (cliente, resposta);
    }

    private async Task<IdentityUser?> UsuarioAsync(string email)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email);
    }

    // Formulário de uma página com o token antifalsificação tirado dela, como o navegador faz.
    private static async Task<FormUrlEncodedContent> FormularioAsync(HttpClient cliente, string pagina,
        Dictionary<string, string> campos)
    {
        var html = await cliente.GetStringAsync(Rota(pagina));
        campos["__RequestVerificationToken"] = WebUtility.HtmlDecode(TokenAntifalsificacao().Match(html).Groups[1].Value);
        return new FormUrlEncodedContent(campos);
    }
}
