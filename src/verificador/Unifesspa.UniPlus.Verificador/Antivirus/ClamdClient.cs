namespace Unifesspa.UniPlus.Verificador.Antivirus;

using System.Buffers.Binary;
using System.Globalization;
using System.Net.Sockets;
using System.Text;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Verificador.Configuracao;

/// <summary>
/// Cliente mínimo do clamd (protocolo de texto do ClamAV): <c>INSTREAM</c> envia o conteúdo em
/// pedaços precedidos do tamanho e a resposta diz <c>OK</c> ou <c>FOUND</c>; <c>VERSION</c> informa
/// a data da base. Uma conexão por comando.
/// </summary>
public sealed class ClamdClient : IClamd
{
    private const int TamanhoDoPedaco = 64 * 1024;

    // Pressão de memória ou de leitura no clamd: o mesmo arquivo pode passar numa nova tentativa.
    private static readonly string[] Transitorias = ["Can't allocate memory", "COMMAND READ TIMED OUT"];

    private static readonly byte[] ComandoInstream = "zINSTREAM\0"u8.ToArray();
    private static readonly byte[] ComandoVersion = "zVERSION\0"u8.ToArray();

    private readonly ClamdOptions _options;

    public ClamdClient(IOptions<VerificadorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value.Clamd;
    }

    public async Task<ResultadoDoClamd> VerificarAsync(Stream conteudo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        string resposta = await ExecutarAsync(
            async (rede, ct) =>
            {
                await rede.WriteAsync(ComandoInstream, ct).ConfigureAwait(false);
                await EnviarConteudoAsync(rede, conteudo, ct).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return InterpretarResposta(resposta);
    }

    public async Task<DateTimeOffset> LerDataDaBaseAsync(CancellationToken cancellationToken)
    {
        string resposta = await ExecutarAsync(
            (rede, ct) => rede.WriteAsync(ComandoVersion, ct).AsTask(),
            cancellationToken).ConfigureAwait(false);

        return InterpretarVersao(resposta);
    }

    /// <summary>
    /// <c>stream: OK</c> é limpo e <c>stream: &lt;ameaça&gt; FOUND</c> é infectado. Qualquer outra
    /// resposta é erro permanente, nunca limpo, salvo as transitórias (memória, leitura), que são indisponibilidade.
    /// </summary>
    public static ResultadoDoClamd InterpretarResposta(string resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        const string Prefixo = "stream: ";
        const string Sufixo = " FOUND";

        if (string.Equals(resposta, "stream: OK", StringComparison.Ordinal))
        {
            return new ResultadoDoClamd(false, null);
        }

        if (resposta.EndsWith(Sufixo, StringComparison.Ordinal))
        {
            string corpo = resposta.StartsWith(Prefixo, StringComparison.Ordinal) ? resposta[Prefixo.Length..] : resposta;
            return new ResultadoDoClamd(true, corpo[..^Sufixo.Length]);
        }

        if (Transitorias.Any(t => resposta.Contains(t, StringComparison.Ordinal)))
        {
            throw new ClamdIndisponivelException($"O clamd não conseguiu varrer agora: {resposta}");
        }

        throw new ClamdErroPermanenteException($"O clamd respondeu algo que não é OK nem FOUND: {resposta}");
    }

    /// <summary>A resposta do <c>VERSION</c> é <c>ClamAV 1.4.3/27800/Fri Oct 24 07:25:35 2026</c>.</summary>
    public static DateTimeOffset InterpretarVersao(string resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        string[] partes = resposta.Split('/');
        string data = partes.Length >= 3 ? string.Join(' ', partes[2].Split(' ', StringSplitOptions.RemoveEmptyEntries)) : string.Empty;

        return DateTimeOffset.TryParseExact(
            data,
            "ddd MMM d HH:mm:ss yyyy",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out DateTimeOffset resultado)
            ? resultado
            : throw new ClamdErroPermanenteException("O clamd não informou a data da base no VERSION.");
    }

    private async Task<string> ExecutarAsync(
        Func<NetworkStream, CancellationToken, Task> enviar,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource prazo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        prazo.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSegundos));
        try
        {
            using TcpClient cliente = new();
            await cliente.ConnectAsync(_options.Host, _options.Port, prazo.Token).ConfigureAwait(false);
            NetworkStream rede = cliente.GetStream();
            await using (rede.ConfigureAwait(false))
            {
                try
                {
                    await enviar(rede, prazo.Token).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    // O clamd que recusa o conteúdo responde e fecha a conexão antes de o envio terminar.
                    // A resposta dele diz se a recusa é definitiva.
                    string? recusa = await TentarLerRespostaAsync(rede, prazo.Token).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(recusa))
                    {
                        return recusa;
                    }

                    throw;
                }

                string resposta = await LerRespostaAsync(rede, prazo.Token).ConfigureAwait(false);
                return resposta.Length > 0
                    ? resposta
                    : throw new ClamdIndisponivelException("O clamd fechou a conexão sem responder.");
            }
        }
        catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new ClamdIndisponivelException($"O clamd não respondeu ({ex.GetType().Name}).", ex);
        }
    }

    private static async Task EnviarConteudoAsync(NetworkStream rede, Stream conteudo, CancellationToken cancellationToken)
    {
        byte[] tamanho = new byte[sizeof(int)];
        byte[] pedaco = new byte[TamanhoDoPedaco];
        int lidos;
        while ((lidos = await conteudo.ReadAsync(pedaco, cancellationToken).ConfigureAwait(false)) > 0)
        {
            BinaryPrimitives.WriteInt32BigEndian(tamanho, lidos);
            await rede.WriteAsync(tamanho, cancellationToken).ConfigureAwait(false);
            await rede.WriteAsync(pedaco.AsMemory(0, lidos), cancellationToken).ConfigureAwait(false);
        }

        // O pedaço de tamanho zero encerra o conteúdo.
        BinaryPrimitives.WriteInt32BigEndian(tamanho, 0);
        await rede.WriteAsync(tamanho, cancellationToken).ConfigureAwait(false);
        await rede.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> LerRespostaAsync(NetworkStream rede, CancellationToken cancellationToken)
    {
        using MemoryStream resposta = new();
        await rede.CopyToAsync(resposta, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(resposta.GetBuffer(), 0, (int)resposta.Length).TrimEnd('\0', '\n', ' ');
    }

    private static async Task<string?> TentarLerRespostaAsync(NetworkStream rede, CancellationToken cancellationToken)
    {
        try
        {
            return await LerRespostaAsync(rede, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
    }
}
