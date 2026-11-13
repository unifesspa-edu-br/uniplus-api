namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Email;

using System.Diagnostics;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Infrastructure.Core.Email;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// O transporte real, com o MailKit sobre TCP, contra um relay que prende uma conexão sem saudar: a mensagem seguinte
/// sai enquanto a primeira espera, a primeira estoura o prazo de conexão e a nova tentativa dela é entregue.
/// </summary>
public sealed class EnvioComRelayLentoIntegrationTests
{
    private static readonly TimeSpan PrazoDeConexao = TimeSpan.FromSeconds(2);

    [Fact(DisplayName = "Relay que prende uma conexão não segura a mensagem seguinte, e a presa estoura o prazo de conexão")]
    public async Task EnviarAsync_RelayPrendeUmaConexao_SeguinteSaiEAPresaEstouraOPrazo()
    {
        await using RelaySmtpLento relay = RelaySmtpLento.Iniciar(conexoesLentas: 1);
        SmtpTransporteDeEmail transporte = Transporte(relay.Porta);
        EnviarEmail seguinte = NovaMensagem();
        Stopwatch cronometro = Stopwatch.StartNew();

        Task presa = await PrenderUmaConexaoAsync(relay, transporte, NovaMensagem());
        await transporte.EnviarAsync(seguinte, CancellationToken.None);

        presa.IsCompleted.Should().BeFalse("a conexão presa ainda está dentro do prazo");
        relay.Recebidas.Should().ContainSingle().Which.Should().Contain(seguinte.MensagemId.ToString("N"));
        await presa.Awaiting(t => t).Should().ThrowAsync<TimeoutException>();
        cronometro.Elapsed.Should().BeLessThan(PrazoDeConexao + TimeSpan.FromSeconds(3));
    }

    [Fact(DisplayName = "Depois do prazo vencido no relay lento, a nova tentativa da mesma mensagem é entregue")]
    public async Task EnviarAsync_NovaTentativaDepoisDoPrazo_Entrega()
    {
        await using RelaySmtpLento relay = RelaySmtpLento.Iniciar(conexoesLentas: 1);
        SmtpTransporteDeEmail transporte = Transporte(relay.Porta);
        EnviarEmail mensagem = NovaMensagem();

        Task presa = await PrenderUmaConexaoAsync(relay, transporte, mensagem);
        await presa.Awaiting(t => t).Should().ThrowAsync<TimeoutException>();
        await transporte.EnviarAsync(mensagem, CancellationToken.None);

        relay.Recebidas.Should().ContainSingle().Which.Should().Contain(mensagem.MensagemId.ToString("N"));
    }

    private static async Task<Task> PrenderUmaConexaoAsync(RelaySmtpLento relay, SmtpTransporteDeEmail transporte, EnviarEmail mensagem)
    {
        Task envio = transporte.EnviarAsync(mensagem, CancellationToken.None);
        await relay.LentaAceita.WaitAsync(TimeSpan.FromSeconds(5));
        return envio;
    }

    private static SmtpTransporteDeEmail Transporte(int porta) => new(
        Options.Create(new EmailSmtpOptions
        {
            Host = "127.0.0.1",
            Porta = porta,
            Seguranca = SegurancaSmtp.Nenhuma,
            Remetente = "nao-responda@uniplus.local",
            TempoLimiteDeConexao = PrazoDeConexao,
            TempoLimiteDeEnvio = TimeSpan.FromSeconds(5),
        }),
        NullLogger<SmtpTransporteDeEmail>.Instance);

    private static EnviarEmail NovaMensagem() => EnviarEmail.Para(
        Email.Criar("candidato@unifesspa.edu.br").Value!, "Aviso de pedido", "Há um pedido aguardando sua análise.",
        "<p>Há um pedido aguardando sua análise.</p>", "governanca.pedido");
}
