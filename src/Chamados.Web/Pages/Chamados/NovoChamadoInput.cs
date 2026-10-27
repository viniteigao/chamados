using System.ComponentModel.DataAnnotations;
using Chamados.Domain;

namespace Chamados.Web.Pages.Chamados;

/// <summary>
/// Modelo do formulário: o que a tela recebe, com as regras de validação da tela.
/// Não é a entidade: Chamado tem setters privados e nasce já válida.
/// </summary>
public sealed class NovoChamadoInput
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

    /// <summary>Converte o que veio da tela no comando que a camada de negócio entende.</summary>
    public NovoChamado ParaComando() => new(Titulo, Prioridade, Descricao);
}
