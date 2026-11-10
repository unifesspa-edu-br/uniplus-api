namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Avisos;
using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Observability;

public sealed class EmissorDeAvisosTests : IDisposable
{
    private const string Emissor = "https://idp.test/realms/unifesspa";

    private static readonly Identidade Rebeca = new(Emissor, "rebeca.lima");
    private static readonly Identidade Pedro = new(Emissor, "pedro.vitor");

    private readonly MetricasDaGovernancaDePerfis _metricas = new(TimeProvider.System);
    private readonly CenarioDaGovernancaDaVersao.RegistroDeAvisosNaMemoria _registro = new();
    private readonly EmissorDeAvisos _emissor;

    public EmissorDeAvisosTests() =>
        _emissor = new EmissorDeAvisos(_registro, _metricas, NullLogger<EmissorDeAvisos>.Instance);

    public void Dispose() => _metricas.Dispose();

    private static PedidoDeAviso Pedido(params DestinatarioDoAviso[] destinatarios) =>
        new(
            new ModeloDeAviso(TipoDeAviso.LoteEnviadoComRecusas, "Análise de atendimento", 2, [], "https://uniplus.test/janela"),
            Guid.NewGuid(),
            string.Empty,
            destinatarios,
            []);

    [Fact(DisplayName = "O aviso sem destinatário válido não sai e soma no contador de avisos sem destinatário")]
    public async Task SemDestinatario_ContaENaoEmite()
    {
        using ColetorDeMedidas coletor = new(_metricas.Medidor);

        IReadOnlyList<EnviarEmail> emitidos = await _emissor.EmitirAsync(
            Pedido(new DestinatarioDoAviso(Pedro, "endereco-invalido")), CancellationToken.None);

        emitidos.Should().BeEmpty();
        coletor.Soma("uniplus.autorizacao.avisos_sem_destinatario", "tipo", "lote_enviado_com_recusas").Should().Be(1);
    }

    [Fact(DisplayName = "Duas identidades com o mesmo endereço recebem uma mensagem só")]
    public async Task MesmoEndereco_UmaMensagem()
    {
        IReadOnlyList<EnviarEmail> emitidos = await _emissor.EmitirAsync(
            Pedido(
                new DestinatarioDoAviso(Rebeca, "rebeca-lima12@uniplus-test.local"),
                new DestinatarioDoAviso(Pedro, "REBECA-LIMA12@uniplus-test.local")),
            CancellationToken.None);

        emitidos.Should().ContainSingle();
    }

    [Fact(DisplayName = "A mesma pessoa com o endereço trocado entre duas gerações do fato recebe um aviso só")]
    public async Task EnderecoTrocado_NaoRepete()
    {
        PedidoDeAviso primeiro = Pedido(new DestinatarioDoAviso(Rebeca, "rebeca-lima12@uniplus-test.local"));
        PedidoDeAviso segundo = primeiro with { Destinatarios = [new DestinatarioDoAviso(Rebeca, "rebeca.nova@uniplus-test.local")] };

        (await _emissor.EmitirAsync(primeiro, CancellationToken.None)).Should().ContainSingle();
        (await _emissor.EmitirAsync(segundo, CancellationToken.None)).Should().BeEmpty();
    }
}
