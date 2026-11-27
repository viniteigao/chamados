using System.ComponentModel.DataAnnotations;
using Chamados.Domain;
using Chamados.Web.Apresentacao;
using Chamados.Web.Diagnostico;
using Chamados.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Chamados.Web.Pages.Chamados;

/// <summary>
/// Detalhe e mudança de status. Uma página, três formulários, três handlers nomeados:
/// asp-page-handler="Iniciar" chama OnPostIniciarAsync, e assim por diante.
/// Módulo 8: qualquer usuário conectado vê o chamado; só atendentes mudam o status.
/// Razor Pages não aceitam [Authorize] por handler, então a regra é conferida no código
/// (autorização imperativa), com a mesma política usada no resto do sistema.
/// Comentários: quem pode ver o chamado pode escrever na conversa (handler Comentar).
/// </summary>
public sealed class DetalhesModel(ChamadoServico servico, IAuthorizationService autorizacao,
    ILogger<DetalhesModel> logger) : PageModel
{
    private const string Obrigatorio = "Preencha este campo antes de confirmar.";
    private const string LongoDemais = "Use no máximo {1} caracteres.";

    public Chamado Chamado { get; private set; } = null!;

    /// <summary>A página mostra os formulários de transição só quando isto é verdadeiro.</summary>
    public bool PodeAtender { get; private set; }

    /// <summary>Prazos: o prazo do chamado agora; null para cancelados.</summary>
    public PrazoDoChamado? Prazo { get; private set; }

    /// <summary>Comentários: Id do usuário conectado, para a conversa alinhar à direita as mensagens dele.</summary>
    public string? UsuarioId { get; private set; }

    /// <summary>Comentários: o texto que voltou com erro, para não se perder ao mostrar a mensagem de erro.</summary>
    public string? TextoDigitado { get; private set; }

    /// <summary>
    /// Histórico das edições: mudanças de status (<see cref="RegistroHistorico"/>) e edições
    /// (<see cref="RegistroEdicao"/>) juntas, em ordem cronológica. Na mesma hora, o status vem primeiro.
    /// </summary>
    public IReadOnlyList<object> LinhaDoTempo =>
    [
        .. Chamado.Historico.Select(r => (r.Em, Item: (object)r))
            .Concat(Chamado.Edicoes.Select(e => (e.Em, Item: (object)e)))
            .OrderBy(x => x.Em)
            .Select(x => x.Item),
    ];

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await servico.ObterAsync(id, cancellationToken) is not { } chamado)
        {
            return NotFound();
        }

        // Meus chamados: chamado de outra pessoa responde 404, como se não existisse
        // (um "acesso negado" confirmaria que o Id existe). A tentativa fica no log.
        if (!User.PodeVer(chamado))
        {
            Registro.ChamadoDeOutroUsuario(logger, id, User.IdDoUsuario() ?? "desconhecido");
            return NotFound();
        }

        Chamado = chamado;
        Prazo = servico.PrazoDe(chamado);
        PodeAtender = (await autorizacao.AuthorizeAsync(User, Politicas.Atender)).Succeeded;
        UsuarioId = User.IdDoUsuario();
        return Page();
    }

    // Comentários: não exige a política Atender. Basta poder ver o chamado: o dono conversa com
    // quem atende. A conferência é a mesma do GET, feita de novo aqui, porque o POST pode ser montado à mão.
    public async Task<IActionResult> OnPostComentarAsync(Guid id,
        [Required(ErrorMessage = "Escreva a mensagem antes de enviar."),
         StringLength(Chamado.TamanhoMaximoComentario, ErrorMessage = LongoDemais)] string texto,
        CancellationToken cancellationToken)
    {
        if (await servico.ObterAsync(id, cancellationToken) is not { } chamado)
        {
            return NotFound();
        }

        if (!User.PodeVer(chamado))
        {
            Registro.ChamadoDeOutroUsuario(logger, id, User.IdDoUsuario() ?? "desconhecido");
            return NotFound();
        }

        if (User.ComoAutor() is not { } autor)
        {
            return Challenge();
        }

        if (ModelState.IsValid)
        {
            try
            {
                await servico.ComentarAsync(id, texto, autor, cancellationToken);
                Registro.ComentarioAdicionado(logger, id, autor.Id);

                // Post/Redirect/Get, voltando direto para a conversa (âncora #conversa).
                return RedirectToPage(pageName: null, pageHandler: null, routeValues: new { id }, fragment: "conversa");
            }
            catch (ChamadoNaoEncontradoException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(nameof(texto), ex.ParaUsuario());
            }
        }

        var pagina = await OnGetAsync(id, cancellationToken);
        TextoDigitado = texto;
        return pagina;
    }

    // Aqui o model binding preenche parâmetros do método, não uma [BindProperty]:
    // cada formulário envia um campo diferente.
    // Melhorias: o tamanho máximo vem das constantes do domínio, como no ChamadoInput.
    public Task<IActionResult> OnPostIniciarAsync(Guid id,
        [Required(ErrorMessage = Obrigatorio), StringLength(Chamado.TamanhoMaximoResponsavel, ErrorMessage = LongoDemais)] string responsavel,
        CancellationToken cancellationToken)
        => TransicionarAsync(id, "Iniciar", () => servico.IniciarAtendimentoAsync(id, responsavel, User.ComoAutor(), cancellationToken),
            "Atendimento iniciado.", cancellationToken);

    public Task<IActionResult> OnPostResolverAsync(Guid id,
        [Required(ErrorMessage = Obrigatorio), StringLength(Chamado.TamanhoMaximoJustificativa, ErrorMessage = LongoDemais)] string solucao,
        CancellationToken cancellationToken)
        => TransicionarAsync(id, "Resolver", () => servico.ResolverAsync(id, solucao, User.ComoAutor(), cancellationToken),
            "Chamado resolvido.", cancellationToken);

    public Task<IActionResult> OnPostCancelarAsync(Guid id,
        [Required(ErrorMessage = Obrigatorio), StringLength(Chamado.TamanhoMaximoJustificativa, ErrorMessage = LongoDemais)] string motivo,
        CancellationToken cancellationToken)
        => TransicionarAsync(id, "Cancelar", () => servico.CancelarAsync(id, motivo, User.ComoAutor(), cancellationToken),
            "Chamado cancelado.", cancellationToken);

    private async Task<IActionResult> TransicionarAsync(Guid id, string acao, Func<Task<Chamado>> transicao, string sucesso,
        CancellationToken cancellationToken)
    {
        // Esconder o formulário não basta: um POST pode ser montado à mão. A regra vale aqui.
        if (!(await autorizacao.AuthorizeAsync(User, Politicas.Atender)).Succeeded)
        {
            Registro.AcaoNegada(logger, acao, id, Politicas.Atender);
            return Forbid();   // logado sem permissão: o cookie leva para /Conta/AcessoNegado
        }

        if (ModelState.IsValid)
        {
            try
            {
                await transicao();
                TempData["Mensagem"] = sucesso;
                return RedirectToPage(new { id });
            }
            catch (ChamadoNaoEncontradoException)
            {
                return NotFound();
            }
            catch (TransicaoInvalidaException ex)
            {
                // Ex.: o chamado foi resolvido em outra aba enquanto esta estava aberta.
                ModelState.AddModelError(string.Empty,
                    $"O chamado está {ex.Atual.Rotulo().ToLowerInvariant()} e não pode passar para " +
                    $"{ex.Pretendido.Rotulo().ToLowerInvariant()}. A página foi atualizada.");
            }
            catch (ConcorrenciaException)
            {
                // Módulo 7: o banco recusou a gravação porque outra pessoa alterou o chamado antes.
                ModelState.AddModelError(string.Empty,
                    "Outra pessoa alterou este chamado ao mesmo tempo. A página foi atualizada; tente de novo.");
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(string.Empty, ex.ParaUsuario());
            }
        }

        // Erro: recarrega o chamado e mostra a mesma página com as mensagens.
        return await OnGetAsync(id, cancellationToken);
    }
}
