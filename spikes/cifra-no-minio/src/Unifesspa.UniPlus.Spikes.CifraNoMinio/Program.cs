using Unifesspa.UniPlus.Spikes.CifraNoMinio;

if (args.Length > 0 && args[0] == "carga")
{
    return await Carga.ExecutarAsync(args[1..]);
}

if (args.Length > 0 && args[0] == "assinar-leitura")
{
    return await Assinatura.ExecutarAsync(args[1..]);
}

string papel = Configuracao.Obrigatoria("PAPEL");
Configuracao config = Configuracao.DoAmbiente();
// Com TRANSPORTE definido, a api-nova também confirma o envio e publica o pedido de verificação.
bool comFila = Environment.GetEnvironmentVariable("TRANSPORTE") is { Length: > 0 };

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<Verificador>();
if (papel == "verificador-fila")
{
    Fila.ConfigurarVerificador(builder, config);
}
else if (papel == "api-nova" && comFila)
{
    Fila.ConfigurarApi(builder);
}

WebApplication app = builder.Build();

Memoria.Mapear(app);
switch (papel)
{
    case "verificador":
        Verificador.Mapear(app);
        break;
    case "verificador-fila":
        break;
    case "api-nova":
        ApiNova.Mapear(app, config);
        if (comFila)
        {
            await ApiFila.PrepararAsync(app);
            ApiFila.Mapear(app);
        }

        break;
    case "api-atual":
        ApiAtual.Mapear(app, config);
        break;
    default:
        throw new InvalidOperationException($"PAPEL desconhecido: {papel}");
}

await app.RunAsync();
return 0;
