namespace Unifesspa.UniPlus.Verificador.UnitTests;

using System.Net;
using System.Net.Sockets;
using System.Text;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Verificador.Antivirus;
using Unifesspa.UniPlus.Verificador.Configuracao;

public sealed class ClamdClientTests
{
    private const int ConteudoGrandeEmBytes = 64 * 1024 * 1024;

    [Fact(DisplayName = "Resposta do clamd que não é OK nem FOUND é erro permanente, e não indisponibilidade")]
    public async Task RespostaDeErro_LancaErroPermanente()
    {
        using TcpListener servidor = Iniciar();
        Task atendimento = AtenderAsync(servidor, "INSTREAM size limit exceeded. ERROR");
        ClamdClient cliente = Criar(((IPEndPoint)servidor.LocalEndpoint).Port);
        using MemoryStream conteudo = new([1, 2, 3]);

        Func<Task> acao = () => cliente.VerificarAsync(conteudo, CancellationToken.None);

        await acao.Should().ThrowAsync<ClamdErroPermanenteException>();
        await atendimento;
    }

    [Theory(DisplayName = "Resposta transitória do clamd é indisponibilidade, e não recusa do arquivo")]
    [InlineData("INSTREAM: Can't allocate memory ERROR")]
    [InlineData("COMMAND READ TIMED OUT")]
    public void RespostaTransitoria_EIndisponibilidade(string resposta)
    {
        Action acao = () => ClamdClient.InterpretarResposta(resposta);

        acao.Should().Throw<ClamdIndisponivelException>();
    }

    [Fact(DisplayName = "Clamd fora do ar é indisponibilidade, que a política de erro tenta de novo")]
    public async Task ClamdForaDoAr_LancaIndisponivel()
    {
        using TcpListener servidor = Iniciar();
        int porta = ((IPEndPoint)servidor.LocalEndpoint).Port;
        servidor.Stop();
        ClamdClient cliente = Criar(porta);
        using MemoryStream conteudo = new([1, 2, 3]);

        Func<Task> acao = () => cliente.VerificarAsync(conteudo, CancellationToken.None);

        await acao.Should().ThrowAsync<ClamdIndisponivelException>();
    }

    [Fact(DisplayName = "FOUND devolve a assinatura e OK devolve limpo")]
    public void Interpretar_DistingueFoundDeOk()
    {
        ClamdClient.InterpretarResposta("stream: Eicar-Test-Signature FOUND").Should().Be(new ResultadoDoClamd(true, "Eicar-Test-Signature"));
        ClamdClient.InterpretarResposta("stream: OK").Infectado.Should().BeFalse();
    }

    [Fact(DisplayName = "A data da base vem do terceiro campo do VERSION")]
    public void InterpretarVersao_LeADataDaBase() =>
        ClamdClient.InterpretarVersao("ClamAV 1.4.3/27800/Fri Oct  2 07:25:35 2026")
            .Should().Be(new DateTimeOffset(2026, 10, 2, 7, 25, 35, TimeSpan.Zero));

    [Theory(DisplayName = "OK devolve limpo e FOUND devolve a assinatura, pela conversa real com o servidor")]
    [InlineData("stream: OK", false, null)]
    [InlineData("stream: Eicar-Test-Signature FOUND", true, "Eicar-Test-Signature")]
    public async Task VerificarAsync_RespostaOkOuFound_DevolveOResultadoDoClamd(string resposta, bool infectado, string? assinatura)
    {
        using TcpListener servidor = Iniciar();
        Task atendimento = AtenderAsync(servidor, resposta);
        using MemoryStream conteudo = new([1, 2, 3]);

        ResultadoDoClamd resultado = await Criar(((IPEndPoint)servidor.LocalEndpoint).Port).VerificarAsync(conteudo, CancellationToken.None);

        resultado.Should().Be(new ResultadoDoClamd(infectado, assinatura));
        await atendimento;
    }

    [Fact(DisplayName = "Conexão que fecha sem resposta é indisponibilidade")]
    public async Task VerificarAsync_ConexaoFechadaSemResposta_LancaIndisponivel()
    {
        using TcpListener servidor = Iniciar();
        Task atendimento = AtenderAsync(servidor, string.Empty);
        using MemoryStream conteudo = new([1, 2, 3]);

        Func<Task> acao = () => Criar(((IPEndPoint)servidor.LocalEndpoint).Port).VerificarAsync(conteudo, CancellationToken.None);

        (await acao.Should().ThrowAsync<ClamdIndisponivelException>()).WithMessage("*fechou a conexão sem responder*");
        await atendimento;
    }

    [Fact(DisplayName = "Prazo esgotado sem resposta do clamd é indisponibilidade")]
    public async Task VerificarAsync_PrazoEsgotado_LancaIndisponivel()
    {
        using TcpListener servidor = Iniciar();
        using CancellationTokenSource fimDoTeste = new();
        Task atendimento = SegurarSemResponderAsync(servidor, fimDoTeste.Token);
        using MemoryStream conteudo = new([1, 2, 3]);

        Func<Task> acao = () => Criar(((IPEndPoint)servidor.LocalEndpoint).Port, prazoEmSegundos: 1).VerificarAsync(conteudo, CancellationToken.None);

        await acao.Should().ThrowAsync<ClamdIndisponivelException>();
        await fimDoTeste.CancelAsync();
        await Record.ExceptionAsync(() => atendimento);
    }

    [Fact(DisplayName = "Cancelamento pedido pelo chamador segue como cancelamento, e não como indisponibilidade")]
    public async Task VerificarAsync_CancelamentoDoChamador_LancaOperationCanceled()
    {
        using TcpListener servidor = Iniciar();
        using CancellationTokenSource cancelado = new();
        await cancelado.CancelAsync();
        using MemoryStream conteudo = new([1, 2, 3]);

        Func<Task> acao = () => Criar(((IPEndPoint)servidor.LocalEndpoint).Port).VerificarAsync(conteudo, cancelado.Token);

        await acao.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "A resposta que não é OK nem FOUND carrega a resposta na mensagem do erro permanente")]
    public void InterpretarResposta_RespostaDesconhecida_LancaErroPermanenteComARespostaNaMensagem()
    {
        Action acao = () => ClamdClient.InterpretarResposta("INSTREAM size limit exceeded. ERROR");

        acao.Should().Throw<ClamdErroPermanenteException>().WithMessage("*INSTREAM size limit exceeded. ERROR*");
    }

    [Fact(DisplayName = "Recusa que o clamd escreve antes de resetar a conexão é lida e vira erro permanente com a mensagem preservada")]
    public async Task VerificarAsync_RecusaEscritaAntesDoReset_LancaErroPermanenteComAMensagem()
    {
        using TcpListener servidor = Iniciar();
        Task atendimento = ResponderEResetarAsync(servidor, "INSTREAM size limit exceeded. ERROR");
        using MemoryStream conteudo = new(new byte[ConteudoGrandeEmBytes]);

        Func<Task> acao = () => Criar(((IPEndPoint)servidor.LocalEndpoint).Port).VerificarAsync(conteudo, CancellationToken.None);

        (await acao.Should().ThrowAsync<ClamdErroPermanenteException>()).WithMessage("*INSTREAM size limit exceeded. ERROR*");
        await atendimento;
    }

    [Fact(DisplayName = "Reset da conexão sem recusa legível relança a falha do envio como indisponibilidade")]
    public async Task VerificarAsync_ResetSemRecusaLegivel_LancaIndisponivelComAIOException()
    {
        using TcpListener servidor = Iniciar();
        Task atendimento = ResponderEResetarAsync(servidor, string.Empty);
        using MemoryStream conteudo = new(new byte[ConteudoGrandeEmBytes]);

        Func<Task> acao = () => Criar(((IPEndPoint)servidor.LocalEndpoint).Port).VerificarAsync(conteudo, CancellationToken.None);

        (await acao.Should().ThrowAsync<ClamdIndisponivelException>()).WithInnerException<IOException>();
        await atendimento;
    }

    // Lê pouco, escreve a resposta (se houver) e fecha com RST: a escrita do cliente falha antes de terminar o envio.
    private static async Task ResponderEResetarAsync(TcpListener servidor, string resposta)
    {
        using TcpClient conexao = await servidor.AcceptTcpClientAsync();
        conexao.LingerState = new LingerOption(true, 0);
        NetworkStream rede = conexao.GetStream();
        _ = await rede.ReadAtLeastAsync(new byte[100], 100, throwOnEndOfStream: false);
        if (!string.IsNullOrEmpty(resposta))
        {
            await rede.WriteAsync(Encoding.UTF8.GetBytes(resposta + "\0"));
        }
    }

    private static async Task SegurarSemResponderAsync(TcpListener servidor, CancellationToken fim)
    {
        using TcpClient conexao = await servidor.AcceptTcpClientAsync(fim);
        await Task.Delay(Timeout.Infinite, fim);
    }

    private static TcpListener Iniciar()
    {
        TcpListener servidor = new(IPAddress.Loopback, 0);
        servidor.Start();
        return servidor;
    }

    private static async Task AtenderAsync(TcpListener servidor, string resposta)
    {
        using TcpClient conexao = await servidor.AcceptTcpClientAsync();
        NetworkStream rede = conexao.GetStream();
        byte[] descarte = new byte[4096];
        // O cliente termina o envio com o pedaço de tamanho zero; só depois o servidor responde.
        int total = 0;
        while (total < 14)
        {
            total += await rede.ReadAsync(descarte);
        }

        if (!string.IsNullOrEmpty(resposta))
        {
            await rede.WriteAsync(Encoding.UTF8.GetBytes(resposta + "\0"));
        }
    }

    private static ClamdClient Criar(int porta, int prazoEmSegundos = 5) =>
        new(Options.Create(new VerificadorOptions { Clamd = { Host = "127.0.0.1", Port = porta, TimeoutSegundos = prazoEmSegundos } }));
}
