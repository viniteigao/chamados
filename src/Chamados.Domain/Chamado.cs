namespace Chamados.Domain;

/// <summary>
/// Solicitação de serviço registrada no sistema.
/// O ciclo de vida só avança pelos métodos públicos, que garantem
/// as transições permitidas e registram o histórico.
/// </summary>
public sealed class Chamado
{
    // Limites da regra de negócio. O formulário web reusa estas constantes
    // nos atributos de validação: uma única fonte de verdade.
    public const int TamanhoMaximoTitulo = 120;
    public const int TamanhoMaximoDescricao = 2000;

    // Melhorias: o nome do responsável tinha 120 caracteres só no banco, e solução e motivo,
    // nenhum limite no domínio. Agora toda entrada de texto tem tamanho máximo aqui.
    public const int TamanhoMaximoResponsavel = 120;
    public const int TamanhoMaximoJustificativa = TamanhoMaximoDescricao;   // solução e motivo

    private readonly List<RegistroHistorico> _historico = [];

    public Chamado(
        string titulo,
        Prioridade prioridade = Prioridade.Media,
        string? descricao = null,
        DateTimeOffset? criadoEm = null)
    {
        // As mesmas regras valem para criar e para editar: ficam em métodos privados.
        Titulo = ValidarTitulo(titulo);
        Descricao = ValidarDescricao(descricao);
        Prioridade = ValidarPrioridade(prioridade);
        CriadoEm = criadoEm ?? DateTimeOffset.UtcNow;
    }

    // Módulo 7: construtor sem parâmetros, privado, para o Entity Framework Core materializar
    // chamados lidos do banco. Os valores chegam das colunas, já validados quando o chamado nasceu.
    // O EF não consegue usar o construtor público porque o parâmetro criadoEm é anulável e a propriedade não.
    private Chamado()
    {
        Titulo = string.Empty;
    }

    public Guid Id { get; } = Guid.NewGuid();

    public string Titulo { get; private set; }

    public string? Descricao { get; private set; }

    public Prioridade Prioridade { get; private set; }

    /// <summary>
    /// Número que aumenta a cada alteração. Quem edita informa a versão que leu;
    /// se o chamado mudou nesse meio-tempo, a edição é recusada (concorrência otimista).
    /// </summary>
    public int Versao { get; private set; } = 1;

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

    /// <summary>Altera título, prioridade e descrição de um chamado ainda não encerrado.</summary>
    public void Editar(string titulo, Prioridade prioridade, string? descricao)
    {
        if (EstaEncerrado)
        {
            throw new ChamadoEncerradoException(Id, Status);
        }

        // Valida tudo antes de alterar qualquer coisa: ou muda tudo, ou nada.
        var novoTitulo = ValidarTitulo(titulo);
        var novaDescricao = ValidarDescricao(descricao);
        var novaPrioridade = ValidarPrioridade(prioridade);

        Titulo = novoTitulo;
        Descricao = novaDescricao;
        Prioridade = novaPrioridade;
        Versao++;
    }

    public void IniciarAtendimento(string responsavel, DateTimeOffset em)
    {
        var nome = ValidarTextoObrigatorio(responsavel, TamanhoMaximoResponsavel, "O responsável", nameof(responsavel));
        MudarStatus(StatusChamado.EmAtendimento, em, $"Atribuído a {nome}");
        Responsavel = nome;
    }

    public void Resolver(string solucao, DateTimeOffset em)
    {
        var texto = ValidarTextoObrigatorio(solucao, TamanhoMaximoJustificativa, "A solução", nameof(solucao));
        MudarStatus(StatusChamado.Resolvido, em, texto);
        Solucao = texto;
        EncerradoEm = em;
    }

    public void Cancelar(string motivo, DateTimeOffset em)
    {
        var texto = ValidarTextoObrigatorio(motivo, TamanhoMaximoJustificativa, "O motivo", nameof(motivo));
        MudarStatus(StatusChamado.Cancelado, em, texto);
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
        Versao++;
    }

    private static string ValidarTitulo(string titulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);

        titulo = titulo.Trim();
        if (titulo.Length > TamanhoMaximoTitulo)
        {
            throw new ArgumentException($"O título deve ter no máximo {TamanhoMaximoTitulo} caracteres.", nameof(titulo));
        }

        return titulo;
    }

    private static string? ValidarDescricao(string? descricao)
    {
        descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        if (descricao?.Length > TamanhoMaximoDescricao)
        {
            throw new ArgumentException($"A descrição deve ter no máximo {TamanhoMaximoDescricao} caracteres.", nameof(descricao));
        }

        return descricao;
    }

    // Texto obrigatório das transições (responsável, solução, motivo): validado antes de mudar
    // qualquer coisa, para que um texto longo demais não deixe o chamado pela metade.
    private static string ValidarTextoObrigatorio(string valor, int tamanhoMaximo, string campo, string nomeDoParametro)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(valor, nomeDoParametro);

        var texto = valor.Trim();
        if (texto.Length > tamanhoMaximo)
        {
            throw new ArgumentException($"{campo} deve ter no máximo {tamanhoMaximo} caracteres.", nomeDoParametro);
        }

        return texto;
    }

    private static Prioridade ValidarPrioridade(Prioridade prioridade)
    {
        // Um enum aceita qualquer inteiro: (Prioridade)99 compila e chega pelo formulário.
        if (!Enum.IsDefined(prioridade))
        {
            throw new ArgumentOutOfRangeException(nameof(prioridade), prioridade, "Prioridade inexistente.");
        }

        return prioridade;
    }

    private static bool TransicaoPermitida(StatusChamado de, StatusChamado para) => (de, para) switch
    {
        (StatusChamado.Aberto, StatusChamado.EmAtendimento) => true,
        (StatusChamado.EmAtendimento, StatusChamado.Resolvido) => true,
        (StatusChamado.Aberto or StatusChamado.EmAtendimento, StatusChamado.Cancelado) => true,
        _ => false
    };
}
