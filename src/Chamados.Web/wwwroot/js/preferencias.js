// Módulo JavaScript carregado pelo Blazor com import() (JS interop).
// Guarda preferências deste navegador no localStorage.
// Atenção: qualquer script da mesma origem lê o localStorage. Nunca guarde senha ou token aqui.

export function ler(chave) {
  try {
    return localStorage.getItem(chave);
  } catch {
    // Armazenamento bloqueado (modo privado, política do navegador): segue sem preferência.
    return null;
  }
}

export function gravar(chave, valor) {
  try {
    localStorage.setItem(chave, valor);
  } catch {
    // Sem armazenamento, a aplicação continua funcionando; só não lembra.
  }
}
