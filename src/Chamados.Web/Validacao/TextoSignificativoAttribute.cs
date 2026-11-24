using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Chamados.Web.Validacao;

/// <summary>
/// Exige um mínimo de letras no texto. Recusa "!!!!!!" e "123456", que passam
/// no tamanho mínimo mas não descrevem problema nenhum.
/// Texto vazio é válido aqui: quem exige preenchimento é o [Required].
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class TextoSignificativoAttribute(int minimoDeLetras = 3)
    : ValidationAttribute("{0} precisa ter pelo menos {1} letras.")
{
    public int MinimoDeLetras { get; } = minimoDeLetras;

    public override bool IsValid(object? value)
    {
        if (value is not string texto || string.IsNullOrWhiteSpace(texto))
        {
            return true;
        }

        // char.IsLetter aceita letras acentuadas: "Água" conta 4 letras.
        return texto.Count(char.IsLetter) >= MinimoDeLetras;
    }

    // {0} é o nome de exibição do campo ([Display(Name = ...)]); {1}, o mínimo.
    public override string FormatErrorMessage(string name)
        => string.Format(CultureInfo.CurrentCulture, ErrorMessageString, name, MinimoDeLetras);
}
