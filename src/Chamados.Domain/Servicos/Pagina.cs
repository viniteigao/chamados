namespace Chamados.Domain;

/// <summary>Um pedaço de uma listagem, com o necessário para montar a navegação.</summary>
public sealed record Pagina<T>(IReadOnlyList<T> Itens, int Numero, int TamanhoPagina, int TotalItens)
{
    // Sem itens, ainda existe uma página (vazia): evita "página 1 de 0".
    public int TotalPaginas => TotalItens == 0 ? 1 : (TotalItens + TamanhoPagina - 1) / TamanhoPagina;

    public bool TemAnterior => Numero > 1;

    public bool TemProxima => Numero < TotalPaginas;
}
