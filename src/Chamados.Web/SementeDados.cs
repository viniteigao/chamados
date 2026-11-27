using Chamados.Domain;
using Microsoft.AspNetCore.Identity;

namespace Chamados.Web;

/// <summary>
/// Popula o banco com exemplos, apenas em Development e só se ele estiver vazio.
/// Meus chamados: cada exemplo tem dono (ana, bruno ou carla), e as mudanças de status são da ana,
/// a atendente. Por isso a semente de usuários roda antes desta (Program.cs).
/// </summary>
public static class SementeDados
{
    public static async Task PopularAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        // ChamadoServico é Scoped: fora de uma requisição, é preciso criar um escopo.
        await using var escopo = services.CreateAsyncScope();
        var servico = escopo.ServiceProvider.GetRequiredService<ChamadoServico>();

        // Módulo 7: o banco sobrevive ao reinício. Sem esta verificação, cada F5 duplicaria os exemplos.
        if ((await servico.ListarPaginaAsync(FiltroChamados.Todos, 1, 1, cancellationToken)).TotalItens > 0)
        {
            return;
        }

        var usuarios = escopo.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var ana = await AutorAsync(usuarios, "ana@chamados.local");
        var bruno = await AutorAsync(usuarios, "bruno@chamados.local");
        var carla = await AutorAsync(usuarios, "carla@chamados.local");

        await servico.AbrirAsync(new NovoChamado("Trocar lâmpada do corredor", Prioridade.Baixa, AbertoPor: bruno), cancellationToken);

        var arCondicionado = await servico.AbrirAsync(
            new NovoChamado("Ar-condicionado da sala 204 desligando", Prioridade.Alta, "Desliga após 10 minutos de uso", carla),
            cancellationToken);
        await servico.IniciarAtendimentoAsync(arCondicionado.Id, "Equipe de climatização", ana, cancellationToken);

        var vazamento = await servico.AbrirAsync(
            new NovoChamado("Vazamento na copa", Prioridade.Critica, "Água sob a pia", ana),
            cancellationToken);
        await servico.IniciarAtendimentoAsync(vazamento.Id, "Hidráulica", ana, cancellationToken);
        await servico.ResolverAsync(vazamento.Id, "Sifão substituído", ana, cancellationToken);

        var projetor = await servico.AbrirAsync(
            new NovoChamado("Projetor sem imagem no auditório", Prioridade.Media, AbertoPor: bruno), cancellationToken);
        await servico.CancelarAsync(projetor.Id, "Aberto em duplicidade", ana, cancellationToken);

        await servico.AbrirAsync(new NovoChamado("Tomada solta na recepção", Prioridade.Alta, AbertoPor: ana), cancellationToken);

        // Módulo 4: mais sete, para a paginação (5 por página) ter três páginas.
        await servico.AbrirAsync(new NovoChamado("Cadeira quebrada na sala 101", Prioridade.Baixa, AbertoPor: bruno), cancellationToken);

        var wifi = await servico.AbrirAsync(
            new NovoChamado("Wi-Fi instável no bloco B", Prioridade.Alta, "Cai a cada 15 minutos", carla), cancellationToken);
        await servico.IniciarAtendimentoAsync(wifi.Id, "Redes", ana, cancellationToken);

        await servico.AbrirAsync(new NovoChamado("Impressora do 2º andar sem toner", Prioridade.Media, AbertoPor: bruno), cancellationToken);
        await servico.AbrirAsync(new NovoChamado("Porta do laboratório 3 não tranca", Prioridade.Critica, AbertoPor: ana), cancellationToken);

        var lampada = await servico.AbrirAsync(
            new NovoChamado("Lâmpada queimada no estacionamento", Prioridade.Baixa, AbertoPor: ana), cancellationToken);
        await servico.IniciarAtendimentoAsync(lampada.Id, "Elétrica", ana, cancellationToken);
        await servico.ResolverAsync(lampada.Id, "Lâmpada trocada", ana, cancellationToken);

        await servico.AbrirAsync(new NovoChamado("Bebedouro sem água gelada", Prioridade.Media, AbertoPor: bruno), cancellationToken);
        await servico.AbrirAsync(
            new NovoChamado("Ar-condicionado da biblioteca pingando", Prioridade.Alta, "Goteira sobre as mesas", carla),
            cancellationToken);
    }

    // Usuário de exemplo como autor; null se ele não existir (o chamado fica sem dono).
    private static async Task<Autor?> AutorAsync(UserManager<IdentityUser> usuarios, string email)
        => await usuarios.FindByEmailAsync(email) is { UserName: { } nome } usuario ? new Autor(usuario.Id, nome) : null;
}
