namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

using Minio;
using Minio.DataModel.Args;

/// <summary>O que o verificador registra de cada arquivo. Nunca o conteúdo, nem o nome da ameaça.</summary>
public sealed record RegistroDaVerificacao(
    string Chave,
    string Veredicto,
    string Destino,
    string Sha256,
    long Bytes,
    DateTimeOffset RecebidoEm,
    DateTimeOffset ConcluidoEm,
    double EsperaNaFilaMs,
    double VerificacaoMs);

/// <summary>
/// O verificador mínimo: a notificação do bucket de envio chega por webhook, entra numa fila em
/// memória e é atendida por <c>PARALELISMO</c> trabalhadores. Cada um lê o objeto direto do MinIO
/// (o MinIO decifra, pois o bucket tem SSE-KMS), passa o stream ao clamd e, conforme o veredicto,
/// promove o arquivo ao bucket definitivo (cópia no próprio servidor, recifrada com chave de dados
/// nova) ou o remove. A leitura e a cópia exigem o mesmo ETag do evento: o que foi verificado é
/// exatamente o que é promovido, mesmo que a URL de envio ainda aceite outro PUT.
/// </summary>
public sealed partial class Verificador(Configuracao config, ILogger<Verificador> logger)
{
    private readonly record struct Pendente(string Chave, string ETag, DateTimeOffset RecebidoEm);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Channel<Pendente> _fila = Channel.CreateUnbounded<Pendente>();
    private readonly ConcurrentDictionary<string, RegistroDaVerificacao> _registros = new(StringComparer.Ordinal);
    private readonly IMinioClient _minio = config.ClienteInterno();
    private readonly Clamd _clamd = new(config.ClamdHost, config.ClamdPorta);
    private readonly Lock _arquivo = new();

    public static void Mapear(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        Verificador verificador = app.Services.GetRequiredService<Verificador>();
        string token = Configuracao.Obrigatoria("TOKEN_WEBHOOK");
        int paralelismo = int.Parse(Configuracao.Obrigatoria("PARALELISMO"), System.Globalization.CultureInfo.InvariantCulture);

        for (int i = 0; i < paralelismo; i++)
        {
            _ = Task.Run(() => verificador.TrabalharAsync(app.Lifetime.ApplicationStopping));
        }

        app.MapPost("/eventos", async (HttpRequest request) =>
        {
            if (!string.Equals(request.Headers.Authorization, $"Bearer {token}", StringComparison.Ordinal))
            {
                return Results.Unauthorized();
            }

            using JsonDocument evento = await JsonDocument.ParseAsync(request.Body);
            verificador.Enfileirar(evento.RootElement);
            return Results.Ok();
        });
        // Coletor do log de auditoria do MinIO, só para a POC: em HML esse destino é o coletor de logs.
        app.MapPost("/auditoria", async (HttpRequest request) =>
        {
            if (!string.Equals(request.Headers.Authorization, $"Bearer {token}", StringComparison.Ordinal))
            {
                return Results.Unauthorized();
            }

            using JsonDocument evento = await JsonDocument.ParseAsync(request.Body);
            verificador.Registrar("/registro/auditoria.jsonl", JsonSerializer.Serialize(evento.RootElement));
            return Results.Ok();
        });
        app.MapGet("/resultados/{chave}", (string chave) =>
            verificador._registros.TryGetValue(chave, out RegistroDaVerificacao? registro) ? Results.Ok(registro) : Results.NotFound());
        app.MapGet("/resultados", () => Results.Ok(new { total = verificador._registros.Count, naFila = verificador._fila.Reader.Count }));
        app.MapDelete("/resultados", () => verificador._registros.Clear());
    }

    private void Enfileirar(JsonElement evento)
    {
        if (!evento.TryGetProperty("Records", out JsonElement registros))
        {
            return;
        }

        foreach (JsonElement registro in registros.EnumerateArray())
        {
            JsonElement objeto = registro.GetProperty("s3").GetProperty("object");
            string chave = Uri.UnescapeDataString(objeto.GetProperty("key").GetString()!);
            string etag = objeto.GetProperty("eTag").GetString()!;
            _fila.Writer.TryWrite(new Pendente(chave, etag, DateTimeOffset.UtcNow));
        }
    }

    private async Task TrabalharAsync(CancellationToken cancellationToken)
    {
        await foreach (Pendente pendente in _fila.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await VerificarAsync(pendente, cancellationToken);
            }
            catch (Minio.Exceptions.MinioException ex)
            {
                // Objeto já removido ou trocado por outro PUT (ETag diferente): o evento do novo PUT
                // traz a sua própria verificação.
                LogFalhaNoArmazenamento(logger, pendente.Chave, ex.GetType().Name);
            }
        }
    }

    private async Task VerificarAsync(Pendente pendente, CancellationToken cancellationToken)
    {
        DateTimeOffset inicio = DateTimeOffset.UtcNow;
        Stopwatch cronometro = Stopwatch.StartNew();
        (Veredicto Veredicto, string Sha256, long Bytes) resultado = default;

        await _minio.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(config.BucketEnvios)
                .WithObject(pendente.Chave)
                .WithMatchETag(pendente.ETag)
                .WithCallbackStream(async (stream, ct) => resultado = await _clamd.VerificarAsync(stream, ct)),
            cancellationToken);

        string destino;
        switch (resultado.Veredicto)
        {
            case Veredicto.Limpo:
                await _minio.CopyObjectAsync(
                    new CopyObjectArgs()
                        .WithBucket(config.BucketArquivos)
                        .WithObject(pendente.Chave)
                        .WithCopyObjectSource(new CopySourceObjectArgs()
                            .WithBucket(config.BucketEnvios)
                            .WithObject(pendente.Chave)
                            .WithMatchETag(pendente.ETag)),
                    cancellationToken);
                await RemoverAsync(pendente.Chave, cancellationToken);
                destino = config.BucketArquivos;
                break;
            case Veredicto.Infectado:
                await RemoverAsync(pendente.Chave, cancellationToken);
                destino = "removido";
                break;
            default:
                // Sem veredicto o arquivo fica onde está, para nova tentativa; nunca é promovido.
                destino = config.BucketEnvios;
                break;
        }

        RegistroDaVerificacao registro = new(
            pendente.Chave,
            resultado.Veredicto.ToString().ToUpperInvariant(),
            destino,
            resultado.Sha256 ?? string.Empty,
            resultado.Bytes,
            pendente.RecebidoEm,
            DateTimeOffset.UtcNow,
            (inicio - pendente.RecebidoEm).TotalMilliseconds,
            cronometro.Elapsed.TotalMilliseconds);
        _registros[pendente.Chave] = registro;
        Registrar("/registro/verificacoes.jsonl", JsonSerializer.Serialize(registro, Json));

        LogVerificado(logger, pendente.Chave, registro.Veredicto, registro.VerificacaoMs);
    }

    private void Registrar(string arquivo, string linha)
    {
        lock (_arquivo)
        {
            File.AppendAllText(arquivo, linha + Environment.NewLine);
        }
    }

    private Task RemoverAsync(string chave, CancellationToken cancellationToken) =>
        _minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(config.BucketEnvios).WithObject(chave), cancellationToken);

    [LoggerMessage(Level = LogLevel.Information, Message = "Arquivo {Chave} verificado: {Veredicto} em {Ms} ms")]
    private static partial void LogVerificado(ILogger logger, string chave, string veredicto, double ms);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Arquivo {Chave} não verificado: falha no armazenamento ({Tipo})")]
    private static partial void LogFalhaNoArmazenamento(ILogger logger, string chave, string tipo);
}
