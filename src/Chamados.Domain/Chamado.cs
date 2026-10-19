namespace Chamados.Domain;

/// <summary>
/// Solicitação de serviço registrada no sistema.
/// O ciclo de vida só avança pelos métodos públicos, que garantem
/// as transições permitidas e registram o histórico.
/// </summary>
public sealed class Chamado
{
    private readonly List<RegistroHistorico> _historico = [];

    public Chamado(
        string titulo,
        Prioridade prioridade = Prioridade.Media,
        string? descricao = null,
        DateTimeOffset? criadoEm = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);

        Titulo = titulo.Trim();
        Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        Prioridade = prioridade;
        CriadoEm = criadoEm ?? DateTimeOffset.UtcNow;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public string Titulo { get; }

    public string? Descricao { get; }

    public Prioridade Prioridade { get; }

    public StatusChamado Status { get; private set; } = StatusChamado.Aberto;

    public string? Responsavel { get; private set; }

    public string? Solucao { get; private set; }

    public DateTimeOffset CriadoEm { get; }

    public DateTimeOffset? EncerradoEm { get; private set; }

    // AsReadOnly impede que quem recebe a lista faça cast para List<T> e a altere.
    public IReadOnlyList<RegistroHistorico> Historico => _historico.AsReadOnly();

    public bool EstaEncerrado => Status is StatusChamado.Resolvido or StatusChamado.Cancelado;

    // DateTimeOffset? - DateTimeOffset resulta em TimeSpan? (nulo enquanto aberto).
    public TimeSpan? TempoAteEncerramento => EncerradoEm - CriadoEm;

    public void IniciarAtendimento(string responsavel, DateTimeOffset em)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responsavel);

        var nome = responsavel.Trim();
        MudarStatus(StatusChamado.EmAtendimento, em, $"Atribuído a {nome}");
        Responsavel = nome;
    }

    public void Resolver(string solucao, DateTimeOffset em)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solucao);

        var texto = solucao.Trim();
        MudarStatus(StatusChamado.Resolvido, em, texto);
        Solucao = texto;
        EncerradoEm = em;
    }

    public void Cancelar(string motivo, DateTimeOffset em)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);

        MudarStatus(StatusChamado.Cancelado, em, motivo.Trim());
        EncerradoEm = em;
    }

    private void MudarStatus(StatusChamado novo, DateTimeOffset em, string? observacao)
    {
        if (!TransicaoPermitida(Status, novo))
        {
            throw new TransicaoInvalidaException(Id, Status, novo);
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(em, CriadoEm);

        _historico.Add(new RegistroHistorico(Status, novo, em, observacao));
        Status = novo;
    }

    private static bool TransicaoPermitida(StatusChamado de, StatusChamado para) => (de, para) switch
    {
        (StatusChamado.Aberto, StatusChamado.EmAtendimento) => true,
        (StatusChamado.EmAtendimento, StatusChamado.Resolvido) => true,
        (StatusChamado.Aberto or StatusChamado.EmAtendimento, StatusChamado.Cancelado) => true,
        _ => false
    };
}