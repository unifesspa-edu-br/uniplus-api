namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

using Minio;
using Minio.DataModel.Args;

/// <summary>
/// A API no fluxo novo: assina a URL de envio e a de leitura do arquivo já verificado, e nada mais.
/// Não há neste papel nenhuma chamada que leia conteúdo, e a credencial dele não tem leitura no
/// bucket de envio. Assinar é cálculo local: nenhum byte do arquivo passa por aqui.
/// </summary>
public static class ApiNova
{
    public static void Mapear(WebApplication app, Configuracao config)
    {
        ArgumentNullException.ThrowIfNull(config);

        IMinioClient assinatura = config.ClienteDeAssinatura();

        // O fluxo atual também começa por uma URL de envio, num bucket sem SSE-KMS e sem notificação.
        app.MapPost("/envios", async (string? fluxo, string? tipo) =>
        {
            string chave = Guid.NewGuid().ToString("N");
            string bucket = fluxo == "atual" ? config.BucketEnviosAtual : config.BucketEnvios;
            string url = await assinatura.PresignedPutObjectAsync(new PresignedPutObjectArgs()
                .WithBucket(bucket)
                .WithObject(chave)
                .WithExpiry(900)
                .WithHeaders(new Dictionary<string, string>(StringComparer.Ordinal) { ["Content-Type"] = tipo ?? "application/pdf" }));
            return Results.Ok(new { chave, url });
        });

        // A leitura pelo candidato: a URL pede ao MinIO o Content-Disposition da resposta, para abrir
        // no navegador (inline) ou baixar com um nome (attachment).
        app.MapGet("/arquivos/{chave}/url", async (string chave, string? disposicao, string? nome) =>
        {
            Dictionary<string, string> resposta = new(StringComparer.Ordinal);
            if (disposicao is not null)
            {
                resposta["response-content-disposition"] = nome is null ? disposicao : $"{disposicao}; filename=\"{nome}\"";
            }

            string url = await assinatura.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                .WithBucket(config.BucketArquivos)
                .WithObject(chave)
                .WithExpiry(300)
                .WithHeaders(resposta));
            return Results.Ok(new { url });
        });
    }
}
