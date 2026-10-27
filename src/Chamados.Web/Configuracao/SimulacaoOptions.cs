using System.ComponentModel.DataAnnotations;

namespace Chamados.Web.Configuracao;

/// <summary>
/// Seção "Simulacao" do appsettings, lida como objeto tipado.
/// Valores fora da faixa derrubam a aplicação na inicialização (ValidateOnStart),
/// em vez de produzirem um erro estranho horas depois.
/// </summary>
public sealed class SimulacaoOptions
{
    public const string Secao = "Simulacao";

    /// <summary>Atraso artificial da listagem, em milissegundos.</summary>
    [Range(0, 10_000, ErrorMessage = "Simulacao:LatenciaMs deve ficar entre {1} e {2}.")]
    public int LatenciaMs { get; set; }
}
