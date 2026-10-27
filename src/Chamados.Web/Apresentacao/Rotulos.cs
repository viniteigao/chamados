using Chamados.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Chamados.Web.Apresentacao;

/// <summary>
/// Textos de tela para os enums do domínio. Ficam no projeto Web porque
/// "Em atendimento" com espaço e acento é decisão de apresentação, não regra de negócio.
/// </summary>
public static class Rotulos
{
    public static string Rotulo(this StatusChamado status) => status switch
    {
        StatusChamado.Aberto => "Aberto",
        StatusChamado.EmAtendimento => "Em atendimento",
        StatusChamado.Resolvido => "Resolvido",
        StatusChamado.Cancelado => "Cancelado",
        _ => status.ToString()
    };

    public static string Rotulo(this Prioridade prioridade) => prioridade switch
    {
        Prioridade.Baixa => "Baixa",
        Prioridade.Media => "Média",
        Prioridade.Alta => "Alta",
        Prioridade.Critica => "Crítica",
        _ => prioridade.ToString()
    };

    /// <summary>Classe CSS do selo, derivada do nome do enum: selo-aberto, selo-critica...</summary>
    public static string ClasseSelo(this Enum valor) => "selo selo-" + valor.ToString().ToLowerInvariant();

    // O value de cada opção é o nome do enum ("Alta"); o model binding converte de volta.
    public static IEnumerable<SelectListItem> OpcoesStatus()
        => Enum.GetValues<StatusChamado>().Select(s => new SelectListItem(s.Rotulo(), s.ToString()));

    public static IEnumerable<SelectListItem> OpcoesPrioridade()
        => Enum.GetValues<Prioridade>().Select(p => new SelectListItem(p.Rotulo(), p.ToString()));

    /// <summary>Duração legível: "menos de 1 min", "42 min", "3 h 05 min".</summary>
    public static string Duracao(this TimeSpan tempo) => tempo switch
    {
        { TotalMinutes: < 1 } => "menos de 1 min",
        { TotalHours: < 1 } => $"{(int)tempo.TotalMinutes} min",
        _ => $"{(int)tempo.TotalHours} h {tempo.Minutes:00} min"
    };

    /// <summary>Data no fuso do servidor, formato brasileiro.</summary>
    public static string DataHora(this DateTimeOffset momento) => momento.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}
