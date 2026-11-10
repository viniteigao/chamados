using Chamados.Domain;

namespace Chamados.Web;

/// <summary>Popula a base em memória com exemplos, apenas em Development.</summary>
public static class SementeDados
{
    public static async Task PopularAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        // ChamadoServico é Scoped: fora de uma requisição, é preciso criar um escopo.
        await using var escopo = services.CreateAsyncScope();
        var servico = escopo.ServiceProvider.GetRequiredService<ChamadoServico>();

        await servico.AbrirAsync(new NovoChamado("Trocar lâmpada do corredor", Prioridade.Baixa), cancellationToken);

        var arCondicionado = await servico.AbrirAsync(
            new NovoChamado("Ar-condicionado da sala 204 desligando", Prioridade.Alta, "Desliga após 10 minutos de uso"),
            cancellationToken);
        await servico.IniciarAtendimentoAsync(arCondicionado.Id, "Equipe de climatização", cancellationToken);

        var vazamento = await servico.AbrirAsync(
            new NovoChamado("Vazamento na copa", Prioridade.Critica, "Água sob a pia"),
            cancellationToken);
        await servico.IniciarAtendimentoAsync(vazamento.Id, "Hidráulica", cancellationToken);
        await servico.ResolverAsync(vazamento.Id, "Sifão substituído", cancellationToken);

        var projetor = await servico.AbrirAsync(new NovoChamado("Projetor sem imagem no auditório", Prioridade.Media), cancellationToken);
        await servico.CancelarAsync(projetor.Id, "Aberto em duplicidade", cancellationToken);

        await servico.AbrirAsync(new NovoChamado("Tomada solta na recepção", Prioridade.Alta), cancellationToken);

        // Módulo 4: mais sete, para a paginação (5 por página) ter três páginas.
        await servico.AbrirAsync(new NovoChamado("Cadeira quebrada na sala 101", Prioridade.Baixa), cancellationToken);

        var wifi = await servico.AbrirAsync(
            new NovoChamado("Wi-Fi instável no bloco B", Prioridade.Alta, "Cai a cada 15 minutos"), cancellationToken);
        await servico.IniciarAtendimentoAsync(wifi.Id, "Redes", cancellationToken);

        await servico.AbrirAsync(new NovoChamado("Impressora do 2º andar sem toner", Prioridade.Media), cancellationToken);
        await servico.AbrirAsync(new NovoChamado("Porta do laboratório 3 não tranca", Prioridade.Critica), cancellationToken);

        var lampada = await servico.AbrirAsync(new NovoChamado("Lâmpada queimada no estacionamento", Prioridade.Baixa), cancellationToken);
        await servico.IniciarAtendimentoAsync(lampada.Id, "Elétrica", cancellationToken);
        await servico.ResolverAsync(lampada.Id, "Lâmpada trocada", cancellationToken);

        await servico.AbrirAsync(new NovoChamado("Bebedouro sem água gelada", Prioridade.Media), cancellationToken);
        await servico.AbrirAsync(
            new NovoChamado("Ar-condicionado da biblioteca pingando", Prioridade.Alta, "Goteira sobre as mesas"),
            cancellationToken);
    }
}
