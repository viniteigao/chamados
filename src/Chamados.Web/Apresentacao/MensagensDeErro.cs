namespace Chamados.Web.Apresentacao;

/// <summary>
/// Melhorias: as regras do domínio lançam ArgumentException com mensagem pensada para o usuário,
/// mas o .NET acrescenta " (Parameter 'titulo')" e, em ArgumentOutOfRangeException,
/// "Actual value was 99." numa linha nova. Telas e API mostram só a frase do domínio.
/// </summary>
public static class MensagensDeErro
{
    public static string ParaUsuario(this ArgumentException excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);

        // O sufixo do parâmetro vem sempre em inglês: o .NET não traduz mensagens do runtime.
        var mensagem = excecao.Message;
        var corte = mensagem.IndexOf(" (Parameter '", StringComparison.Ordinal);
        return corte >= 0 ? mensagem[..corte] : mensagem;
    }
}
