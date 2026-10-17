# Chamados

Sistema de chamados (ordens de serviço) para uma empresa de manutenção predial.

## Requisitos

- .NET SDK 10.0.402 ou superior na faixa 10.0 (fixado em `global.json`)

## Como executar

```bash
dotnet build
dotnet run --project src/Chamados.Web
```

## Decisões

- **.NET 10 (LTS) como alvo**: versão com suporte até 14/11/2028, cobrindo todo o curso.
- **Formato `.slnx`**: padrão do `dotnet new sln` no SDK 10; XML enxuto, sem GUIDs.