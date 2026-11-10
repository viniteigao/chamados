using System.ComponentModel.DataAnnotations;
using Chamados.Domain;

namespace Chamados.Web.Pages.Chamados;

/// <summary>
/// Campos que o usuário preenche para abrir ou editar um chamado.
/// No Módulo 3 se chamava NovoChamadoInput; agora serve às duas telas.
/// Não é a entidade: Chamado tem setters privados e nasce já válida.
/// </summary>
public sealed class ChamadoInput
{
    [Display(Name = "Título")]
    [Required(ErrorMessage = "Informe o título.")]
    [StringLength(Chamado.TamanhoMaximoTitulo, MinimumLength = 5,
        ErrorMessage = "O título deve ter entre {2} e {1} caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [Display(Name = "Prioridade")]
    [Required(ErrorMessage = "Escolha a prioridade.")]
    public Prioridade Prioridade { get; set; } = Prioridade.Media;

    [Display(Name = "Descrição")]
    [StringLength(Chamado.TamanhoMaximoDescricao, ErrorMessage = "A descrição deve ter no máximo {1} caracteres.")]
    public string? Descricao { get; set; }

    /// <summary>Preenche o formulário de edição com os dados atuais do chamado.</summary>
    public static ChamadoInput De(Chamado chamado) => new()
    {
        Titulo = chamado.Titulo,
        Prioridade = chamado.Prioridade,
        Descricao = chamado.Descricao
    };

    /// <summary>Converte o que veio da tela no comando de abertura.</summary>
    public NovoChamado ParaComando() => new(Titulo, Prioridade, Descricao);

    /// <summary>Converte o que veio da tela no comando de edição, com a versão lida.</summary>
    public EdicaoChamado ParaEdicao(int versaoEsperada) => new(Titulo, Prioridade, Descricao, versaoEsperada);
}
