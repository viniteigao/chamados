namespace Chamados.Domain;

/// <summary>
/// Comentários: uma mensagem na conversa do chamado. Imutável: o que foi dito fica como foi dito.
/// Guarda quem escreveu (Id e nome, como era no momento) e quando.
/// </summary>
public sealed record Comentario(string Texto, DateTimeOffset Em, string AutorId, string AutorNome)
{
    /// <summary>Quem escreveu a mensagem.</summary>
    public Autor Autor => new(AutorId, AutorNome);
}
