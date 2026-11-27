namespace Chamados.Domain;

/// <summary>
/// Quem fez uma ação: abriu um chamado ou mudou o status dele.
/// O domínio não conhece o Identity: recebe só o Id do usuário (para regras e consultas) e o nome
/// (para exibir, como era no momento da ação). Quem monta o Autor é a camada Web, a partir do login.
/// </summary>
public sealed record Autor
{
    public const int TamanhoMaximoId = 450;     // o mesmo limite do Id de usuário do Identity
    public const int TamanhoMaximoNome = 256;   // o mesmo limite do UserName do Identity

    public Autor(string id, string nome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(id.Length, TamanhoMaximoId, nameof(id));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(nome.Trim().Length, TamanhoMaximoNome, nameof(nome));

        Id = id;
        Nome = nome.Trim();
    }

    public string Id { get; }

    public string Nome { get; }
}
