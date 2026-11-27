using Chamados.Domain;
using Chamados.Web.Apresentacao;
using Microsoft.Extensions.Options;

namespace Chamados.Web.Configuracao;

/// <summary>
/// Prazos: os prazos de atendimento por prioridade, lidos da seção "Prazos" da configuração
/// (formato de TimeSpan: "04:00:00" são 4 horas, "3.00:00:00" são 3 dias).
/// A regra (todos positivos; prioridade mais alta nunca com prazo maior) é do domínio:
/// <see cref="PoliticaDePrazos"/>. Prazo inválido impede a aplicação de iniciar (ValidateOnStart).
/// </summary>
public sealed class PrazosOptions
{
    public const string Secao = "Prazos";

    public TimeSpan Critica { get; set; } = PoliticaDePrazos.Padrao.PrazoDe(Prioridade.Critica);

    public TimeSpan Alta { get; set; } = PoliticaDePrazos.Padrao.PrazoDe(Prioridade.Alta);

    public TimeSpan Media { get; set; } = PoliticaDePrazos.Padrao.PrazoDe(Prioridade.Media);

    public TimeSpan Baixa { get; set; } = PoliticaDePrazos.Padrao.PrazoDe(Prioridade.Baixa);

    public PoliticaDePrazos ParaPolitica() => new(new Dictionary<Prioridade, TimeSpan>
    {
        [Prioridade.Critica] = Critica,
        [Prioridade.Alta] = Alta,
        [Prioridade.Media] = Media,
        [Prioridade.Baixa] = Baixa,
    });
}

/// <summary>
/// Confere os prazos configurados com a própria regra do domínio e, se algo estiver errado,
/// impede a inicialização com a mensagem dele ("O prazo da prioridade Critica não pode ser maior...").
/// </summary>
public sealed class ValidacaoDePrazos : IValidateOptions<PrazosOptions>
{
    public ValidateOptionsResult Validate(string? name, PrazosOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            options.ParaPolitica();
            return ValidateOptionsResult.Success;
        }
        catch (ArgumentException ex)
        {
            return ValidateOptionsResult.Fail($"Seção {PrazosOptions.Secao}: {ex.ParaUsuario()}");
        }
    }
}
