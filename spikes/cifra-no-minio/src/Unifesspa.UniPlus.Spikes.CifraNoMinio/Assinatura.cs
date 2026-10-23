namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

using Minio;
using Minio.DataModel.Args;

/// <summary>
/// Assina, no host, uma URL de leitura com a credencial pedida, como a API assinaria: assinar é
/// cálculo local, sem consultar o objeto. Usado nas provas da alternativa por etiqueta.
/// Uso: assinar-leitura &lt;usuário&gt; &lt;senha&gt; &lt;bucket&gt; &lt;chave&gt;
/// </summary>
public static class Assinatura
{
    public static async Task<int> ExecutarAsync(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        using IMinioClient cliente = new MinioClient()
            .WithEndpoint("localhost:19000")
            .WithCredentials(args[0], args[1])
            .WithRegion("us-east-1")
            .Build();
        Console.WriteLine(await cliente.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(args[2])
            .WithObject(args[3])
            .WithExpiry(300)));
        return 0;
    }
}
