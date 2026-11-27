using System.Globalization;
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

    public static string Rotulo(this OrdemChamados ordem) => ordem switch
    {
        OrdemChamados.Prioridade => "Prioridade",
        OrdemChamados.MaisRecentes => "Mais recentes",
        OrdemChamados.MaisAntigos => "Mais antigos",
        OrdemChamados.Titulo => "Título (A–Z)",
        _ => ordem.ToString()
    };

    public static string Rotulo(this SituacaoPrazo situacao) => situacao switch
    {
        SituacaoPrazo.NoPrazo => "No prazo",
        SituacaoPrazo.PertoDeVencer => "Vence logo",
        SituacaoPrazo.Vencido => "Vencido",
        SituacaoPrazo.Cumprido => "Cumprido",
        SituacaoPrazo.Descumprido => "Fora do prazo",
        _ => situacao.ToString()
    };

    /// <summary>Rótulo de qualquer enum conhecido. Usado pelo Tag Helper &lt;selo&gt;.</summary>
    public static string Rotulo(this Enum valor) => valor switch
    {
        StatusChamado status => status.Rotulo(),
        Prioridade prioridade => prioridade.Rotulo(),
        OrdemChamados ordem => ordem.Rotulo(),
        SituacaoPrazo situacao => situacao.Rotulo(),
        _ => valor.ToString()
    };

    /// <summary>
    /// Prazos: a frase que acompanha o selo. "vence em 3 h 20 min", "venceu há 2 dias",
    /// "resolvido 5 h antes do prazo", "resolvido com 1 dia e 2 h de atraso".
    /// </summary>
    public static string Descricao(this PrazoDoChamado prazo)
    {
        ArgumentNullException.ThrowIfNull(prazo);

        return prazo.Situacao switch
        {
            SituacaoPrazo.Vencido => $"venceu há {prazo.Folga.Aproximada()}",
            SituacaoPrazo.Cumprido => $"resolvido {prazo.Folga.Aproximada()} antes do prazo",
            SituacaoPrazo.Descumprido => $"resolvido com {prazo.Folga.Aproximada()} de atraso",
            _ => $"vence em {prazo.Folga.Aproximada()}"
        };
    }

    /// <summary>
    /// Duração arredondada para leitura rápida, sem sinal: "menos de 1 min", "40 min", "3 h",
    /// "3 h 20 min", "1 dia", "2 dias e 4 h". Para prazos de dias, minutos não importam.
    /// </summary>
    public static string Aproximada(this TimeSpan tempo)
    {
        tempo = tempo.Duration();
        if (tempo.TotalMinutes < 1)
        {
            return "menos de 1 min";
        }

        if (tempo.TotalHours < 1)
        {
            return $"{(int)tempo.TotalMinutes} min";
        }

        if (tempo.TotalDays < 1)
        {
            return tempo.Minutes == 0 ? $"{(int)tempo.TotalHours} h" : $"{(int)tempo.TotalHours} h {tempo.Minutes:00} min";
        }

        var dias = (int)tempo.TotalDays;
        var textoDias = dias == 1 ? "1 dia" : $"{dias} dias";
        return tempo.Hours == 0 ? textoDias : $"{textoDias} e {tempo.Hours} h";
    }

    /// <summary>Classe CSS do selo, derivada do nome do enum: selo-aberto, selo-critica...</summary>
    public static string ClasseSelo(this Enum valor) => "selo selo-" + valor.ToString().ToLowerInvariant();

    // O value de cada opção é o nome do enum ("Alta"); o model binding converte de volta.
    public static IEnumerable<SelectListItem> OpcoesStatus()
        => Enum.GetValues<StatusChamado>().Select(s => new SelectListItem(s.Rotulo(), s.ToString()));

    public static IEnumerable<SelectListItem> OpcoesPrioridade()
        => Enum.GetValues<Prioridade>().Select(p => new SelectListItem(p.Rotulo(), p.ToString()));

    public static IEnumerable<SelectListItem> OpcoesOrdem()
        => Enum.GetValues<OrdemChamados>().Select(o => new SelectListItem(o.Rotulo(), o.ToString()));

    /// <summary>Duração legível: "menos de 1 min", "42 min", "3 h 05 min".</summary>
    public static string Duracao(this TimeSpan tempo) => tempo switch
    {
        { TotalMinutes: < 1 } => "menos de 1 min",
        { TotalHours: < 1 } => $"{(int)tempo.TotalMinutes} min",
        _ => $"{(int)tempo.TotalHours} h {tempo.Minutes:00} min"
    };

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Data no fuso do servidor, formato brasileiro.</summary>
    // Cultura explícita (CA1305): o resultado não depende da configuração da máquina.
    public static string DataHora(this DateTimeOffset momento) => momento.ToLocalTime().ToString("dd/MM/yyyy HH:mm", PtBr);

    /// <summary>Gestão de usuários: o que a pessoa pode fazer, em poucas palavras.</summary>
    public static string Perfil(bool atende, bool coordena) => (atende, coordena) switch
    {
        (true, true) => "Atende e coordena",
        (true, false) => "Atende chamados",
        (false, true) => "Coordenação",
        _ => "Abre chamados",
    };

    /// <summary>Comentários: "1 mensagem", "3 mensagens".</summary>
    public static string Mensagens(this int quantidade)
        => quantidade == 1 ? "1 mensagem" : $"{quantidade} mensagens";

    /// <summary>Mensagens não lidas: "1 mensagem nova", "3 mensagens novas".</summary>
    public static string MensagensNovas(this int quantidade)
        => quantidade == 1 ? "1 mensagem nova" : $"{quantidade} mensagens novas";

    /// <summary>Mensagens não lidas: "1 nova", "3 novas".</summary>
    public static string Novas(this int quantidade)
        => quantidade == 1 ? "1 nova" : $"{quantidade} novas";

    /// <summary>Mensagens não lidas: o aviso do cabeçalho, "Mensagem nova em 1 chamado", "Mensagens novas em 2 chamados".</summary>
    public static string AvisoDeNaoLidas(this int chamados)
        => chamados == 1 ? "Mensagem nova em 1 chamado" : $"Mensagens novas em {chamados} chamados";

    /// <summary>Comentários: data e hora de uma mensagem da conversa, "26/11/2026 às 14:32".</summary>
    public static string DataHoraMensagem(this DateTimeOffset momento)
        => momento.ToLocalTime().ToString("dd/MM/yyyy 'às' HH:mm", PtBr);

    /// <summary>
    /// O mesmo instante em ISO 8601 (2026-11-26T14:32:05-03:00), para o atributo datetime de &lt;time&gt;:
    /// leitores de tela e scripts entendem a data sem depender do formato brasileiro.
    /// </summary>
    public static string Iso(this DateTimeOffset momento)
        => momento.ToLocalTime().ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
}
