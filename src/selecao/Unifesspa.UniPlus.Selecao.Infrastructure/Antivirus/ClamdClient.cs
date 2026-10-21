namespace Unifesspa.UniPlus.Selecao.Infrastructure.Antivirus;

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>O que o antivírus concluiu sobre o conteúdo.</summary>
public enum VeredictoDoAntivirus
{
    Limpo,
    Infectado,

    /// <summary>Não foi possível verificar: o clamd não respondeu, recusou o conteúdo ou passou do prazo.</summary>
    Indisponivel,
}

/// <summary>
/// Cliente mínimo do clamd pelo comando <c>INSTREAM</c> (protocolo de texto do ClamAV): o conteúdo
/// segue em pedaços precedidos do tamanho, e a resposta diz <c>OK</c> ou <c>FOUND</c>. Uma conexão por
/// verificação. O nome da ameaça nunca sai daqui: junto do arquivo de uma pessoa ele é dado dela.
/// </summary>
public sealed partial class ClamdClient
{
    private const int TamanhoDoPedaco = 64 * 1024;

    private static readonly byte[] Comando = "zINSTREAM\0"u8.ToArray();

    private readonly ClamdOptions _options;
    private readonly ILogger<ClamdClient> _logger;

    public ClamdClient(IOptions<ClamdOptions> options, ILogger<ClamdClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _logger = logger;
    }

    public async Task<VeredictoDoAntivirus> VerificarAsync(byte[] conteudo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conteudo);

        using CancellationTokenSource prazo = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        prazo.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSegundos));
        try
        {
            using TcpClient cliente = new();
            await cliente.ConnectAsync(_options.Host, _options.Port, prazo.Token).ConfigureAwait(false);
            NetworkStream rede = cliente.GetStream();
            await using (rede.ConfigureAwait(false))
            {
                await EnviarAsync(rede, conteudo, prazo.Token).ConfigureAwait(false);
                return Interpretar(await LerRespostaAsync(rede, prazo.Token).ConfigureAwait(false));
            }
        }
        catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            LogIndisponivel(_logger, ex.GetType().Name);
            return VeredictoDoAntivirus.Indisponivel;
        }
    }

    private static async Task EnviarAsync(NetworkStream rede, byte[] conteudo, CancellationToken cancellationToken)
    {
        await rede.WriteAsync(Comando, cancellationToken).ConfigureAwait(false);

        byte[] tamanho = new byte[sizeof(int)];
        for (int deslocamento = 0; deslocamento < conteudo.Length; deslocamento += TamanhoDoPedaco)
        {
            int pedaco = Math.Min(TamanhoDoPedaco, conteudo.Length - deslocamento);
            BinaryPrimitives.WriteInt32BigEndian(tamanho, pedaco);
            await rede.WriteAsync(tamanho, cancellationToken).ConfigureAwait(false);
            await rede.WriteAsync(conteudo.AsMemory(deslocamento, pedaco), cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// <c>stream: OK</c> é limpo e <c>stream: &lt;ameaça&gt; FOUND</c> é infectado; qualquer outra resposta
    /// (<c>ERROR</c>, limite de tamanho do clamd, resposta vazia) é não verificado, nunca limpo.
    /// </summary>
    private static VeredictoDoAntivirus Interpretar(string resposta) =>
        resposta.EndsWith("FOUND", StringComparison.Ordinal) ? VeredictoDoAntivirus.Infectado
        : string.Equals(resposta, "stream: OK", StringComparison.Ordinal) ? VeredictoDoAntivirus.Limpo
        : VeredictoDoAntivirus.Indisponivel;

    [LoggerMessage(Level = LogLevel.Warning, Message = "O clamd não verificou o arquivo ({TipoDaFalha})")]
    private static partial void LogIndisponivel(ILogger logger, string tipoDaFalha);
}
