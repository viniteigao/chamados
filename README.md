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

| Login (e-mail) | Nome exibido | Papel ou claim | Pode |
| --- | --- | --- | --- |
| `ana@chamados.local` | Ana | papel `Atendente` | ver todos os chamados, a fila de atendimento, editar e mudar status |
| `davi@chamados.local` | Davi | papel `Atendente` | o mesmo que a ana (dois atendentes, para atribuir e reatribuir chamados) |
| `bruno@chamados.local` | Bruno | nenhum | abrir chamados e acompanhar só os que abriu ("Meus chamados") |
| `carla@chamados.local` | Carla | claim `cargo = Coordenacao` | ver todos os chamados e os indicadores |

O login é pelo e-mail; nas telas e na API aparece o nome (veja "Nome de exibição"). Os usuários de exemplo são criados uma vez; depois, cada um troca a própria senha em **Meu perfil** (clique no nome, no cabeçalho), e a coordenação cadastra outras pessoas em **Usuários** (veja "Gestão de usuários").

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

O projeto traz sete migrations: `Inicial` (tabelas `Chamados` e `HistoricoChamados`), `IndicePrioridade` (um índice criado depois, para mostrar o banco evoluindo sem perder dados), `Identidade` (tabelas de usuários do Módulo 8), `AutoriaDosChamados` (quem abriu e quem fez cada mudança), `ComentariosDosChamados` (tabela `ComentariosChamados`), `EdicoesDosChamados` (tabela `EdicoesChamados`) e `ResponsavelAtendente` (o Id do atendente responsável). Um banco antigo recebe as que faltam no próximo início, sem perder dados.

Em Development, o log mostra cada comando SQL (categoria `Microsoft.EntityFrameworkCore.Database.Command`), com os valores dos parâmetros ocultos.

### Páginas (Razor Pages)

| Rota | Página | O que faz |
| --- | --- | --- |
| `/` | `Pages/Index` | Painel com totais por status (`ObterEstatisticasAsync`) |
| `/Chamados?status=&prioridadeMinima=&texto=&ordem=&pagina=` | `Pages/Chamados/Index` | Lista com filtros, ordenação e paginação (`ListarPaginaAsync`) |
| `/Chamados/Novo` | `Pages/Chamados/Novo` | Formulário de abertura (`AbrirAsync`), validação no cliente e no servidor, Post/Redirect/Get |
| `/Chamados/Detalhes/{id}` | `Pages/Chamados/Detalhes` | Detalhe, histórico, conversa e handlers Iniciar, Resolver, Cancelar e Comentar |
| `/Chamados/Editar/{id}` | `Pages/Chamados/Editar` | Edição com concorrência otimista (`EditarAsync`) |
| `/Usuarios`, `/Usuarios/Novo`, `/Usuarios/Editar/{id}` | `Pages/Usuarios/*` | Gestão de usuários, só para a coordenação (política `GerenciarUsuarios`) |
| `/Perfil` | `Pages/Perfil` | Meu perfil: trocar o próprio nome e a própria senha |
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
| `GET /api/chamados?status=&prioridadeMinima=&texto=&ordem=&somenteVencidos=&meus=&atribuidosAMim=` | Lista com filtros e ordenação (`Prioridade`, `MaisRecentes`, `MaisAntigos`, `Titulo`); `somenteVencidos=true` traz só os em andamento com o prazo vencido; `meus=true` só os que o usuário abriu (quem não é atendente nem da coordenação sempre recebe só os seus); `atribuidosAMim=true` só os que estão com quem pede |
| `GET /api/chamados/estatisticas` | Totais e tempo médio de resolução |
| `GET /api/chamados/prazos` | Vencidos, perto de vencer, percentual resolvido no prazo e a tabela de prazos (política `VerIndicadores`) |
| `GET /api/chamados/{id}` | Um chamado com o histórico (com quem fez cada mudança), as edições, o prazo, quem abriu e a conversa (DTO `DetalheChamadoResposta`, inclui `versao`); id inexistente ou de outra pessoa → 404 |
| `GET /api/chamados/{id}/comentarios` | As mensagens da conversa, da mais antiga para a mais nova, cada uma com `texto`, `em` (data e hora) e `autor` |
| `POST /api/chamados/{id}/comentarios` | Escreve uma mensagem (corpo: `{"texto": "..."}`) → 201; vazia ou com mais de 2000 caracteres → 400 |
| `POST /api/chamados` | Abre chamado (corpo: `{"titulo": "...", "prioridade": "Alta"}`); corpo inválido → 400 com erros por campo |
| `PUT /api/chamados/{id}` | Edita (corpo com `versaoEsperada`; versão desatualizada → 409, chamado encerrado → 409) |
| `POST /api/chamados/{id}/atendimento` | Inicia atendimento. Corpo `{}`: quem atende é quem chama; `{"responsavelId": "..."}`: o atendente indicado (Id de `GET /api/atendentes`); quem não é atendente ativo → 400 |
| `POST /api/chamados/{id}/responsavel` | Passa um chamado em atendimento a outro atendente (corpo: `{"responsavelId": "..."}`); chamado aberto ou encerrado → 409 |
| `GET /api/atendentes` | Atendentes ativos, com Id e nome, para escolher o responsável (política `Atender`) |
| `GET /api/chamados/carga` | Chamados em atendimento e vencidos de cada atendente (política `VerIndicadores`) |
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

`PrimeiroCoordenador:Email`, `PrimeiroCoordenador:Nome` e `PrimeiroCoordenador:Senha` criam, ao iniciar, a conta de coordenação que cadastra os demais usuários, se ela ainda não existir (veja "Gestão de usuários"). Em Development não são necessárias: a semente já cria a carla.

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

### Histórico das edições

Feito na branch `historico-edicoes`. Antes, editar título, prioridade ou descrição não deixava rastro, e mudar a prioridade muda o prazo. Agora cada edição fica registrada (tabela `EdicoesChamados`, migration `EdicoesDosChamados`).

- **O que é guardado**: quando, quem editou e o que mudou. Do título e da prioridade, o valor de antes e o de depois; da descrição, só se mudou (o texto atual continua no chamado). Uma edição que não muda nada não entra no histórico.
- **Detalhe**: o "Histórico" virou uma linha do tempo com as mudanças de status e as edições, por exemplo "Dados editados · prioridade Baixa → Alta · título “A” → “B” · por Ana".
- **Quem editou**: o usuário conectado, nos três caminhos de edição (página Editar, fila de atendimento e `PUT /api/chamados/{id}`). Na API, o autor vem do token ou do cookie; um campo `por` no corpo é ignorado.
- **API**: o detalhe traz `edicoes`, cada uma com `em`, `tituloAnterior`, `tituloNovo`, `prioridadeAnterior`, `prioridadeNova`, `descricaoAlterada` e `por`.
- **Banco antigo**: as edições feitas antes deste recurso não têm como ser recuperadas; o histórico delas começa agora.

### Responsável atendente

Feito na branch `responsavel-atendente`. Antes, o responsável era um texto livre ("Equipe de climatização", "ana", "Ana"): o sistema não sabia quem era quem. Agora quem atende é sempre uma pessoa, escolhida na lista de atendentes ativos, e o chamado guarda o Id e o nome dela (migration `ResponsavelAtendente`).

- **Iniciar atendimento**: na página do chamado e na fila, uma lista com os atendentes ativos, com você já marcado (na fila, o último atendente escolhido neste navegador). Pela API, sem `responsavelId`, quem atende é quem chama.
- **Reatribuir**: um chamado em atendimento pode passar para outra pessoa, pela página ("Passar para outra pessoa"), pela fila ("Reatribuir") ou pela API. A troca entra no histórico: "Responsável alterado · Ana → Davi · por Ana".
- **Meus atendimentos**: na fila, "Só os meus atendimentos" mostra os chamados em atendimento com você, do mais perto de vencer ao mais folgado (a coluna "Abertos" continua inteira, para pegar novos). Na lista, "Atribuídos a mim", para quem atende. Na API, `?atribuidosAMim=true`.
- **Carga por atendente**: nos Indicadores, quantos chamados cada atendente tem em atendimento e quantos já venceram, com os mais pendentes primeiro (contagem agrupada no banco).
- **Só atendentes ativos**: a escolha é conferida no servidor, na página, na fila e na API. Um solicitante, uma conta desativada ou um Id inventado são recusados (400 na API, mensagem na tela). Na fila, a lista é conferida de novo no clique, porque alguém pode ter sido desativado desde que ela abriu.
- **Chamados antigos**: os atribuídos antes deste recurso continuam mostrando o texto que tinham. Eles não entram em "Meus atendimentos" e aparecem na carga como "Responsável não cadastrado (texto antigo)". Ao reatribuir um deles, ele passa a ter um atendente de verdade.
- **Semente**: o davi (`davi@chamados.local`, mesma senha) atende junto com a ana; o ar-condicionado da sala 204 e o Wi-Fi do bloco B estão com ele.

### Nome de exibição

Feito na branch `nome-de-exibicao`. O login continua pelo e-mail, mas o cabeçalho, "Aberto por", o histórico, a conversa, a fila e a API mostram um nome ("Ana", "Bruno", "Carla"). O e-mail de login aparece só no próprio cabeçalho, ao passar o mouse sobre o nome.

- **Onde fica**: numa claim `nome` de cada usuário, na tabela de claims que o Identity já tem (a mesma da claim `cargo`). Não houve coluna nova nem migration. No login, o Identity põe as claims no cookie e no token, e `Seguranca/NomeDeExibicao.cs` lê o nome de lá.
- **O que é gravado**: as ações passam a registrar o nome (`AbertoPorNome`, `PorNome` do histórico e `AutorNome` da conversa), em vez do e-mail.
- **Banco que já existia**: ao iniciar, a semente acrescenta o nome aos usuários que ainda não o têm (sem trocar um nome já gravado). Os chamados, registros e mensagens gravados antes continuam com o e-mail, porque o nome é guardado como era no momento da ação; num banco novo, tudo já sai com os nomes.
- **Sessão aberta antes**: um cookie emitido antes deste recurso não tem a claim; até sair e entrar de novo, vale o e-mail.

### Gestão de usuários

Feito na branch `gestao-usuarios`. Antes, os únicos usuários eram os três de exemplo, criados só em Development: fora dele, não havia como entrar ninguém. Agora a coordenação cuida das contas pelo sistema, e cada pessoa cuida do próprio nome e da própria senha. O login continua pelo e-mail. Não houve migration: tudo usa as tabelas do Identity.

| Página | Quem | O que faz |
| --- | --- | --- |
| **Usuários** (`/Usuarios`) | coordenação | lista com nome, e-mail, o que cada um pode fazer e a situação |
| **Novo usuário** | coordenação | nome, e-mail (o login), senha inicial e o que a pessoa pode fazer: "Atende chamados" (papel `Atendente`) e "Coordenação" (claim `cargo = Coordenacao`) |
| **Editar** | coordenação | muda nome e perfil, redefine a senha de quem esqueceu, desativa e reativa a conta |
| **Meu perfil** (`/Perfil`, pelo nome no cabeçalho) | todos | troca o próprio nome (vale na hora) e a própria senha (pede a atual) |

- **Desativar, em vez de excluir**: a conta fica bloqueada sem prazo. A pessoa não entra mais, e os chamados, históricos e mensagens dela continuam com o nome dela. Reativar devolve o acesso com o mesmo perfil e a mesma senha. No login, "Esta conta foi desativada" só aparece para quem digitou a senha certa; para os demais, a mensagem de sempre ("E-mail ou senha incorretos"), que não confirma quem tem conta.
- **Mudança de acesso vale na hora**: mudar o perfil, desativar ou trocar a senha troca o carimbo de segurança da conta. Um token da API emitido antes deixa de valer na próxima requisição (401); o cookie e a fila de atendimento aberta perdem a sessão em até 1 minuto (a fila some e pede um novo login, sem recarregar a página). Na fila, cada clique também confere a permissão. Mudar só o nome não derruba ninguém: o nome novo aparece em até 1 minuto.
- **Sempre sobra quem gerencie**: ninguém tira a própria coordenação nem desativa a própria conta.
- **Senhas**: a coordenação define a senha inicial e a passa à pessoa por um canal seguro; o sistema não envia e-mail. As regras do Identity (10 caracteres ou mais, com maiúscula, minúscula, número e símbolo) e as mensagens de erro aparecem em português (`Seguranca/ErrosDoIdentityEmPortugues.cs`), todas juntas ao lado do campo.
- **Primeiro coordenador em produção**: com `PrimeiroCoordenador__Email`, `PrimeiroCoordenador__Nome` e `PrimeiroCoordenador__Senha` (variáveis de ambiente), a aplicação cria essa conta ao iniciar, se ela não existir. Depois do primeiro início, tire a senha da configuração: uma conta que já existe nunca é alterada por aqui. As migrations precisam ter sido aplicadas antes.
- **Log**: eventos 1010 a 1017 (cadastro, perfil, desativação, reativação, senha redefinida, senha trocada, sessão recusada e login em conta desativada), sempre com os Ids de quem fez e de quem recebeu a ação.

### Bibliotecas do lado do cliente

`wwwroot/lib` traz jQuery 3.7.1, jQuery Validation 1.21.0 e jQuery Validation Unobtrusive 4.0.0 (licenças MIT e Apache 2.0, arquivos de licença incluídos), usados só pela validação no cliente. O `libman.json` registra a origem; no Visual Studio, botão direito em `libman.json` › **Restaurar Bibliotecas do Lado do Cliente** baixa de novo.

## Testar

No Visual Studio: **Teste > Gerenciador de Testes > Executar Todos** (`Ctrl+R, A`). Pela CLI: `dotnet test`. São 195 testes: 102 do domínio (com a política de prazos e um relógio controlado), 18 de integração com o banco e 75 da aplicação web (integração com `WebApplicationFactory`, inclusive em ambiente Production com o banco falhando, com prazos de milissegundos, com os três usuários vendo cada um o que pode e conversando nos chamados, com a coordenação cadastrando, desativando e mudando o acesso de outras pessoas, componentes com `HtmlRenderer` e unidade das políticas de autorização). O GitHub roda os mesmos testes a cada push (`.github/workflows/ci.yml`).

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
- **Uma consulta por coleção, ordem garantida pelo domínio** (comentários e edições): o chamado tem três coleções (histórico, conversa e edições). Num JOIN só, cada linha se repetiria a cada mensagem e a cada edição; por isso o detalhe usa `AsSplitQuery`, uma consulta por tabela. Nesse modo o EF ordena só pelo chamado, e a ordem das linhas de cada tabela ficaria por conta do banco; então o próprio `Chamado` devolve as três listas em ordem cronológica (pela data de cada registro), sem depender do SQL. Um teste do domínio cria registros fora de ordem e confere.
- **`totalComentarios` na lista, `comentarios` no detalhe** (comentários): nomes diferentes porque os tipos são diferentes (número e lista). Com o mesmo nome, um cliente que lesse o detalhe com o tipo da lista quebraria; um teste já existente pegou exatamente isso.
- **Sem atualização ao vivo** (comentários): a conversa aparece ao abrir ou recarregar o detalhe (Post/Redirect/Get). Mensagens chegando sozinhas pediriam o detalhe em Blazor ou SignalR; ficou de fora por ora.
- **Nome de exibição numa claim, não numa coluna** (nome de exibição): o sistema já usava claims do Identity para o cargo; o nome entra do mesmo jeito, sem trocar o `IdentityUser` por uma classe própria nem criar migration. O nome de usuário do Identity continua sendo o e-mail, que serve só para o login, e o nome pode ter espaços e acentos (o nome de usuário do Identity não aceita espaço). Se um dia houver uma tela para cada um editar o próprio perfil, a troca é `ReplaceClaimAsync`.
- **Nomes na tela e na API, e-mail só no login** (nome de exibição): o e-mail de uma pessoa não aparece mais para as outras, nem no JSON. Os logs continuam só com Ids.
- **Edições num registro próprio** (histórico das edições): `RegistroEdicao` tem campos para o que muda numa edição (título e prioridade de antes e de depois, descrição alterada), em vez de forçar a edição dentro do registro de status. A tela junta os dois numa linha do tempo só.
- **O autor da edição fica fora do comando** (histórico das edições): o `EdicaoChamado` chega pronto do corpo da requisição na API. Se o autor estivesse nele, quem chama a API poderia escrever o nome de outra pessoa; por isso ele é um parâmetro do serviço, preenchido pelo usuário conectado. Um teste manda `por` no corpo e confere que é ignorado.
- **Descrição: só "alterada"** (histórico das edições): guardar o texto anterior inteiro (até 2000 caracteres por edição) não pagava o espaço para o uso deste sistema. Título e prioridade, curtos e decisivos para o prazo, guardam os dois valores.
- **Perfil continua sendo papel e claim** (gestão de usuários): "Atende chamados" é o papel `Atendente` e "Coordenação" é a claim `cargo = Coordenacao`, os mesmos do Módulo 8. As políticas não mudaram; a tela só liga e desliga os dois. A gestão ganhou uma política própria (`GerenciarUsuarios`), hoje com a mesma regra da coordenação, para poder mudar sozinha.
- **Carimbo de segurança em vez de lista de tokens revogados** (gestão de usuários): o Identity já guarda um carimbo por conta e o põe no cookie e no token. Trocar o carimbo invalida tudo que foi emitido antes, sem tabela nova. O custo é uma consulta ao banco por requisição da API (`FiltroSessaoValida`) e uma a cada minuto para cada cookie e cada fila aberta.
- **A regra vale no clique, não só ao abrir a página** (gestão de usuários): o circuito do Blazor guarda quem entrou enquanto a aba estiver aberta. Por isso a fila confere a política a cada ação e esconde o conteúdo quando a revalidação encerra a sessão. Sem isso, uma atendente desativada continuaria resolvendo chamados até recarregar a página.
- **Sem cadastro aberto nem "esqueci a senha" por e-mail** (gestão de usuários): quem cria contas é a coordenação, e o sistema não tem servidor de e-mail. Quem esquece a senha pede à coordenação, que redefine. Se um dia houver e-mail, o Identity já tem o token de redefinição (`AddDefaultTokenProviders`).
- **Responsável é sempre uma pessoa** (responsável atendente): é o que permite "Meus atendimentos" e a carga por atendente. Equipes ("Redes", "Hidráulica") não são mais responsáveis; se o sistema precisar delas, elas cabem melhor como categoria do chamado.
- **Id do usuário só onde a API precisa dele** (responsável atendente): a regra continua sendo mostrar nomes. A exceção é `GET /api/atendentes`, só para quem atende, porque é pelo Id que a API recebe a escolha do responsável (nomes podem se repetir e mudar).
- **Reatribuir é uma edição, não uma transição** (responsável atendente): o status não muda, então a troca fica no histórico das edições, com o nome de antes e o de depois, e aumenta a versão do chamado, como uma edição.
