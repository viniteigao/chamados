using System.Runtime.InteropServices;
using Chamados.Domain;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Chamados.Web em execução");
app.MapGet("/status", () => Enum.GetNames<StatusChamado>());

// Diagnóstico exposto somente em Development:
// em produção, revelar versão de runtime ajuda um atacante.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/ambiente", (IHostEnvironment env) => new
    {
        env.EnvironmentName,
        Runtime = RuntimeInformation.FrameworkDescription
    });
}

app.Run();