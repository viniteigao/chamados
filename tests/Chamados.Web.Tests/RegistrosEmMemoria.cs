using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Chamados.Web.Tests;

/// <summary>
/// Melhorias: guarda em memória o que a aplicação escreve no log, para os testes conferirem
/// os eventos de segurança (Id do evento e mensagem já formatada). Sem pacote extra.
/// </summary>
public sealed class RegistrosEmMemoria : ILoggerProvider
{
    private readonly ConcurrentQueue<RegistroDeLog> _registros = new();

    public IReadOnlyCollection<RegistroDeLog> Todos => _registros;

    public ILogger CreateLogger(string categoryName) => new Coletor(categoryName, _registros);

    public void Dispose()
    {
    }

    private sealed class Coletor(string categoria, ConcurrentQueue<RegistroDeLog> destino) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                destino.Enqueue(new RegistroDeLog(categoria, eventId.Id, logLevel, formatter(state, exception)));
            }
        }
    }
}

public sealed record RegistroDeLog(string Categoria, int EventoId, LogLevel Nivel, string Mensagem);
