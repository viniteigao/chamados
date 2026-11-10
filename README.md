# Chamados

Sistema de chamados (ordens de serviço) para uma empresa de manutenção predial. Estado: Módulo 4.

## Requisitos

- .NET SDK 10.0.402 ou superior na faixa 10.0 (fixado em `global.json`)

## Como executar

No Visual Studio: abra `Chamados.slnx`, escolha o perfil **https** e pressione `F5`. O navegador abre no painel (`https://localhost:7270/`).

Pela CLI:

```bash
dotnet build
dotnet run --project src/Chamados.Web --launch-profile https
dotnet test
```

Em Development a base em memória recebe 12 chamados de exemplo (três páginas de 5 na lista).

### Páginas (Razor Pages)

| Rota | Página | O que faz |
| --- | --- | --- |
| `/` | `Pages/Index` | Painel com totais por status (`ObterEstatisticasAsync`) |
| `/Chamados?status=&prioridadeMinima=&texto=&ordem=&pagina=` | `Pages/Chamados/Index` | Lista com filtros, ordenação e paginação (`ListarPaginaAsync`) |
| `/Chamados/Novo` | `Pages/Chamados/Novo` | Formulário de abertura (`AbrirAsync`), validação no cliente e no servidor, Post/Redirect/Get |
| `/Chamados/Detalhes/{id}` | `Pages/Chamados/Detalhes` | Detalhe, histórico e handlers Iniciar, Resolver e Cancelar |
| `/Chamados/Editar/{id}` | `Pages/Chamados/Editar` | Edição com concorrência otimista (`EditarAsync`) |
| `/Erro`, `/Erro/{código}` | `Pages/Erro` | Erros sem detalhes internos; 404 e 400 amigáveis fora de `/api` |

O cabeçalho de todas as páginas mostra o contador de pendências (View Component `ResumoStatus`).

### API JSON (em `/api` desde o Módulo 3)

| Rota | Função |
| --- | --- |
| `GET /api/chamados?status=&prioridadeMinima=&texto=&ordem=` | Lista com filtros e ordenação (`Prioridade`, `MaisRecentes`, `MaisAntigos`, `Titulo`) |
| `GET /api/chamados/estatisticas` | Totais e tempo médio de resolução |
| `GET /api/chamados/{id}` | Um chamado (inclui `versao`) |
| `POST /api/chamados` | Abre chamado (corpo: `{"titulo": "...", "prioridade": "Alta"}`) |
| `PUT /api/chamados/{id}` | Edita (corpo com `versaoEsperada`; versão desatualizada → 409, chamado encerrado → 409) |
| `POST /api/chamados/{id}/atendimento` | Inicia atendimento (corpo: `{"responsavel": "..."}`) |
| `POST /api/chamados/{id}/resolucao` | Resolve (corpo: `{"solucao": "..."}`) |
| `POST /api/chamados/{id}/cancelamento` | Cancela (corpo: `{"motivo": "..."}`) |
| `GET /api/status`, `GET /api/ambiente` | Herdadas do Módulo 1 (`/api/ambiente` só em Development) |

Erros de domínio na API voltam como Problem Details (RFC 9457): 400, 404 ou 409. A API nunca recebe as páginas de erro em HTML. Exemplos em `src/Chamados.Web/Chamados.Web.http`.

### Configuração

`Simulacao:LatenciaMs` (0 a 10000) atrasa a listagem para observar cancelamento. Valor fora da faixa impede a aplicação de iniciar (`ValidateOnStart`). Para testar sem editar arquivo:

```powershell
$env:Simulacao__LatenciaMs = "20000"; dotnet run --project src/Chamados.Web
```

### Bibliotecas do lado do cliente

`wwwroot/lib` traz jQuery 3.7.1, jQuery Validation 1.21.0 e jQuery Validation Unobtrusive 4.0.0 (licenças MIT e Apache 2.0, arquivos de licença incluídos), usados só pela validação no cliente. O `libman.json` registra a origem; no Visual Studio, botão direito em `libman.json` › **Restaurar Bibliotecas do Lado do Cliente** baixa de novo.

## Testar

No Visual Studio: **Teste > Gerenciador de Testes > Executar Todos** (`Ctrl+R, A`). Pela CLI: `dotnet test` (43 testes).

## Estrutura

| Projeto | Papel | Depende de |
| --- | --- | --- |
| `src/Chamados.Domain` | Entidades, regras e casos de uso | — |
| `src/Chamados.Web` | Páginas Razor, API JSON, composição e pipeline | Domain |
| `tests/Chamados.Domain.Tests` | Testes xUnit do domínio | Domain |

## Decisões

- **.NET 10 (LTS) como alvo**: versão com suporte até 14/11/2028, cobrindo todo o curso.
- **Formato `.slnx`**: padrão do `dotnet new sln` no SDK 10; XML enxuto, sem GUIDs. `Chamados.Web` é o projeto de inicialização (`DefaultStartup="true"`).
- **Interface `IChamadoRepositorio`**: existe porque há uma segunda implementação prevista. Hoje os dados ficam em memória; no Módulo 7 entra o Entity Framework Core. A troca muda uma linha no `Program.cs`, sem alterar o serviço nem os testes. O `ChamadoServico` não tem interface porque não há segunda implementação prevista para ele.
- **`TimeProvider` injetado**: o serviço e o repositório recebem o relógio pelo construtor em vez de ler `DateTimeOffset.UtcNow`. Em produção é usado `TimeProvider.System`; nos testes, o `RelogioFixo`, que torna datas e durações previsíveis.
- **Casos de uso no Domain, sem camada Application**: o `ChamadoServico` fica em `Chamados.Domain/Servicos` enquanto o projeto é pequeno. Separar em mais projetos só se justifica quando chegar o banco de dados (Módulo 7).
- **Razor Pages para os formulários** (Módulo 3): uma página por tela, com o `PageModel` chamando só o `ChamadoServico`, nunca o repositório.
- **Validação em duas camadas**: Data Annotations no modelo do formulário (mensagem amigável) e invariantes no domínio (palavra final). Os limites de tamanho são constantes do `Chamado`, reusadas nos atributos.
- **Validação no cliente é conforto, não segurança** (Módulo 4): jQuery Validation mostra as mensagens sem POST, mas o servidor continua validando sempre (`ModelState.IsValid` e o domínio).
- **API sob `/api`** com `MapGroup` e um filtro único de exceções de domínio (`FiltroExcecoesDominio`), para não disputar a rota `/Chamados` com as páginas e não repetir `try/catch` em cada rota.
- **Concorrência otimista com número de versão** (Módulo 4): `Chamado.Versao` aumenta a cada alteração; quem edita informa a versão lida, e a gravação é recusada com `ConcorrenciaException` se ela mudou. No Módulo 7 o EF Core assume a conferência com um token de concorrência.
- **Paginação no serviço, não na página** (Módulo 4): `ListarPaginaAsync` devolve `Pagina<T>` e ajusta números de página fora da faixa. Quando o EF Core chegar, `Skip`/`Take` viram SQL sem mudar as páginas.
- **Partial para o que só exibe, View Component para o que busca dados, Tag Helper para marcação repetida** (Módulo 4): `_CamposChamado`, `ResumoStatus` e `<selo>`.
- **Páginas de status só fora de `/api`** (Módulo 4): `UseWhen` cria um ramo do pipeline para que a API continue respondendo JSON.
