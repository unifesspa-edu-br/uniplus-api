namespace Unifesspa.UniPlus.Infrastructure.Core.Storage;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Assina uma requisição S3 pelo esquema AWS Signature Version 4, com autenticação no cabeçalho
/// <c>Authorization</c> e todos os cabeçalhos enviados entrando na assinatura.
/// </summary>
/// <remarks>
/// Existe para as operações que a biblioteca do MinIO não consegue montar: ela só sabe gravar, de
/// cabeçalho de conteúdo, o tipo, o tamanho e o MD5, e recusa os demais — entre eles o
/// <c>Content-Disposition</c>, que o objeto público precisa carregar. Requisição sem corpo: o hash
/// do corpo é o da sequência vazia.
/// </remarks>
internal static class AssinaturaS3V4
{
    private const string Algoritmo = "AWS4-HMAC-SHA256";
    private const string Servico = "s3";
    private const string RegiaoPadrao = "us-east-1";

    /// <summary>Hash SHA-256 do corpo vazio, em hexadecimal minúsculo.</summary>
    internal const string HashDoCorpoVazio = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    /// <summary>
    /// Devolve o valor do cabeçalho <c>Authorization</c> e o instante que foi assinado — o mesmo que
    /// tem de seguir em <c>x-amz-date</c>.
    /// </summary>
    /// <param name="cabecalhos">
    /// Todos os cabeçalhos que a requisição levará, exceto <c>host</c>, <c>x-amz-date</c> e
    /// <c>x-amz-content-sha256</c>, que esta assinatura acrescenta ao conjunto assinado.
    /// </param>
    internal static (string Authorization, string AmzDate) Assinar(
        HttpMethod metodo,
        Uri endereco,
        IReadOnlyDictionary<string, string> cabecalhos,
        StorageOptions opcoes,
        DateTimeOffset instante)
    {
        string amzDate = instante.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        string data = amzDate[..8];
        string regiao = string.IsNullOrWhiteSpace(opcoes.Region) ? RegiaoPadrao : opcoes.Region;
        string escopo = $"{data}/{regiao}/{Servico}/aws4_request";

        SortedDictionary<string, string> assinados = new(StringComparer.Ordinal)
        {
            ["host"] = endereco.Authority,
            ["x-amz-content-sha256"] = HashDoCorpoVazio,
            ["x-amz-date"] = amzDate,
        };
        foreach ((string nome, string valor) in cabecalhos)
        {
            assinados[nome.ToLowerInvariant()] = ValorCanonico(valor);
        }

        string nomesAssinados = string.Join(';', assinados.Keys);
        string requisicaoCanonica = string.Join(
            '\n',
            metodo.Method,
            endereco.AbsolutePath,
            endereco.Query.TrimStart('?'),
            string.Concat(assinados.Select(static par => $"{par.Key}:{par.Value}\n")),
            nomesAssinados,
            HashDoCorpoVazio);

        string textoAAssinar = string.Join('\n', Algoritmo, amzDate, escopo, Hex(SHA256.HashData(Encoding.UTF8.GetBytes(requisicaoCanonica))));

        byte[] chave = Hmac(Encoding.UTF8.GetBytes("AWS4" + opcoes.SecretKey), data);
        chave = Hmac(chave, regiao);
        chave = Hmac(chave, Servico);
        chave = Hmac(chave, "aws4_request");
        string assinatura = Hex(Hmac(chave, textoAAssinar));

        return ($"{Algoritmo} Credential={opcoes.AccessKey}/{escopo}, SignedHeaders={nomesAssinados}, Signature={assinatura}", amzDate);
    }

    /// <summary>
    /// O caminho de um objeto no estilo de caminho do S3 — <c>/bucket/chave</c> —, com cada segmento
    /// codificado como a assinatura o espera.
    /// </summary>
    internal static string CaminhoDoObjeto(string bucket, string chave) =>
        "/" + Uri.EscapeDataString(bucket) + "/" + string.Join('/', chave.Split('/').Select(Uri.EscapeDataString));

    /// <summary>
    /// O valor do cabeçalho na forma canônica do SigV4: sem espaço nas pontas e com os espaços
    /// seguidos reduzidos a um. O servidor canoniza assim; aparar só as pontas faria divergir a
    /// assinatura de todo valor com espaço duplo — um nome de arquivo, por exemplo —, e a
    /// requisição seria recusada sempre. O valor enviado segue como está.
    /// </summary>
    private static string ValorCanonico(string valor) =>
        string.Join(' ', valor.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static byte[] Hmac(byte[] chave, string texto) => HMACSHA256.HashData(chave, Encoding.UTF8.GetBytes(texto));

    private static string Hex(byte[] bytes) => Convert.ToHexStringLower(bytes);
}
