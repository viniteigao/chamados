using Chamados.Web.Apresentacao;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Chamados.Web.TagHelpers;

/// <summary>
/// &lt;selo valor="@chamado.Status" /&gt; vira
/// &lt;span class="selo selo-ematendimento"&gt;Em atendimento&lt;/span&gt;.
/// Substitui a combinação repetida de ClasseSelo() e Rotulo() nas páginas.
/// </summary>
[HtmlTargetElement("selo", Attributes = "valor", TagStructure = TagStructure.WithoutEndTag)]
public sealed class SeloTagHelper : TagHelper
{
    /// <summary>Qualquer enum com rótulo: StatusChamado ou Prioridade.</summary>
    public Enum? Valor { get; set; }

    /// <summary>Texto opcional antes do rótulo, como "Prioridade ".</summary>
    public string? Prefixo { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (Valor is null)
        {
            output.SuppressOutput();
            return;
        }

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", Valor.ClasseSelo());

        // SetContent codifica o texto em HTML: nenhum rótulo vira marcação.
        output.Content.SetContent(Prefixo + Valor.Rotulo());
    }
}
