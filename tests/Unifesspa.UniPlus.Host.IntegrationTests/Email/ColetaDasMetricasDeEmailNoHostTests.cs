namespace Unifesspa.UniPlus.Host.IntegrationTests.Email;


using AwesomeAssertions;

using MailKit.Net.Smtp;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Infrastructure.Core.Email;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

using Wolverine;

/// <summary>
/// As métricas do envio de e-mail pelo pipeline OpenTelemetry real do host, com um exportador em memória: a série dos
/// instantes existe desde a subida, antes de qualquer envio, e a entrega soma aceites e falhas por tipo.
/// </summary>
[Collection(MonolitoComMailpitCollection.Name)]
public sealed class ColetaDasMetricasDeEmailNoHostTests : IClassFixture<HostComColetaDeMetricasDeEmail>
{
    private readonly MonolitoComMailpitFixture _fixture;
    private readonly HostComColetaDeMetricasDeEmail _host;

    public ColetaDasMetricasDeEmailNoHostTests(MonolitoComMailpitFixture fixture, HostComColetaDeMetricasDeEmail host)
    {
        _fixture = fixture;
        _host = host;
    }

    [Fact(DisplayName = "Com o host iniciado e antes de qualquer envio, a coleta traz o último envio e a última falha em zero")]
    public void AntesDeQualquerEnvio_OsInstantesVemEmZero()
    {
        IReadOnlyList<MedidaExportada> medidas = _host.Exportar(_fixture);

        medidas.Should().Contain(m => m.Instrumento == "uniplus.email.ultimo_envio" && m.Valor == 0);
        medidas.Should().Contain(m => m.Instrumento == "uniplus.email.ultima_falha" && m.Valor == 0);
    }

    [Fact(DisplayName = "A entrega soma aceites e falhas por tipo e move os instantes no pipeline exportado")]
    public async Task Entrega_SomaAceitesEFalhasPorTipo()
    {
        TransporteFalso transporte = new();
        EntregaDeEmail entrega = ActivatorUtilities.CreateInstance<EntregaDeEmail>(_host.Obter(_fixture).Services, transporte);

        await EntregarUmAceiteEDuasFalhasAsync(entrega, transporte);

        IReadOnlyList<MedidaExportada> medidas = _host.Exportar(_fixture);

        medidas.Single(m => m.Instrumento == "uniplus.email.enviados").Valor.Should().Be(1);
        medidas.Single(m => m.Instrumento == "uniplus.email.falhas" && (string?)m.Etiquetas["tipo"] == "transitoria").Valor.Should().Be(1);
        medidas.Single(m => m.Instrumento == "uniplus.email.falhas" && (string?)m.Etiquetas["tipo"] == "permanente").Valor.Should().Be(1);
        medidas.Single(m => m.Instrumento == "uniplus.email.ultimo_envio").Valor.Should().BeGreaterThan(0);
        medidas.Single(m => m.Instrumento == "uniplus.email.ultima_falha").Valor.Should().BeGreaterThanOrEqualTo(
            medidas.Single(m => m.Instrumento == "uniplus.email.ultimo_envio").Valor);
    }

    private static async Task EntregarUmAceiteEDuasFalhasAsync(EntregaDeEmail entrega, TransporteFalso transporte)
    {
        await entrega.Handle(NovaMensagem(), new Envelope(), CancellationToken.None);
        transporte.Falha = new InvalidOperationException();
        await ((Func<Task>)(() => entrega.Handle(NovaMensagem(), new Envelope(), CancellationToken.None))).Should().ThrowAsync<FalhaNoEnvioDeEmailException>();
        transporte.Falha = new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "550");
        await ((Func<Task>)(() => entrega.Handle(NovaMensagem(), new Envelope(), CancellationToken.None))).Should().ThrowAsync<EnvioDeEmailRecusadoException>();
    }

    private static EnviarEmail NovaMensagem() => EnviarEmail.Para(
        Email.Criar("candidato@unifesspa.edu.br").Value!, "Teste", "Corpo em texto.", "<p>Corpo em HTML.</p>", "teste.metricas");

    private sealed class TransporteFalso : ITransporteDeEmail
    {
        public Exception? Falha { get; set; }

        public Task EnviarAsync(EnviarEmail mensagem, Action aoAceitar, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(aoAceitar);
            if (Falha is not null)
            {
                return Task.FromException(Falha);
            }

            aoAceitar();
            return Task.CompletedTask;
        }
    }
}
