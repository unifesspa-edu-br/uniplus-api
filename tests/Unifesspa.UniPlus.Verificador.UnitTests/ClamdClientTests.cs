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

        await rede.WriteAsync(Encoding.UTF8.GetBytes(resposta + "\0"));
    }

    private static ClamdClient Criar(int porta) =>
        new(Options.Create(new VerificadorOptions { Clamd = { Host = "127.0.0.1", Port = porta, TimeoutSegundos = 5 } }));
}
