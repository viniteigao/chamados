namespace Chamados.Domain;

/// <summary>
/// Mensagens não lidas: a regra de quais mensagens da conversa um usuário ainda não leu.
/// A marcação de leitura (até quando cada pessoa leu cada conversa) fica fora do Chamado: é de cada
/// usuário, não do chamado, e ler não é alterar (não muda a versão nem entra no histórico).
/// O repositório guarda essa marcação; estes métodos só a interpretam. O repositório do banco repete
/// a mesma regra em SQL, como já faz com os filtros.
/// </summary>
public static class MensagensNaoLidas
{
    /// <summary>
    /// Quem acompanha a conversa: quem abriu o chamado, o atendente responsável e quem já escreveu nela.
    /// Só essas pessoas recebem o aviso de mensagem nova. Sem esta regra, quem vê todos os chamados
    /// (atendentes e coordenação) teria "não lidas" em todas as conversas do sistema.
    /// </summary>
    public static bool AcompanhaConversa(this Chamado chamado, string usuarioId)
    {
        ArgumentNullException.ThrowIfNull(chamado);
        ArgumentException.ThrowIfNullOrWhiteSpace(usuarioId);

        return chamado.FoiAbertoPor(usuarioId)
            || chamado.FoiAtribuidoA(usuarioId)
            || chamado.Comentarios.Any(m => m.AutorId == usuarioId);
    }

    /// <summary>
    /// A mensagem conta como não lida para esse usuário? Só para quem acompanha a conversa, só as
    /// escritas por outras pessoas (as próprias nunca contam) e só as posteriores à última leitura
    /// (<paramref name="lidoAte"/>; null quando o usuário nunca leu a conversa).
    /// </summary>
    public static bool NaoLida(this Chamado chamado, Comentario mensagem, string usuarioId, DateTimeOffset? lidoAte)
    {
        ArgumentNullException.ThrowIfNull(mensagem);

        return chamado.AcompanhaConversa(usuarioId) && DeOutroDepoisDaLeitura(mensagem, usuarioId, lidoAte);
    }

    /// <summary>As mensagens que esse usuário ainda não leu, da mais antiga para a mais nova.</summary>
    public static IReadOnlyList<Comentario> NaoLidasPor(this Chamado chamado, string usuarioId, DateTimeOffset? lidoAte)
    {
        // A conferência de quem acompanha é a mesma para todas as mensagens: feita uma vez só.
        return chamado.AcompanhaConversa(usuarioId)
            ? [.. chamado.Comentarios.Where(m => DeOutroDepoisDaLeitura(m, usuarioId, lidoAte))]
            : [];
    }

    // Duas mensagens no mesmo milissegundo da leitura contam como lidas: o banco guarda as datas com
    // precisão de milissegundo, e a leitura grava a hora exata da última mensagem mostrada.
    private static bool DeOutroDepoisDaLeitura(Comentario mensagem, string usuarioId, DateTimeOffset? lidoAte)
        => mensagem.AutorId != usuarioId && (lidoAte is null || mensagem.Em > lidoAte);
}
