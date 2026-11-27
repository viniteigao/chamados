namespace Chamados.Domain;

/// <summary>
/// Projeção de um chamado resolvido, só com o que o cálculo de prazo precisa.
/// Subtrair datas não tem tradução para SQL no SQLite: o banco devolve as colunas e o
/// serviço decide, em C#, se o prazo foi cumprido.
/// </summary>
public sealed record ResolucaoChamado(Prioridade Prioridade, DateTimeOffset CriadoEm, DateTimeOffset ResolvidoEm);
