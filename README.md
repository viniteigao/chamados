# Chamados

Sistema de chamados (ordens de serviço) para uma empresa de manutenção predial. Estado: Módulo 3.

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

Em Development a base em memória recebe 5 chamados de exemplo.

### Páginas (Razor Pages)

| Rota | Página | O que faz |
| --- | --- | --- |
| `/` | `Pages/Index` | Painel com totais por status (`ObterEstatisticasAsync`) |
| `/Chamados?status=&prioridadeMinima=&texto=` | `Pages/Chamados/Index` | Lista com filtros (`ListarAsync`) |
| `/Chamados/Novo` | `Pages/Chamados/Novo` | Formulário de abertura (`AbrirAsync`), validação e Post/Redirect/Get |
| `/Chamados/Detalhes/{id}` | `Pages/Chamados/Detalhes` | Detalhe, histórico e handlers Iniciar, Resolver e Cancelar |
| `/Erro` | `Pages/Erro` | Página de erro genérica, usada fora de Development |

### API JSON (em `/api` desde o Módulo 3)

| Rota | Função |
| --- | --- |
| `GET /api/chamados?status=&prioridadeMinima=&texto=` | Lista com filtros |
| `GET /api/chamados/estatisticas` | Totais e tempo médio de resolução |
| `GET /api/chamados/{id}` | Um chamado |
| `POST /api/chamados` | Abre chamado (corpo: `{"titulo": "...", "prioridade": "Alta"}`) |
| `POST /api/chamados/{id}/atendimento` | Inicia atendimento (corpo: `{"responsavel": "..."}`) |
| `POST /api/chamados/{id}/resolucao` | Resolve (corpo: `{"solucao": "..."}`) |
| `POST /api/chamados/{id}/cancelamento` | Cancela (corpo: `{"motivo": "..."}`) |
| `GET /api/status`, `GET /api/ambiente` | Herdadas do Módulo 1 (`/api/ambiente` só em Development) |

Erros de domínio na API voltam como Problem Details (RFC 9457): 400, 404 ou 409. Exemplos em `src/Chamados.Web/Chamados.Web.http`.

### Configuração

`Simulacao:LatenciaMs` (0 a 10000) atrasa a listagem para observar cancelamento. Valor fora da faixa impede a aplicação de iniciar (`ValidateOnStart`). Para testar sem editar arquivo:

```powershell
$env:Simulacao__LatenciaMs = "20000"; dotnet run --project src/Chamados.Web
```

## Decisões

- **.NET 10 (LTS) como alvo**: versão com suporte até 14/11/2028, cobrindo todo o curso.
- **Formato `.slnx`**: padrão do `dotnet new sln` no SDK 10; XML enxuto, sem GUIDs. `Chamados.Web` é o projeto de inicialização (`DefaultStartup="true"`).
- **Interface `IChamadoRepositorio`**: existe porque há uma segunda implementação prevista. Hoje os dados ficam em memória; no Módulo 7 entra o Entity Framework Core. A troca muda uma linha no `Program.cs`, sem alterar o serviço nem os testes. O `ChamadoServico` não tem interface porque não há segunda implementação prevista para ele.
- **`TimeProvider` injetado**: o serviço e o repositório recebem o relógio pelo construtor em vez de ler `DateTimeOffset.UtcNow`. Em produção é usado `TimeProvider.System`; nos testes, o `RelogioFixo`, que torna datas e durações previsíveis.
- **Casos de uso no Domain, sem camada Application**: o `ChamadoServico` fica em `Chamados.Domain/Servicos` enquanto o projeto é pequeno. Separar em mais projetos só se justifica quando chegar o banco de dados (Módulo 7).
- **Razor Pages para os formulários** (Módulo 3): uma página por tela, com o `PageModel` chamando só o `ChamadoServico`, nunca o repositório.
- **Validação em duas camadas**: Data Annotations no modelo do formulário (mensagem amigável, no servidor) e invariantes no domínio (palavra final). Os limites de tamanho são constantes do `Chamado`, reusadas nos atributos.
- **Sem validação no cliente por enquanto**: nenhuma biblioteca JavaScript; o servidor valida sempre. Validação no cliente é conforto, não segurança (Módulo 4).
- **API sob `/api`** com `MapGroup` e um filtro único de exceções de domínio (`FiltroExcecoesDominio`), para não disputar a rota `/Chamados` com as páginas e não repetir `try/catch` em cada rota.
