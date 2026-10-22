namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

public enum Veredicto
{
    Limpo,
    Infectado,
    Indisponivel,
}

/// <summary>
/// Cliente do clamd pelo <c>INSTREAM</c>, com o mesmo protocolo e a mesma interpretação da resposta
/// do <c>ClamdClient</c> da API. Diferença: lê de um <see cref="Stream"/> em pedaços de 64 KiB, sem
/// nunca ter o arquivo inteiro em memória, e calcula o SHA-256 no caminho.
/// </summary>
public sealed class Clamd(string host, int porta)
{
    private const int TamanhoDoPedaco = 64 * 1024;
    private static readonly byte[] Comando = "zINSTREAM\0"u8.ToArray();

    public async Task<(Veredicto Veredicto, string Sha256, long Bytes)> VerificarAsync(Stream conteudo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        byte[] pedaco = ArrayPool<byte>.Shared.Rent(TamanhoDoPedaco);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long total = 0;
        try
        {
            using TcpClient cliente = new();
            await cliente.ConnectAsync(host, porta, cancellationToken);
            NetworkStream rede = cliente.GetStream();
            await rede.WriteAsync(Comando, cancellationToken);
            byte[] tamanho = new byte[sizeof(int)];
            int lidos;
            while ((lidos = await conteudo.ReadAtLeastAsync(pedaco.AsMemory(0, TamanhoDoPedaco), TamanhoDoPedaco, false, cancellationToken)) > 0)
            {
                BinaryPrimitives.WriteInt32BigEndian(tamanho, lidos);
                await rede.WriteAsync(tamanho, cancellationToken);
                await rede.WriteAsync(pedaco.AsMemory(0, lidos), cancellationToken);
                hash.AppendData(pedaco, 0, lidos);
                total += lidos;
            }

            BinaryPrimitives.WriteInt32BigEndian(tamanho, 0);
            await rede.WriteAsync(tamanho, cancellationToken);
            await rede.FlushAsync(cancellationToken);
            string resposta = await LerRespostaAsync(rede, cancellationToken);
            return (Interpretar(resposta), Convert.ToHexStringLower(hash.GetHashAndReset()), total);
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            return (Veredicto.Indisponivel, string.Empty, total);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pedaco);
        }
    }

    /// <summary>O caminho da API de hoje: o conteúdo inteiro já está num <c>byte[]</c>.</summary>
    public async Task<Veredicto> VerificarAsync(byte[] conteudo, CancellationToken cancellationToken)
    {
        using MemoryStream stream = new(conteudo, writable: false);
        return (await VerificarAsync(stream, cancellationToken)).Veredicto;
    }

    private static async Task<string> LerRespostaAsync(NetworkStream rede, CancellationToken cancellationToken)
    {
        using MemoryStream resposta = new();
        await rede.CopyToAsync(resposta, cancellationToken);
        return Encoding.UTF8.GetString(resposta.GetBuffer(), 0, (int)resposta.Length).TrimEnd('\0', '\n', ' ');
    }

    private static Veredicto Interpretar(string resposta) =>
        resposta.EndsWith("FOUND", StringComparison.Ordinal) ? Veredicto.Infectado
        : string.Equals(resposta, "stream: OK", StringComparison.Ordinal) ? Veredicto.Limpo
        : Veredicto.Indisponivel;
}
