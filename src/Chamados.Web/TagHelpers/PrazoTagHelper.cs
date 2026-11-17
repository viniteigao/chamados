using Chamados.Domain;
using Chamados.Web.Apresentacao;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Chamados.Web.TagHelpers;

/// <summary>
/// &lt;prazo criado-em="@c.CriadoEm" prioridade="@c.Prioridade" status="@c.Status" /&gt; vira
/// &lt;span class="selo selo-no-prazo"&gt;Vence em 3 h 10 min&lt;/span&gt; ou
/// &lt;span class="selo selo-atrasado"&gt;Atrasado há 45 min&lt;/span&gt;.
/// O prazo vem do domínio (PrazoAtendimento); aqui só se decide o texto e a cor.
/// </summary>
[HtmlTargetElement("prazo", Attributes = "criado-em,prioridade,status", TagStructure = TagStructure.WithoutEndTag)]
public sealed class PrazoTagHelper(TimeProvider relogio) : TagHelper
{
    public DateTimeOffset CriadoEm { get; set; }

    public Prioridade Prioridade { get; set; }

    public StatusChamado Status { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // Chamado encerrado não tem prazo a cumprir: a célula fica vazia.
        if (Status is StatusChamado.Resolvido or StatusChamado.Cancelado)
        {
            output.SuppressOutput();
            return;
        }

        var restante = PrazoAtendimento.VenceEm(CriadoEm, Prioridade) - relogio.GetUtcNow();
        var noPrazo = restante >= TimeSpan.Zero;

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", noPrazo ? "selo selo-no-prazo" : "selo selo-atrasado");
        output.Content.SetContent(noPrazo
            ? $"Vence em {restante.Duracao()}"
            : $"Atrasado há {restante.Negate().Duracao()}");
    }
}
