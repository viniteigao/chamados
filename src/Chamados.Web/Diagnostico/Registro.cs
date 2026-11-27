namespace Chamados.Web.Diagnostico;

/// <summary>
/// Eventos de log da aplicação, com mensagem estruturada: cada {Nome} vira um campo pesquisável
/// no destino do log (no console JSON, uma propriedade). O gerador de código cria a implementação,
/// sem formatar texto quando o nível está desligado.
/// Regra do projeto: nunca senha, token, e-mail ou nome; o usuário aparece pelo Id.
/// </summary>
public static partial class Registro
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Login aceito para o usuário {UsuarioId}")]
    public static partial void LoginAceito(ILogger logger, string usuarioId);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Login recusado (usuário {UsuarioId})")]
    public static partial void LoginRecusado(ILogger logger, string usuarioId);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning, Message = "Conta bloqueada após tentativas seguidas: usuário {UsuarioId}")]
    public static partial void ContaBloqueada(ILogger logger, string usuarioId);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Logout do usuário {UsuarioId}")]
    public static partial void Logout(ILogger logger, string usuarioId);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Warning, Message = "Ação negada: {Acao} no chamado {ChamadoId} exige a política {Politica}")]
    public static partial void AcaoNegada(ILogger logger, string acao, Guid chamadoId, string politica);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Error, Message = "Falha ao consultar a API de indicadores")]
    public static partial void FalhaApiIndicadores(ILogger logger, Exception excecao);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Information, Message = "{Metodo} {Caminho} respondeu {Status} em {DuracaoMs:F1} ms")]
    public static partial void RequisicaoConcluida(ILogger logger, string metodo, string caminho, int status, double duracaoMs);

    // Melhorias: o limite de tentativas de login (Seguranca/LimiteDeLogin.cs) recusou uma requisição.
    [LoggerMessage(EventId = 1008, Level = LogLevel.Warning, Message = "Limite de tentativas de login atingido em {Caminho}")]
    public static partial void LimiteDeLoginAtingido(ILogger logger, string caminho);

    // Eventos que já existiam (Módulos 3 e 6), migrados para cá pela análise estática (CA1848).
    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Chamado {ChamadoId} aberto com prioridade {Prioridade}")]
    public static partial void ChamadoAberto(ILogger logger, Guid chamadoId, Domain.Prioridade prioridade);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Chamado {ChamadoId} editado (versão lida {Versao})")]
    public static partial void ChamadoEditado(ILogger logger, Guid chamadoId, int versao);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Não foi possível atualizar os totais da fila")]
    public static partial void FalhaAoAtualizarTotais(ILogger logger, Exception excecao);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Information, Message = "Atendimento iniciado (interativo: {Interativo}, fila restaurada: {Restaurada})")]
    public static partial void AtendimentoIniciado(ILogger logger, bool interativo, bool restaurada);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "Busca anterior cancelada")]
    public static partial void BuscaCancelada(ILogger logger);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Error, Message = "Falha ao carregar a fila")]
    public static partial void FalhaAoCarregarFila(ILogger logger, Exception excecao);
}
