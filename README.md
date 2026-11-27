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
| `ana@chamados.local` | papel `Atendente` | ver todos os chamados, a fila de atendimento, editar e mudar status |
| `bruno@chamados.local` | nenhum | abrir chamados e acompanhar só os que abriu ("Meus chamados") |
| `carla@chamados.local` | claim `cargo = Coordenacao` | ver todos os chamados e os indicadores |

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

O projeto traz cinco migrations: `Inicial` (tabelas `Chamados` e `HistoricoChamados`), `IndicePrioridade` (um índice criado depois, para mostrar o banco evoluindo sem perder dados), `Identidade` (tabelas de usuários do Módulo 8), `AutoriaDosChamados` (quem abriu e quem fez cada mudança) e `ComentariosDosChamados` (tabela `ComentariosChamados`). Um banco antigo recebe as que faltam no próximo início, sem perder dados.

Em Development, o log mostra cada comando SQL (categoria `Microsoft.EntityFrameworkCore.Database.Command`), com os valores dos parâmetros ocultos.

### Páginas (Razor Pages)

| Rota | Página | O que faz |
| --- | --- | --- |
| `/` | `Pages/Index` | Painel com totais por status (`ObterEstatisticasAsync`) |
| `/Chamados?status=&prioridadeMinima=&texto=&ordem=&pagina=` | `Pages/Chamados/Index` | Lista com filtros, ordenação e paginação (`ListarPaginaAsync`) |
| `/Chamados/Novo` | `Pages/Chamados/Novo` | Formulário de abertura (`AbrirAsync`), validação no cliente e no servidor, Post/Redirect/Get |
| `/Chamados/Detalhes/{id}` | `Pages/Chamados/Detalhes` | Detalhe, histórico, conversa e handlers Iniciar, Resolver, Cancelar e Comentar |
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
| `GET /api/chamados?status=&prioridadeMinima=&texto=&ordem=&somenteVencidos=&meus=` | Lista com filtros e ordenação (`Prioridade`, `MaisRecentes`, `MaisAntigos`, `Titulo`); `somenteVencidos=true` traz só os em andamento com o prazo vencido; `meus=true` só os que o usuário abriu (quem não é atendente nem da coordenação sempre recebe só os seus) |
| `GET /api/chamados/estatisticas` | Totais e tempo médio de resolução |
| `GET /api/chamados/prazos` | Vencidos, perto de vencer, percentual resolvido no prazo e a tabela de prazos (política `VerIndicadores`) |
| `GET /api/chamados/{id}` | Um chamado com o histórico (com quem fez cada mudança), o prazo, quem abriu e a conversa (DTO `DetalheChamadoResposta`, inclui `versao`); id inexistente ou de outra pessoa → 404 |
| `GET /api/chamados/{id}/comentarios` | As mensagens da conversa, da mais antiga para a mais nova, cada uma com `texto`, `em` (data e hora) e `autor` |
| `POST /api/chamados/{id}/comentarios` | Escreve uma mensagem (corpo: `{"texto": "..."}`) → 201; vazia ou com mais de 2000 caracteres → 400 |
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

`LimiteDeLogin:TentativasPorMinuto` (1 a 1000, padrão 10) é quantas tentativas de login cada endereço IP pode fazer por minuto, somando o formulário e o endpoint de token.

`Prazos:Critica`, `Prazos:Alta`, `Prazos:Media` e `Prazos:Baixa` são os prazos de atendimento, no formato de `TimeSpan` (`"04:00:00"` = 4 horas, `"3.00:00:00"` = 3 dias). Padrão: 4 horas, 1 dia, 3 dias e 7 dias. Cada prazo precisa ser maior que zero, e uma prioridade mais alta não pode ter prazo maior que uma mais baixa; senão, a aplicação não inicia e diz qual prioridade está errada.

Datas e números saem em pt-BR também no servidor de hospedagem (`UseRequestLocalization("pt-BR")`).

### Segurança e observabilidade (Módulo 8)

- **Login**: `/Conta/Entrar` (ASP.NET Core Identity, senha guardada só como hash, bloqueio de 5 minutos após 5 erros seguidos). Logout por POST em `/Conta/Sair`.
- **Autorização**: política de fallback exige login em tudo; exceções explícitas com `[AllowAnonymous]` (login e páginas de erro). Políticas em `Seguranca/Politicas.cs`.
- **Cabeçalhos**: `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options` e `Referrer-Policy` em todas as respostas (`Seguranca/CabecalhosSeguranca.cs`).
- **Logs estruturados**: eventos com Id e campos nomeados em `Diagnostico/Registro.cs`. O perfil **https-json** mostra o log em JSON, com `TraceId` em cada linha; o mesmo `traceId` volta nas respostas de erro (Problem Details).
- **Métricas**: medidor `Chamados` com `chamados.abertos` (por prioridade), `chamados.transicoes` (por status) e, desde os prazos, `chamados.resolvidos` (dentro ou fora do prazo). Para ver ao vivo: `dotnet tool install --global dotnet-counters` e `dotnet-counters monitor --name Chamados.Web --counters Chamados,Microsoft.AspNetCore.Hosting`.
- **Análise estática**: `Directory.Build.props` liga `AnalysisLevel=latest-recommended` em todos os projetos e transforma em erro os avisos de injeção de SQL (`EF1002`, `EF1003`) e de comando SQL montado com texto (`CA2100`).
- **Dependências**: a restauração audita os pacotes, inclusive os transitivos (`NuGetAuditMode=all`). Conferência manual: `dotnet package list --vulnerable --include-transitive` e `dotnet package list --outdated`. O `.github/dependabot.yml` pede atualizações semanais.

### Melhorias depois do curso

Feitas na branch `melhorias`, a partir da análise do projeto ao fim do Módulo 8:

- **Erros da API sempre em JSON**: em produção, um erro 500 em `/api` responde Problem Details com `traceId` (antes respondia a página HTML de erro). As páginas continuam com a página `/Erro`.
- **Página de erro sem banco**: a página de erro não mostra o contador do cabeçalho, que consulta o banco, e o contador falha em silêncio (com log) em qualquer página. Antes, com o banco fora do ar, a própria página de erro caía e o usuário recebia uma tela em branco.
- **Limite de tentativas de login** (`Seguranca/LimiteDeLogin.cs`): 10 por minuto por endereço IP, no formulário e no token; acima disso, 429 com `Retry-After`. O bloqueio do Identity (5 erros, 5 minutos) continua protegendo cada conta.
- **Login pela API no log**: o endpoint de token registra os mesmos eventos do formulário (1001 a 1003), pelo Id do usuário; o limite atingido é o evento 1008.
- **Tamanho máximo em todos os textos**: responsável (120), solução e motivo (2000) validados no domínio, nas telas (`maxlength`) e nos corpos da API (400 com o erro no campo).
- **Mensagens limpas**: erros do domínio aparecem sem o "(Parameter '…')" que o .NET acrescenta (`Apresentacao/MensagensDeErro.cs`), na API e nas telas.
- **Integração contínua** (`.github/workflows/ci.yml`): cada push e pull request compila com aviso tratado como erro e roda todos os testes no GitHub. O Dependabot não propõe mais a versão 11 do EF Core e do ASP.NET Core (exigiria o .NET 11) e passa a atualizar as actions do CI.
- **`.gitattributes`**: fins de linha normalizados (LF no repositório, o padrão de cada sistema no checkout).

### Prazos de atendimento (SLA)

Feitos na branch `prazos`. Cada prioridade tem um prazo, contado em horas corridas desde a abertura: Crítica 4 horas, Alta 1 dia, Média 3 dias, Baixa 7 dias (configuráveis em `Prazos`).

| Situação | Quando |
| --- | --- |
| No prazo | em andamento, com um quarto do prazo ou mais pela frente |
| Vence logo | em andamento, com menos de um quarto do prazo pela frente (Crítica: menos de 1 hora) |
| Vencido | em andamento, depois do vencimento |
| Cumprido | resolvido até o vencimento |
| Fora do prazo | resolvido depois do vencimento |

Chamado cancelado não tem prazo. Onde aparece:

- **Fila** (`/atendimento`): selo e frase em cada cartão ("vence em 18 h 05 min", "venceu há 1 h 32 min"), com borda vermelha nos vencidos e laranja nos que vencem logo. A tela se atualiza sozinha a cada minuto, sem consultar o banco.
- **Detalhe**: linha "Prazo" com o vencimento e a situação; nos resolvidos, a folga ou o atraso.
- **Lista** (`/Chamados`): coluna "Prazo" e o filtro "Só vencidos" (`?somenteVencidos=true`), aplicado no banco.
- **Painel** (`/`): quantos estão vencidos, com o atalho para a lista filtrada.
- **Indicadores** (`/indicadores`): vencidos agora, vencem logo, percentual resolvido no prazo e a tabela de prazos, lidos de `GET /api/chamados/prazos`.
- **API**: o detalhe traz `prazo: { vencimento, situacao }`; a lista aceita `somenteVencidos`.
- **Métrica**: `chamados.resolvidos`, com a etiqueta `no_prazo` (verdadeiro ou falso).

### Meus chamados

Feito na branch `meus-chamados`. Cada chamado guarda quem o abriu, e o histórico guarda quem fez cada mudança de status (migration `AutoriaDosChamados`).

| Quem | Vê |
| --- | --- |
| Atendentes (papel `Atendente`) e coordenação (claim `cargo = Coordenacao`) | todos os chamados; podem pedir só os seus ("Só os meus", `?meus=true`) |
| Os demais usuários (como o bruno) | só os chamados que abriram: lista, detalhe, painel, busca e API |

- **Detalhe**: chamado de outra pessoa responde 404, como se não existisse (um "acesso negado" confirmaria que o Id existe). A tentativa fica no log (evento 1009). Mostra "Aberto por" e, no histórico, "por" quem fez cada mudança.
- **Lista** (`/Chamados`): para quem vê só os seus, o título vira "Meus chamados"; quem vê todos ganha o filtro "Só os meus". O menu mostra "Meus chamados" para todos e "Lista" só para quem vê todos.
- **Painel** (`/`): para quem vê só os seus, os números são dos chamados que abriu. O contador da fila no cabeçalho aparece só para quem vê todos.
- **Fila** (`/atendimento`): cada cartão mostra quem pediu.
- **API**: a lista e a busca por responsável respeitam a mesma regra; o detalhe traz `abertoPor` e, em cada registro do histórico, `por` (só nomes; o Id do usuário não sai na API).
- **Chamados antigos**: os criados antes deste recurso ficam sem dono e aparecem só para atendentes e coordenação. Num banco novo, a semente já cria os exemplos com dono (5 do bruno, 3 da carla e 4 da ana).

### Comentários (conversa do chamado)

Feito na branch `comentarios`. Cada chamado tem uma conversa entre quem pediu e quem atende (tabela `ComentariosChamados`, migration `ComentariosDosChamados`).

- **Quem participa**: quem pode ver o chamado pode ler e escrever na conversa: o dono, atendentes e coordenação. Para os demais, 404, como no detalhe (evento 1009 no log). Não é preciso ser atendente para comentar.
- **Data e hora**: cada mensagem mostra o autor e quando foi escrita, no formato "26/11/2026 às 14:32", dentro de um `<time datetime="2026-11-26T14:32:05-03:00">` (o mesmo instante em ISO 8601, para leitores de tela e scripts). A hora vem do relógio do servidor, nunca do formulário ou do corpo da requisição.
- **Detalhe** (`/Chamados/Detalhes/{id}`): seção "Conversa" no fim da página, em balões. As mensagens do usuário conectado aparecem à direita, como "Você"; as do dono do chamado levam a marca "solicitante". Depois de enviar, a página volta direto para a conversa (`#conversa`).
- **Lista e fila**: um chamado com conversa mostra "3 mensagens", com link para ela. A contagem é feita no banco (`COUNT` correlacionado); as mensagens não saem do banco na listagem.
- **API**: `GET` e `POST /api/chamados/{id}/comentarios`; o detalhe traz `comentarios`, e a lista, `totalComentarios`. Só o nome do autor sai na API.
- **Log**: evento 2007 a cada mensagem, só com o Id do chamado e o do usuário (o texto não vai para o log).
- **Semente**: num banco novo, o chamado do ar-condicionado da sala 204 (da carla) já tem três mensagens, e o da impressora (do bruno), uma resposta da ana.

### Bibliotecas do lado do cliente

`wwwroot/lib` traz jQuery 3.7.1, jQuery Validation 1.21.0 e jQuery Validation Unobtrusive 4.0.0 (licenças MIT e Apache 2.0, arquivos de licença incluídos), usados só pela validação no cliente. O `libman.json` registra a origem; no Visual Studio, botão direito em `libman.json` › **Restaurar Bibliotecas do Lado do Cliente** baixa de novo.

## Testar

No Visual Studio: **Teste > Gerenciador de Testes > Executar Todos** (`Ctrl+R, A`). Pela CLI: `dotnet test`. São 151 testes: 88 do domínio (com a política de prazos e um relógio controlado), 16 de integração com o banco e 47 da aplicação web (integração com `WebApplicationFactory`, inclusive em ambiente Production com o banco falhando, com prazos de milissegundos, com os três usuários vendo cada um o que pode e conversando nos chamados, componentes com `HtmlRenderer` e unidade das políticas de autorização). O GitHub roda os mesmos testes a cada push (`.github/workflows/ci.yml`).

## Estrutura

| Projeto | Papel | Depende de |
| --- | --- | --- |
| `src/Chamados.Domain` | Entidades, regras e casos de uso | — |
| `src/Chamados.Infra` | EF Core: `ChamadosDbContext`, mapeamento (Fluent API), migrations e `RepositorioChamadosEf` | Domain |
| `src/Chamados.Web` | Páginas Razor, componentes Blazor, API JSON, composição e pipeline | Domain, Infra |
| `tests/Chamados.Domain.Tests` | Testes xUnit do domínio (repositório em memória) | Domain |
| `tests/Chamados.Infra.Tests` | Testes de integração do repositório com SQLite em memória | Infra |
| `tests/Chamados.Web.Tests` | Aplicação inteira em memória (`WebApplicationFactory`, em Development e em Production), componentes e políticas | Web |

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
- **Erros da API e das páginas por caminhos separados** (melhorias): o mesmo critério `EhApi` decide o tratador de exceções e as páginas de status. A API nunca recebe HTML, nem num erro 500.
- **Página de erro sem dependências** (melhorias): o que aparece quando algo quebra não pode depender do que pode ter quebrado. A página de erro não consulta o banco; o contador do cabeçalho, informação de apoio, falha em silêncio e registra no log.
- **Limite por origem soma-se ao bloqueio por conta** (melhorias): o bloqueio do Identity impede adivinhar a senha de uma conta; o limite de 10 tentativas por minuto por IP impede testar senhas em volume e bloquear contas em série. Ainda é possível bloquear uma conta específica com 5 tentativas; fechar isso pediria um segundo fator ou CAPTCHA. Atrás de um proxy reverso, o limite precisa de `UseForwardedHeaders`, senão todos chegam com o IP do proxy.
- **Toda entrada de texto tem tamanho máximo no domínio** (melhorias): o banco declara os tamanhos, mas o SQLite não os confere. As mesmas constantes do `Chamado` alimentam o domínio, o mapeamento do EF, os atributos da API e o `maxlength` das telas.
- **CI como porta de entrada** (melhorias): o que não compila sem aviso ou não passa nos testes não entra, inclusive as atualizações do Dependabot. Um pacote com vulnerabilidade conhecida também quebra o CI de propósito.
- **Prazo calculado, não gravado** (prazos): vencimento = abertura + prazo da prioridade atual. Sem coluna nova nem migration, e mudar a prioridade de um chamado muda o prazo dele na hora. O custo: mudar a política em `Prazos` reavalia também o histórico de cumprimento. Se um dia o prazo precisar ficar "congelado" na abertura, aí vira coluna.
- **A regra de prazos é do domínio** (prazos): `PoliticaDePrazos` decide a situação com um "agora" recebido por parâmetro, sem relógio nem banco, e valida a configuração (prazos positivos, Crítica ≤ Alta ≤ Média ≤ Baixa). A configuração inválida impede a aplicação de iniciar com a mensagem dela.
- **"Só vencidos" filtrado no banco** (prazos): o serviço traduz o pedido em um corte de data por prioridade (agora − prazo), e o repositório compara datas no `WHERE`, com os cortes como parâmetros. As comparações são estritas nos dois lados, para a tela e a contagem nunca discordarem na fronteira. O cumprimento dos resolvidos é calculado em C#, porque o SQLite não soma datas.
- **Horas corridas** (prazos): o prazo conta noites e fins de semana. Horário comercial e feriados pediriam um calendário; ficou de fora por ora.
- **Autoria no domínio, sem Identity** (meus chamados): o domínio recebe um `Autor` (Id e nome) da camada Web e grava os dois em colunas simples, no chamado e em cada registro do histórico. O nome fica como era no momento da ação. Colunas simples, e não um tipo "owned" do EF, para não acrescentar concessões ao EF no domínio.
- **Uma regra de visibilidade, num arquivo** (meus chamados): `Seguranca/Visibilidade.cs` diz quem vê todos (papel ou claim) e devolve o dono a aplicar nas consultas; páginas, componentes e API só citam esses métodos. A restrição vira `WHERE AbertoPorId = @dono` no banco, com índice.
- **404, e não 403, para chamado de outra pessoa** (meus chamados): quem não pode ver um chamado não fica sabendo que ele existe. A tentativa é registrada no log para quem investiga.
- **Chamados antigos sem dono** (meus chamados): não há como saber, depois do fato, quem abriu os chamados anteriores; eles ficam visíveis só para atendentes e coordenação, em vez de ganharem um dono inventado.
- **O autor é opcional no serviço** (meus chamados): os testes do domínio e a semente continuam simples, e a camada Web sempre informa o usuário conectado. Como o compilador não acusa a falta de um parâmetro opcional, testes de integração conferem o autor pela API e pelas Razor Pages (formulário de abertura e botões do detalhe); os caminhos da fila Blazor foram conferidos no navegador.
- **Comentar não muda a versão** (comentários): a versão protege título, prioridade e status contra gravações cruzadas. Uma mensagem só acrescenta uma linha em outra tabela; se mudasse a versão, quem estivesse editando o chamado receberia um conflito por causa de uma conversa. Um teste de integração grava uma mensagem no meio de uma transição e confere que as duas ficam.
- **Mensagens imutáveis e com autor obrigatório** (comentários): não há editar nem apagar; o que foi dito fica como foi dito, com o nome do autor como era no momento. Ao contrário das transições, o autor não é opcional no domínio: mensagem sem autor não faz sentido.
- **Conversa em qualquer status** (comentários): depois de resolvido, "voltou a dar problema" também é conversa. Comentar não reabre o chamado; reabertura seria outra transição, com regra própria.
- **Duas coleções, um JOIN só** (comentários): com histórico e conversa no mesmo chamado, o detalhe usa `AsSingleQuery`. O JOIN repete cada registro do histórico a cada mensagem, mas o histórico tem no máximo dois registros (iniciar e resolver ou cancelar). Em troca, o SQL ordena pela chave das mensagens, e elas chegam na ordem em que foram gravadas; com `AsSplitQuery` (uma consulta por tabela), o EF ordena só pelo chamado, e essa ordem ficaria por conta do banco.
- **`totalComentarios` na lista, `comentarios` no detalhe** (comentários): nomes diferentes porque os tipos são diferentes (número e lista). Com o mesmo nome, um cliente que lesse o detalhe com o tipo da lista quebraria; um teste já existente pegou exatamente isso.
- **Sem atualização ao vivo** (comentários): a conversa aparece ao abrir ou recarregar o detalhe (Post/Redirect/Get). Mensagens chegando sozinhas pediriam o detalhe em Blazor ou SignalR; ficou de fora por ora.
