namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Email;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

/// <summary>
/// Relay SMTP mínimo, em processo e sobre TCP real: aceita a conversa do MailKit e guarda o texto de cada mensagem.
/// As conexões marcadas como lentas são aceitas e nunca recebem a saudação, como um relay preso em DNS reverso.
/// </summary>
internal sealed class RelaySmtpLento : IAsyncDisposable
{
    private readonly TcpListener _escuta = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _parada = new();
    private readonly ConcurrentQueue<string> _recebidas = new();
    private readonly TaskCompletionSource _lentaAceita = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _lentasPendentes;
    private Task _aceite = Task.CompletedTask;

    private RelaySmtpLento()
    {
    }

    public int Porta => ((IPEndPoint)_escuta.LocalEndpoint).Port;

    /// <summary>O texto bruto de cada mensagem aceita, cabeçalhos inclusive.</summary>
    public IReadOnlyCollection<string> Recebidas => _recebidas;

    /// <summary>Conclui quando a primeira conexão lenta foi aceita e está presa.</summary>
    public Task LentaAceita => _lentaAceita.Task;

    public static RelaySmtpLento Iniciar(int conexoesLentas)
    {
        RelaySmtpLento relay = new() { _lentasPendentes = conexoesLentas };
        relay._escuta.Start();
        relay._aceite = relay.AceitarAsync(relay._parada.Token);
        return relay;
    }

    public async ValueTask DisposeAsync()
    {
        await _parada.CancelAsync();
        _escuta.Stop();
        await _aceite;
        _parada.Dispose();
    }

    private async Task AceitarAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                TcpClient conexao = await _escuta.AcceptTcpClientAsync(cancellationToken);
                _ = AtenderAsync(conexao, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // Parada do relay.
        }
    }

    private async Task AtenderAsync(TcpClient conexao, CancellationToken cancellationToken)
    {
        using (conexao)
        {
            try
            {
                await (Interlocked.Decrement(ref _lentasPendentes) >= 0
                    ? SegurarAsync(cancellationToken)
                    : ConversarAsync(conexao.GetStream(), cancellationToken));
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException)
            {
                // O cliente desistiu ou o relay parou.
            }
        }
    }

    private Task SegurarAsync(CancellationToken cancellationToken)
    {
        _lentaAceita.TrySetResult();
        return Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private async Task ConversarAsync(NetworkStream fluxo, CancellationToken cancellationToken)
    {
        using StreamReader leitor = new(fluxo, Encoding.ASCII, leaveOpen: true);
        await using StreamWriter escritor = new(fluxo, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
        await escritor.WriteLineAsync("220 relay de teste");
        while (await leitor.ReadLineAsync(cancellationToken) is { } linha)
        {
            string comando = Comando(linha);
            if (comando == "DATA")
            {
                await escritor.WriteLineAsync("354 envie a mensagem");
                _recebidas.Enqueue(await LerMensagemAsync(leitor, cancellationToken));
            }

            await escritor.WriteLineAsync(comando == "QUIT" ? "221 encerrado" : "250 ok");
        }
    }

    private static string Comando(string linha) => (linha.Length > 4 ? linha[..4] : linha).ToUpperInvariant();

    private static async Task<string> LerMensagemAsync(StreamReader leitor, CancellationToken cancellationToken)
    {
        StringBuilder mensagem = new();
        while (await leitor.ReadLineAsync(cancellationToken) is { } linha && linha != ".")
        {
            mensagem.AppendLine(linha);
        }

        return mensagem.ToString();
    }
}
