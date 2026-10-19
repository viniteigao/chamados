namespace Chamados.Domain.Tests;

/// <summary>
/// Relógio controlado pelo teste. Substitui TimeProvider.System
/// para que datas e durações sejam previsíveis.
/// </summary>
internal sealed class RelogioFixo(DateTimeOffset inicio) : TimeProvider
{
    private DateTimeOffset _agora = inicio;

    public override DateTimeOffset GetUtcNow() => _agora;

    public void Avancar(TimeSpan intervalo) => _agora += intervalo;
}