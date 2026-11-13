namespace Unifesspa.UniPlus.Infrastructure.Core.IntegrationTests.Email;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Infrastructure.Core.Email;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// O transporte real, com o MailKit sobre TCP, contra um relay que prende uma conexão sem saudar: a mensagem seguinte
/// sai enquanto a primeira espera, a primeira estoura o prazo de conexão e a nova tentativa dela é entregue. Cada espera
/// tem um prazo externo próprio: se o tempo limite do transporte regredir, o teste falha em vez de ficar preso.
/// </summary>
public sealed class EnvioComRelayLentoIntegrationTests
{
    private static readonly TimeSpan PrazoExterno = TimeSpan.FromSeconds(60);

    [Fact(DisplayName = "Relay que prende uma conexão não segura a mensagem seguinte, e a presa estoura o prazo de conexão")]
    public async Task EnviarAsync_RelayPrendeUmaConexao_SeguinteSaiEAPresaEstouraOPrazo()
    {
        await using RelaySmtpLento relay = RelaySmtpLento.Iniciar(conexoesLentas: 1);
        SmtpTransporteDeEmail transporte = Transporte(relay.Porta, TimeSpan.FromSeconds(10));
        EnviarEmail seguinte = NovaMensagem();

        Task presa = await PrenderUmaConexaoAsync(relay, transporte, NovaMensagem());
        await DentroDoPrazoExterno(transporte.EnviarAsync(seguinte, CancellationToken.None), "o envio da seguinte");
        await DentroDoPrazoExterno(relay.PrimeiraEntregue, "a entrega da seguinte no relay");

        presa.IsCompleted.Should().BeFalse("a conexão presa ainda está dentro do prazo de 10 s");
        relay.Recebidas.Should().ContainSingle().Which.Should().Contain(seguinte.MensagemId.ToString("N"));
        await DentroDoPrazoExterno(presa.ContinueWith(static _ => { }, TaskScheduler.Default), "o prazo da presa");
        await presa.Awaiting(t => t).Should().ThrowAsync<TimeoutException>();
    }

    [Fact(DisplayName = "Depois do prazo vencido no relay lento, a nova tentativa da mesma mensagem é entregue")]
    public async Task EnviarAsync_NovaTentativaDepoisDoPrazo_Entrega()
    {
        await using RelaySmtpLento relay = RelaySmtpLento.Iniciar(conexoesLentas: 1);
        SmtpTransporteDeEmail transporte = Transporte(relay.Porta, TimeSpan.FromSeconds(2));
        EnviarEmail mensagem = NovaMensagem();

        Task presa = await PrenderUmaConexaoAsync(relay, transporte, mensagem);
        await DentroDoPrazoExterno(presa.ContinueWith(static _ => { }, TaskScheduler.Default), "o prazo da presa");
        await presa.Awaiting(t => t).Should().ThrowAsync<TimeoutException>();
        await DentroDoPrazoExterno(transporte.EnviarAsync(mensagem, CancellationToken.None), "a nova tentativa");

        relay.Recebidas.Should().ContainSingle().Which.Should().Contain(mensagem.MensagemId.ToString("N"));
    }

    private static async Task<Task> PrenderUmaConexaoAsync(RelaySmtpLento relay, SmtpTransporteDeEmail transporte, EnviarEmail mensagem)
    {
        Task envio = transporte.EnviarAsync(mensagem, CancellationToken.None);
        await DentroDoPrazoExterno(relay.LentaAceita, "o relay segurar a conexão");
        return envio;
    }

    private static async Task DentroDoPrazoExterno(Task espera, string oQue)
    {
        Task primeira = await Task.WhenAny(espera, Task.Delay(PrazoExterno));
        primeira.Should().BeSameAs(espera, $"{oQue} tem de terminar em {PrazoExterno.TotalSeconds:0} s");
        await espera;
    }

    private static SmtpTransporteDeEmail Transporte(int porta, TimeSpan prazoDeConexao) => new(
        Options.Create(new EmailSmtpOptions
        {
            Host = "127.0.0.1",
            Porta = porta,
            Seguranca = SegurancaSmtp.Nenhuma,
            Remetente = "nao-responda@uniplus.local",
            TempoLimiteDeConexao = prazoDeConexao,
            TempoLimiteDeEnvio = TimeSpan.FromSeconds(5),
        }),
        NullLogger<SmtpTransporteDeEmail>.Instance);

    private static EnviarEmail NovaMensagem() => EnviarEmail.Para(
        Email.Criar("candidato@unifesspa.edu.br").Value!, "Aviso de pedido", "Há um pedido aguardando sua análise.",
        "<p>Há um pedido aguardando sua análise.</p>", "governanca.pedido");
}
