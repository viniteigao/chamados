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

    // Comentários: tamanho máximo de uma mensagem da conversa.
    public const int TamanhoMaximoComentario = 2000;

    private readonly List<RegistroHistorico> _historico = [];
    private readonly List<Comentario> _comentarios = [];
    private readonly List<RegistroEdicao> _edicoes = [];

    public Chamado(
        string titulo,
        Prioridade prioridade = Prioridade.Media,
        string? descricao = null,
        DateTimeOffset? criadoEm = null,
        Autor? abertoPor = null)
    {
        // As mesmas regras valem para criar e para editar: ficam em métodos privados.
        Titulo = ValidarTitulo(titulo);
        Descricao = ValidarDescricao(descricao);
        Prioridade = ValidarPrioridade(prioridade);
        CriadoEm = criadoEm ?? DateTimeOffset.UtcNow;
        AbertoPorId = abertoPor?.Id;
        AbertoPorNome = abertoPor?.Nome;
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

    /// <summary>Nome do responsável, como era ao ser atribuído (chamados antigos: o texto digitado).</summary>
    public string? Responsavel { get; private set; }

    /// <summary>
    /// Responsável atendente: o Id do atendente que cuida do chamado. Nulo nos chamados abertos e nos
    /// atribuídos antes deste recurso, quando o responsável era um texto livre.
    /// </summary>
    public string? ResponsavelId { get; private set; }

    /// <summary>O chamado está com este atendente? Falso nos chamados sem responsável cadastrado.</summary>
    public bool FoiAtribuidoA(string usuarioId) => ResponsavelId is not null && ResponsavelId == usuarioId;

    public string? Solucao { get; private set; }

    public DateTimeOffset CriadoEm { get; }

    public DateTimeOffset? EncerradoEm { get; private set; }

    // Meus chamados: quem abriu, em duas colunas simples. Nulo nos chamados anteriores a este
    // recurso, que ficam sem dono (visíveis só para atendentes e coordenação).
    public string? AbertoPorId { get; private set; }

    public string? AbertoPorNome { get; private set; }

    /// <summary>Quem abriu o chamado; null nos chamados sem dono.</summary>
    public Autor? AbertoPor => AbertoPorId is null ? null : new Autor(AbertoPorId, AbertoPorNome ?? AbertoPorId);

    /// <summary>O chamado foi aberto por este usuário? Falso nos chamados sem dono.</summary>
    public bool FoiAbertoPor(string usuarioId) => AbertoPorId is not null && AbertoPorId == usuarioId;

    // Histórico das edições: as três listas saem sempre em ordem cronológica, garantida aqui e não
    // pelo banco (que carrega cada tabela numa consulta e não promete a ordem das linhas).
    // OrderBy é estável: registros com a mesma hora ficam na ordem em que chegaram.
    // Cada leitura devolve uma cópia somente leitura: quem recebe não altera a lista do chamado.
    public IReadOnlyList<RegistroHistorico> Historico => [.. _historico.OrderBy(r => r.Em)];

    /// <summary>Comentários: a conversa do chamado, da mensagem mais antiga para a mais nova.</summary>
    public IReadOnlyList<Comentario> Comentarios => [.. _comentarios.OrderBy(m => m.Em)];

    /// <summary>Histórico das edições: cada edição de título, prioridade ou descrição, da mais antiga para a mais nova.</summary>
    public IReadOnlyList<RegistroEdicao> Edicoes => [.. _edicoes.OrderBy(e => e.Em)];

    public bool EstaEncerrado => Status is StatusChamado.Resolvido or StatusChamado.Cancelado;

    // DateTimeOffset? - DateTimeOffset resulta em TimeSpan? (nulo enquanto aberto).
    public TimeSpan? TempoAteEncerramento => EncerradoEm - CriadoEm;

    /// <summary>
    /// Altera título, prioridade e descrição de um chamado ainda não encerrado.
    /// Histórico das edições: registra o que mudou, quando e por quem. Uma edição que não muda
    /// nada não entra no histórico (a versão aumenta mesmo assim, como sempre aumentou).
    /// </summary>
    public void Editar(string titulo, Prioridade prioridade, string? descricao, DateTimeOffset em, Autor? por = null)
    {
        if (EstaEncerrado)
        {
            throw new ChamadoEncerradoException(Id, Status);
        }

        // Valida tudo antes de alterar qualquer coisa: ou muda tudo, ou nada.
        var novoTitulo = ValidarTitulo(titulo);
        var novaDescricao = ValidarDescricao(descricao);
        var novaPrioridade = ValidarPrioridade(prioridade);
        ArgumentOutOfRangeException.ThrowIfLessThan(em, CriadoEm);

        var tituloMudou = !string.Equals(novoTitulo, Titulo, StringComparison.Ordinal);
        var prioridadeMudou = novaPrioridade != Prioridade;
        var descricaoMudou = !string.Equals(novaDescricao, Descricao, StringComparison.Ordinal);
        if (tituloMudou || prioridadeMudou || descricaoMudou)
        {
            _edicoes.Add(new RegistroEdicao(
                em,
                tituloMudou ? Titulo : null,
                tituloMudou ? novoTitulo : null,
                prioridadeMudou ? Prioridade : null,
                prioridadeMudou ? novaPrioridade : null,
                descricaoMudou,
                por?.Id,
                por?.Nome));
        }

        Titulo = novoTitulo;
        Descricao = novaDescricao;
        Prioridade = novaPrioridade;
        Versao++;
    }

    /// <summary>
    /// Comentários: acrescenta uma mensagem à conversa, em qualquer status (depois de resolvido,
    /// "voltou a dar problema" também é conversa). Toda mensagem tem autor.
    /// Não muda a versão: comentar não é editar, e quem estiver editando o chamado ao mesmo
    /// tempo não deve receber um conflito por causa de uma mensagem.
    /// </summary>
    public Comentario Comentar(string texto, DateTimeOffset em, Autor por)
    {
        ArgumentNullException.ThrowIfNull(por);
        var mensagem = ValidarTextoObrigatorio(texto, TamanhoMaximoComentario, "O comentário", nameof(texto));
        ArgumentOutOfRangeException.ThrowIfLessThan(em, CriadoEm);

        var comentario = new Comentario(mensagem, em, por.Id, por.Nome);
        _comentarios.Add(comentario);
        return comentario;
    }

    // Meus chamados: cada transição aceita quem a fez (por), que vai para o histórico.
    // Responsável atendente: o responsável é uma pessoa (Id e nome), não mais um texto livre.
    public void IniciarAtendimento(Autor responsavel, DateTimeOffset em, Autor? por = null)
    {
        ArgumentNullException.ThrowIfNull(responsavel);
        var nome = ValidarTextoObrigatorio(responsavel.Nome, TamanhoMaximoResponsavel, "O nome do responsável", nameof(responsavel));
        MudarStatus(StatusChamado.EmAtendimento, em, $"Atribuído a {nome}", por);
        Responsavel = nome;
        ResponsavelId = responsavel.Id;
    }

    /// <summary>
    /// Responsável atendente: passa o atendimento a outra pessoa. Só em atendimento; o status não muda,
    /// e a troca entra no histórico das edições ("responsável Ana → Davi"). Passar para quem já é o
    /// responsável não muda nada.
    /// </summary>
    public void Reatribuir(Autor responsavel, DateTimeOffset em, Autor? por = null)
    {
        ArgumentNullException.ThrowIfNull(responsavel);
        if (EstaEncerrado)
        {
            throw new ChamadoEncerradoException(Id, Status);
        }

        if (Status != StatusChamado.EmAtendimento)
        {
            throw new ChamadoSemAtendimentoException(Id, Status);
        }

        var nome = ValidarTextoObrigatorio(responsavel.Nome, TamanhoMaximoResponsavel, "O nome do responsável", nameof(responsavel));
        ArgumentOutOfRangeException.ThrowIfLessThan(em, CriadoEm);
        if (FoiAtribuidoA(responsavel.Id))
        {
            return;
        }

        _edicoes.Add(new RegistroEdicao(em, null, null, null, null, false, por?.Id, por?.Nome,
            ResponsavelAnterior: Responsavel, ResponsavelNovo: nome));
        Responsavel = nome;
        ResponsavelId = responsavel.Id;
        Versao++;
    }

    public void Resolver(string solucao, DateTimeOffset em, Autor? por = null)
    {
        var texto = ValidarTextoObrigatorio(solucao, TamanhoMaximoJustificativa, "A solução", nameof(solucao));
        MudarStatus(StatusChamado.Resolvido, em, texto, por);
        Solucao = texto;
        EncerradoEm = em;
    }

    public void Cancelar(string motivo, DateTimeOffset em, Autor? por = null)
    {
        var texto = ValidarTextoObrigatorio(motivo, TamanhoMaximoJustificativa, "O motivo", nameof(motivo));
        MudarStatus(StatusChamado.Cancelado, em, texto, por);
        EncerradoEm = em;
    }

    private void MudarStatus(StatusChamado novo, DateTimeOffset em, string? observacao, Autor? por)
    {
        if (!TransicaoPermitida(Status, novo))
        {
            throw new TransicaoInvalidaException(Id, Status, novo);
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(em, CriadoEm);

        _historico.Add(new RegistroHistorico(Status, novo, em, observacao, por?.Id, por?.Nome));
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
