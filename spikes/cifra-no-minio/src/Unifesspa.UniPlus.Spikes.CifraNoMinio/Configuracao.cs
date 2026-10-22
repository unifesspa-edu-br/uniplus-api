namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

using Minio;

/// <summary>O que cada papel lê do ambiente, definido no docker-compose da POC.</summary>
public sealed record Configuracao(
    string Endpoint,
    string EndpointPublico,
    string AccessKey,
    string SecretKey,
    string BucketEnvios,
    string BucketEnviosAtual,
    string BucketArquivos,
    string BucketSelados,
    string ClamdHost,
    int ClamdPorta)
{
    public static Configuracao DoAmbiente() => new(
        Obrigatoria("MINIO_ENDPOINT"),
        Obrigatoria("MINIO_ENDPOINT_PUBLICO"),
        Obrigatoria("MINIO_ACCESS_KEY"),
        Obrigatoria("MINIO_SECRET_KEY"),
        Obrigatoria("BUCKET_ENVIOS"),
        Obrigatoria("BUCKET_ENVIOS_ATUAL"),
        Obrigatoria("BUCKET_ARQUIVOS"),
        Obrigatoria("BUCKET_SELADOS"),
        Obrigatoria("CLAMD_HOST"),
        int.Parse(Obrigatoria("CLAMD_PORT"), System.Globalization.CultureInfo.InvariantCulture));

    public static string Obrigatoria(string nome) =>
        Environment.GetEnvironmentVariable(nome) is { Length: > 0 } valor
            ? valor
            : throw new InvalidOperationException($"A variável {nome} é obrigatória.");

    /// <summary>Cliente da rede interna, para ler e gravar.</summary>
    public IMinioClient ClienteInterno() => Cliente(Endpoint);

    /// <summary>
    /// Cliente que só assina: a URL vai a quem está fora da rede do compose. A região explícita
    /// evita a consulta de localização do bucket, então assinar não faz nenhuma requisição.
    /// </summary>
    public IMinioClient ClienteDeAssinatura() => Cliente(EndpointPublico);

    private IMinioClient Cliente(string endpoint) => new MinioClient()
        .WithEndpoint(endpoint)
        .WithCredentials(AccessKey, SecretKey)
        .WithRegion("us-east-1")
        .Build();
}
