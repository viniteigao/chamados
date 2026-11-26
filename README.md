# Chamados

Sistema de chamados (ordens de serviço) para uma empresa de manutenção predial. Estado: Módulo 8.

## Requisitos

- .NET SDK 10.0.402 ou superior na faixa 10.0 (fixado em `global.json`)

## Primeira execução: senha dos usuários de exemplo (Módulo 8)

A aplicação exige login. Em Development ela cria três usuários de exemplo, mas a senha deles **não está no repositório**: vem dos Segredos do Usuário, guardados no seu perfil, fora da pasta do projeto. Sem ela, a aplicação para ao iniciar com uma mensagem explicando o que fazer.

No Visual Studio: botão direito em `Chamados.Web` › **Gerenciar Segredos do Usuário** e grave:

```json
{ "Semente:SenhaInicial": "escolha-uma-senha-forte" }
```

Pela CLI: `dotnet user-secrets set "Semente:SenhaInicial" "escolha-uma-senha-forte" --project src/Chamados.Web`.
A senha precisa ter 10 caracteres ou mais, com maiúscula, minúscula, número e símbolo (regras do Identity no `Program.cs`).

| Usuário | Papel ou claim | Pode |
| --- | --- | --- |
| `ana@chamados.local` | papel `Atendente` | tudo o que um usuário conectado faz, mais a fila de atendimento, editar e mudar status |
| `bruno@chamados.local` | nenhum | abrir chamados, ver a lista e os detalhes |
| `carla@chamados.local` | claim `cargo = Coordenacao` | o que o bruno faz, mais os indicadores |

Os usuários são criados uma vez; para trocar a senha depois, apague `chamados.db` e execute de novo.

## Como executar

No Visual Studio: abra `Chamados.slnx`, escolha o perfil **https** e pressione `F5`. O navegador abre no painel (`https://localhost:7270/`).

Pela CLI:

```bash
dotnet build
dotnet run --project src/Chamados.Web --launch-profile https
dotnet test
```

Portas fixas: https 7270, http 5058. Na primeira execução com o perfil https, aceite a confiança no certificado de desenvolvimento: a página Indicadores chama a API por HTTPS.

### Banco de dados (Módulo 7)

Os chamados ficam num arquivo SQLite, `src/Chamados.Web/chamados.db` (string de conexão `ConnectionStrings:Chamados`). Em Development, a aplicação aplica as migrations pendentes ao iniciar e, se o banco estiver vazio, grava 12 chamados de exemplo (três páginas de 5 na lista). Para recomeçar do zero, pare a aplicação e apague `chamados.db`, `chamados.db-shm` e `chamados.db-wal`. O arquivo não vai para o Git (`.gitignore`).

Migrations no Visual Studio (**Ferramentas › Gerenciador de Pacotes do NuGet › Console do Gerenciador de Pacotes**, com o projeto padrão `Chamados.Infra` e `Chamados.Web` como projeto de inicialização). Os comandos vêm do pacote `Microsoft.EntityFrameworkCore.Tools`, já referenciado no `Chamados.Web`:

```powershell
Add-Migration NomeDaMudanca
Update-Database
Script-Migration -Idempotent   # SQL revisável, para produção
```

Pela CLI (ferramenta `dotnet-ef`, instalada uma vez com `dotnet tool install --global dotnet-ef --version 10.0.15`):

```bash
dotnet ef migrations add NomeDaMudanca --project src/Chamados.Infra --startup-project src/Chamados.Web
dotnet ef database update --project src/Chamados.Infra --startup-project src/Chamados.Web
```

O projeto traz duas migrations: `Inicial` (tabelas `Chamados` e `HistoricoChamados`) e `IndicePrioridade` (um índice criado depois, para mostrar o banco evoluindo sem perder dados). Um banco criado só com a primeira recebe a segunda no próximo início.

Em Development, o log mostra cada comando SQL (categoria `Microsoft.EntityFrameworkCore.Database.Command`), com os valores dos parâmetros ocultos.

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

### Páginas Blazor (componentes Razor, Módulos 5 a 7)

| Rota | Componente | O que faz |
| --- | --- | --- |
| `/atendimento` | `Components/Pages/Atendimento.razor` | Fila interativa (InteractiveServer): busca com cancelamento, iniciar, resolver, cancelar, abrir e editar (EditForm) sem recarregar |
| `/modos` | `Components/Pages/Modos.razor` | O mesmo `Contador` renderizado de forma estática e interativa, lado a lado |
| `/indicadores` | `Components/Pages/Indicadores.razor` | Totais lidos pela API com o cliente tipado `ChamadosApiCliente` (Módulo 7), com renderização em fluxo (`[StreamRendering]`) |

Razor Pages e Blazor convivem na mesma aplicação: `MapRazorPages()` e `MapRazorComponents<App>()` no `Program.cs`, o mesmo `site.css` e o mesmo `ChamadoServico`.

Componentes acrescentados no Módulo 6: `Formularios/FormularioChamado` (EditForm com Data Annotations, validação personalizada e erros do servidor), `Layout/ResumoFila` (totais no cabeçalho das páginas Blazor, alimentados pelo serviço `Estado/EstadoFila`) e o módulo JavaScript `wwwroot/js/preferencias.js` (lembra o responsável no `localStorage`).

### API JSON (em `/api` desde o Módulo 3)

| Rota | Função |
| --- | --- |
| `GET /api/chamados?status=&prioridadeMinima=&texto=&ordem=` | Lista com filtros e ordenação (`Prioridade`, `MaisRecentes`, `MaisAntigos`, `Titulo`) |
| `GET /api/chamados/estatisticas` | Totais e tempo médio de resolução |
| `GET /api/chamados/{id}` | Um chamado com o histórico (DTO `DetalheChamadoResposta`, inclui `versao`); id inexistente → 404 |
| `POST /api/chamados` | Abre chamado (corpo: `{"titulo": "...", "prioridade": "Alta"}`); corpo inválido → 400 com erros por campo |
| `PUT /api/chamados/{id}` | Edita (corpo com `versaoEsperada`; versão desatualizada → 409, chamado encerrado → 409) |
| `POST /api/chamados/{id}/atendimento` | Inicia atendimento (corpo: `{"responsavel": "..."}`) |
| `POST /api/chamados/{id}/resolucao` | Resolve (corpo: `{"solucao": "..."}`) |
| `POST /api/chamados/{id}/cancelamento` | Cancela (corpo: `{"motivo": "..."}`) |
| `GET /api/chamados/por-responsavel?nome=` | Chamados de um responsável (Módulo 8; SQL escrito à mão com `FromSql`, parametrizado) |
| `POST /api/conta/token` | Troca e-mail e senha por um token de acesso (Módulo 8; corpo `{"email": "...", "senha": "..."}`) |
| `GET /api/status`, `GET /api/ambiente` | Herdadas do Módulo 1 (`/api/ambiente` só em Development) |

Erros de domínio na API voltam como Problem Details (RFC 9457): 400, 404 ou 409. A API nunca recebe as páginas de erro em HTML. Exemplos em `src/Chamados.Web/Chamados.Web.http`. Em Development, o documento OpenAPI fica em `/openapi/v1.json`.

Desde o Módulo 8, toda a API exige autenticação, pelo cookie do navegador ou pelo cabeçalho `Authorization: Bearer <token>`. Sem nenhum dos dois: 401. Conectado sem permissão: 403. Editar e as três mudanças de status (atendimento, resolução e cancelamento) exigem a política `Atender`; estatísticas, a política `VerIndicadores`. O arquivo `Chamados.Web.http` pede o token primeiro; escolha o ambiente **dev**, cujo arquivo `http-client.env.json` lê a senha dos Segredos do Usuário.

### Configuração

`ConnectionStrings:Chamados` aponta para o arquivo SQLite. `Api:UrlBase` é o endereço que o cliente HTTP da página Indicadores usa: `https://localhost:7270/` no perfil `https`; o perfil `http` troca para `http://localhost:5058/` pela variável `Api__UrlBase` do `launchSettings.json`.

`Simulacao:LatenciaMs` (0 a 10000) atrasa as consultas (lista, contagem e estatísticas) para observar carregamento e cancelamento; em Development vale 300. `Simulacao:FalhaNaListagemACada` (0 a 100) faz uma listagem a cada N falhar de propósito, para testar o tratamento de erro; 0 desliga. Valor fora da faixa impede a aplicação de iniciar (`ValidateOnStart`). Para testar sem editar arquivo:

```powershell
$env:Simulacao__LatenciaMs = "20000"; dotnet run --project src/Chamados.Web
$env:Simulacao__FalhaNaListagemACada = "3"; dotnet run --project src/Chamados.Web
```

No Visual Studio, o perfil **https-falha** (em `launchSettings.json`) já liga `Simulacao__FalhaNaListagemACada = 3` e abre em `/atendimento`; pela CLI: `dotnet run --project src/Chamados.Web --launch-profile https-falha`.

Datas e números saem em pt-BR também no servidor de hospedagem (`UseRequestLocalization("pt-BR")`).

### Segurança e observabilidade (Módulo 8)

- **Login**: `/Conta/Entrar` (ASP.NET Core Identity, senha guardada só como hash, bloqueio de 5 minutos após 5 erros seguidos). Logout por POST em `/Conta/Sair`.
- **Autorização**: política de fallback exige login em tudo; exceções explícitas com `[AllowAnonymous]` (login e páginas de erro). Políticas em `Seguranca/Politicas.cs`.
- **Cabeçalhos**: `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options` e `Referrer-Policy` em todas as respostas (`Seguranca/CabecalhosSeguranca.cs`).
- **Logs estruturados**: eventos com Id e campos nomeados em `Diagnostico/Registro.cs`. O perfil **https-json** mostra o log em JSON, com `TraceId` em cada linha; o mesmo `traceId` volta nas respostas de erro (Problem Details).
- **Métricas**: medidor `Chamados` com `chamados.abertos` (por prioridade) e `chamados.transicoes` (por status). Para ver ao vivo: `dotnet tool install --global dotnet-counters` e `dotnet-counters monitor --name Chamados.Web --counters Chamados,Microsoft.AspNetCore.Hosting`.
- **Análise estática**: `Directory.Build.props` liga `AnalysisLevel=latest-recommended` em todos os projetos e transforma em erro os avisos de injeção de SQL (`EF1002`, `EF1003`) e de comando SQL montado com texto (`CA2100`).
- **Dependências**: a restauração audita os pacotes, inclusive os transitivos (`NuGetAuditMode=all`). Conferência manual: `dotnet package list --vulnerable --include-transitive` e `dotnet package list --outdated`. O `.github/dependabot.yml` pede atualizações semanais.

### Bibliotecas do lado do cliente

`wwwroot/lib` traz jQuery 3.7.1, jQuery Validation 1.21.0 e jQuery Validation Unobtrusive 4.0.0 (licenças MIT e Apache 2.0, arquivos de licença incluídos), usados só pela validação no cliente. O `libman.json` registra a origem; no Visual Studio, botão direito em `libman.json` › **Restaurar Bibliotecas do Lado do Cliente** baixa de novo.

## Testar

No Visual Studio: **Teste > Gerenciador de Testes > Executar Todos** (`Ctrl+R, A`). Pela CLI: `dotnet test`. São 70 testes: 43 do domínio, 9 de integração com o banco e 18 da aplicação web (integração com `WebApplicationFactory`, componentes com `HtmlRenderer` e unidade das políticas de autorização).

## Estrutura

| Projeto | Papel | Depende de |
| --- | --- | --- |
| `src/Chamados.Domain` | Entidades, regras e casos de uso | — |
| `src/Chamados.Infra` | EF Core: `ChamadosDbContext`, mapeamento (Fluent API), migrations e `RepositorioChamadosEf` | Domain |
| `src/Chamados.Web` | Páginas Razor, componentes Blazor, API JSON, composição e pipeline | Domain, Infra |
| `tests/Chamados.Domain.Tests` | Testes xUnit do domínio (repositório em memória) | Domain |
| `tests/Chamados.Infra.Tests` | Testes de integração do repositório com SQLite em memória | Infra |
| `tests/Chamados.Web.Tests` | Aplicação inteira em memória (`WebApplicationFactory`), componentes e políticas | Web |

## Decisões

- **.NET 10 (LTS) como alvo**: versão com suporte até 14/11/2028, cobrindo todo o curso.
- **Formato `.slnx`**: padrão do `dotnet new sln` no SDK 10; XML enxuto, sem GUIDs. `Chamados.Web` é o projeto de inicialização (`DefaultStartup="true"`).
- **Interface `IChamadoRepositorio`**: existe porque havia uma segunda implementação prevista. Até o Módulo 6 os dados ficavam em memória; no Módulo 7 entrou o Entity Framework Core, escolhido no `Program.cs`, e a versão em memória continua servindo aos testes do domínio. O `ChamadoServico` não tem interface porque não há segunda implementação prevista para ele.
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
- **Blazor no mesmo projeto Web** (Módulo 5): Razor Pages ficam com os formulários de cadastro; a fila de atendimento, que muda o tempo todo, vira um componente interativo no servidor (`InteractiveServer`). Nenhum projeto novo.
- **Interatividade por página, não global** (Módulo 5): só `/atendimento` declara `@rendermode InteractiveServer`. O resto continua estático, sem conexão aberta com o servidor. No Módulo 6, o `ResumoFila` do cabeçalho das páginas Blazor também é interativo, com a própria raiz no circuito.
- **Componente pede, página executa** (Módulo 5): `CartaoChamado` só avisa a página por `EventCallback<PedidoTransicao>`; quem chama o `ChamadoServico` e trata as exceções é `Atendimento.razor`.
- **`ChamadoInput` também no Blazor** (Módulo 6): o `EditForm` usa o mesmo modelo e as mesmas Data Annotations das Razor Pages. A regra nova, `[TextoSignificativo]`, vale nas duas telas.
- **Erros do servidor no `EditContext`** (Módulo 6): concorrência e regras do domínio entram por um `ValidationMessageStore` e aparecem no `ValidationSummary`. O envio usa `OnSubmit` com `Validate()` manual, para limpar esses erros antes de validar de novo.
- **Estado compartilhado em serviço Scoped com evento** (Módulo 6): `EstadoFila` liga a página de atendimento ao resumo do cabeçalho sem relação pai e filho. Quem se inscreve no evento cancela a inscrição no `Dispose`.
- **JavaScript só no `OnAfterRenderAsync`, em módulo ES isolado** (Módulo 6): nada de JS na pré-renderização; o `localStorage` guarda só preferência de tela, nunca dado sensível.
- **Fila sem busca duplicada** (Módulo 6): `[PersistentState]` leva a lista carregada na pré-renderização para o circuito; cada busca nova cancela a anterior com `CancellationTokenSource`.
- **EF Core com SQLite num projeto próprio** (Módulo 7): `Chamados.Infra` implementa `IChamadoRepositorio`; o domínio continua sem referência a EF. A única concessão é um construtor privado sem parâmetros no `Chamado`, para o EF materializar objetos lidos do banco.
- **Mapeamento só por Fluent API** (Módulo 7), numa classe `IEntityTypeConfiguration` por entidade; o histórico é entidade *owned* (tabela `HistoricoChamados`), carregada e gravada sempre com o chamado.
- **Consultas no banco, não em memória** (Módulo 7): o repositório recebe o filtro e devolve resumos projetados; `Where`, `ORDER BY`, `LIMIT/OFFSET` e `GROUP BY` viram SQL. O texto buscado vai como parâmetro do `LIKE`, com `%` e `_` escapados. O repositório em memória mantém a mesma regra para os testes do domínio.
- **Um DbContext por operação, criado por `IDbContextFactory`** (Módulo 7): no Blazor Server, um contexto por circuito seria usado por dois componentes ao mesmo tempo (a fila e o resumo do cabeçalho), o que o EF não permite.
- **Concorrência no banco** (Módulo 7): `Versao` é token de concorrência; o UPDATE leva `WHERE Versao = <lida>` e a falha vira `ConcorrenciaException`, tratada também na página de detalhes e na fila.
- **DateTimeOffset gravado como número** (Módulo 7, `DateTimeOffsetToBinaryConverter`): o SQLite não ordena nem compara datas com fuso guardadas como texto. O custo é a precisão: frações abaixo do milissegundo se perdem na gravação.
- **API com DTOs, validação automática e OpenAPI** (Módulo 7): a entidade nunca é serializada; `AddValidation()` confere as Data Annotations do corpo (o mesmo `ChamadoInput` das telas); o documento OpenAPI é publicado só em Development.
- **Toda mudança de esquema por migration** (Módulo 7): nada de `EnsureCreated` nem de editar o banco à mão. Se o modelo muda sem migration, o `MigrateAsync` recusa iniciar (`PendingModelChangesWarning`) e um teste de integração (`HasPendingModelChanges`) acusa antes.
- **ASP.NET Core Identity com EF Core, no mesmo banco** (Módulo 8): usuários, papéis e claims nas tabelas `AspNet*`, criadas pela migration `Identidade`. Login próprio, em Razor Pages, sem a interface pronta do Identity: menos código para ler e nenhum cadastro aberto.
- **Política de fallback** (Módulo 8): tudo exige login, e o que é público declara `[AllowAnonymous]`. Esquecer de proteger uma página nova não a deixa aberta.
- **Papel para quem atende, claim para quem coordena** (Módulo 8): `Atender` exige o papel `Atendente`; `VerIndicadores`, a claim `cargo = Coordenacao`. Mostrar ou esconder links é conforto; quem protege são as páginas, os handlers e os endpoints. Os endpoints próprios deste projeto (`/resolucao` e `/cancelamento`) também exigem `Atender`, como os handlers da página Detalhes.
- **Cookie para o navegador, token para outros clientes** (Módulo 8): a API aceita os dois esquemas (`Politicas.Api`) e responde 401/403, nunca redireciona. O token sai de `POST /api/conta/token`, não de `MapIdentityApi`, para não publicar cadastro aberto.
- **Nenhum segredo no repositório** (Módulo 8): a senha dos usuários de exemplo vem dos Segredos do Usuário; sem ela a aplicação não inicia. O arquivo `.http` lê a mesma chave pelo `http-client.env.json`.
- **SQL à mão só com parâmetros** (Módulo 8): `FromSql` com string interpolada vira parâmetro; `FromSqlRaw` com texto montado é bloqueado na compilação (`EF1002`/`EF1003` como erro).
- **Observabilidade sem pacotes extras** (Módulo 8): logs com `[LoggerMessage]`, correlação por `TraceId` e métricas com `System.Diagnostics.Metrics`. Exportar para um painel (OpenTelemetry) fica para quando houver onde guardar os dados.
- **Teste de componente com `HtmlRenderer`** (Módulo 8), que vem no ASP.NET Core: confere o HTML de cada estado. Interação (clique, digitação) pede a biblioteca bUnit, da comunidade, não usada aqui.
