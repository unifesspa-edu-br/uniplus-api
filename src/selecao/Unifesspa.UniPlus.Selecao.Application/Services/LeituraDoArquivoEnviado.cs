namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Lê, na confirmação, o arquivo que o cliente enviou direto ao storage — o documento do Edital e
/// o modelo de documento de uma exigência —, sem nunca trazer mais que o teto do arquivo. O
/// resultado é neutro: quem confirma traduz o objeto ausente e o tamanho excedido para os códigos
/// de erro do próprio arquivo.
/// </summary>
public static class LeituraDoArquivoEnviado
{
    /// <summary>
    /// Lê o objeto em <paramref name="objectKey"/> até <paramref name="tamanhoMaximoBytes"/>.
    /// </summary>
    /// <remarks>
    /// O stat (HEAD) barra cedo o envio já obviamente grande, sem abrir o stream. Não é a proteção
    /// definitiva: a chave de envio segue sobrescrevível até o TTL da URL expirar, então o tamanho
    /// pode mudar entre o stat e a leitura. Quem garante o limite é a leitura limitada a
    /// <c>teto + 1</c> pelo storage (Range request), reconferida depois — nunca bufferizar o
    /// objeto inteiro para só então recusar. O objeto de 0 bytes não é lido: um Range sobre objeto
    /// vazio é recusado pelo servidor (416), e não há o que ler.
    /// </remarks>
    public static async Task<ArquivoEnviadoLido> LerAsync(
        IArquivoArmazenadoStorage storage, string objectKey, long tamanhoMaximoBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(storage);

        InfoObjetoArmazenado? info = await storage.ObterInfoAsync(objectKey, cancellationToken).ConfigureAwait(false);
        if (info is null)
        {
            return ArquivoEnviadoLido.Ausente;
        }

        if (info.TamanhoBytes > tamanhoMaximoBytes)
        {
            return ArquivoEnviadoLido.Excedido;
        }

        if (info.TamanhoBytes == 0)
        {
            return ArquivoEnviadoLido.Lido([], info.ContentType);
        }

        byte[] conteudo;
        Stream stream = await storage.AbrirLeituraAsync(objectKey, tamanhoMaximoBytes + 1, cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using MemoryStream buffer = new();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            conteudo = buffer.ToArray();
        }

        return conteudo.LongLength > tamanhoMaximoBytes ? ArquivoEnviadoLido.Excedido : ArquivoEnviadoLido.Lido(conteudo, info.ContentType);
    }
}

/// <summary>
/// O desfecho da leitura: o objeto não foi enviado (ou expirou), excede o teto, ou foi lido — com
/// o conteúdo e o content-type que o objeto declara.
/// </summary>
public sealed record ArquivoEnviadoLido(SituacaoDoArquivoEnviado Situacao, byte[] Conteudo, string ContentType)
{
    public static ArquivoEnviadoLido Ausente { get; } = new(SituacaoDoArquivoEnviado.Ausente, [], string.Empty);

    public static ArquivoEnviadoLido Excedido { get; } = new(SituacaoDoArquivoEnviado.Excedido, [], string.Empty);

    public static ArquivoEnviadoLido Lido(byte[] conteudo, string contentType) => new(SituacaoDoArquivoEnviado.Lido, conteudo, contentType);
}

/// <summary>A situação do arquivo enviado na leitura da confirmação.</summary>
public enum SituacaoDoArquivoEnviado
{
    Ausente,
    Excedido,
    Lido,
}
