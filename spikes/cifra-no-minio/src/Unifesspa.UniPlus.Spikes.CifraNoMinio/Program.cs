using Unifesspa.UniPlus.Spikes.CifraNoMinio;

if (args.Length > 0 && args[0] == "carga")
{
    return await Carga.ExecutarAsync(args[1..]);
}

string papel = Configuracao.Obrigatoria("PAPEL");
Configuracao config = Configuracao.DoAmbiente();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<Verificador>();
WebApplication app = builder.Build();

Memoria.Mapear(app);
switch (papel)
{
    case "verificador":
        Verificador.Mapear(app);
        break;
    case "api-nova":
        ApiNova.Mapear(app, config);
        break;
    case "api-atual":
        ApiAtual.Mapear(app, config);
        break;
    default:
        throw new InvalidOperationException($"PAPEL desconhecido: {papel}");
}

await app.RunAsync();
return 0;
