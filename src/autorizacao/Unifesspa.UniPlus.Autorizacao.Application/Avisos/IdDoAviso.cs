namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A chave de um aviso: o tipo, o fato, o marco, para o que um fato emite mais de uma vez, e a identidade de quem recebe.
/// O destinatário é a identidade da pessoa, nunca o endereço: se o e-mail mudar entre duas gerações do mesmo fato, a
/// chave é a mesma.
/// </summary>
public sealed record ChaveDoAviso(TipoDeAviso Tipo, Guid FatoId, string Marco, Identidade Destinatario)
{
    /// <summary>Os textos que identificam o aviso, na ordem que compõe o identificador; método, para não entrar no <c>ToString()</c> do record.</summary>
    internal string[] Componentes() =>
        [Tipo.ToCodigo(), FatoId.ToString("N"), Marco, Destinatario.Emissor, Destinatario.ContaId];
}

/// <summary>
/// O <c>MensagemId</c> determinístico do aviso: um UUID versão 8 com os 16 primeiros bytes do SHA-256 da chave. O mesmo
/// aviso gerado duas vezes tem o mesmo identificador e, portanto, o mesmo <c>Message-ID</c> no SMTP. O identificador é
/// pseudonimizado: quem conhece o fato e uma identidade candidata recalcula o hash.
/// </summary>
public static class IdDoAviso
{
    private const int TamanhoDoGuid = 16;
    private const byte MascaraDaVersao = 0x0F;
    private const byte Versao8 = 0x80;
    private const byte MascaraDaVariante = 0x3F;
    private const byte VarianteRfc = 0x80;

    public static Guid De(ChaveDoAviso chave)
    {
        ArgumentNullException.ThrowIfNull(chave);

        Span<byte> bytes = SHA256.HashData(Serializar(chave)).AsSpan(0, TamanhoDoGuid);
        bytes[6] = (byte)((bytes[6] & MascaraDaVersao) | Versao8);
        bytes[8] = (byte)((bytes[8] & MascaraDaVariante) | VarianteRfc);

        return new Guid(bytes, bigEndian: true);
    }

    // Cada componente em UTF-8, precedido do comprimento em 4 bytes: emissor e conta são texto opaco e podem conter
    // qualquer separador, e a serialização não pode confundir dois conjuntos de componentes.
    private static byte[] Serializar(ChaveDoAviso chave)
    {
        using MemoryStream saida = new();

        foreach (byte[] texto in chave.Componentes().Select(Encoding.UTF8.GetBytes))
        {
            EscreverComPrefixoDoComprimento(saida, texto);
        }

        return saida.ToArray();
    }

    private static void EscreverComPrefixoDoComprimento(MemoryStream saida, byte[] texto)
    {
        Span<byte> comprimento = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(comprimento, texto.Length);
        saida.Write(comprimento);
        saida.Write(texto);
    }
}
