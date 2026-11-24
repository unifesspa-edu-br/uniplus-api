namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
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
            new ModeloDeAviso(TipoDeAviso.DecisoesEnviadasComRecusas, "Análise de atendimento", 2, [], "https://uniplus.test/revisao"),
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
        coletor.Soma("uniplus.autorizacao.avisos_sem_destinatario", "tipo", "DECISOES_ENVIADAS_COM_RECUSAS").Should().Be(1);
    }

    [Fact(DisplayName = "O aviso sem destinatário válido não chama o registro")]
    public async Task SemDestinatario_NaoChamaORegistro()
    {
        IRegistroDeAvisos registro = Substitute.For<IRegistroDeAvisos>();
        EmissorDeAvisos emissor = new(registro, _metricas, NullLogger<EmissorDeAvisos>.Instance);

        await emissor.EmitirAsync(Pedido(new DestinatarioDoAviso(Pedro, "endereco-invalido")), CancellationToken.None);

        await registro.DidNotReceiveWithAnyArgs().RegistrarAsync(default!, default, default!, default);
    }

    [Fact(DisplayName = "Cada emissão sem destinatário válido soma uma vez no contador")]
    public async Task SemDestinatario_DuasEmissoes_SomaDuas()
    {
        using ColetorDeMedidas coletor = new(_metricas.Medidor);
        PedidoDeAviso pedido = Pedido(new DestinatarioDoAviso(Pedro, "endereco-invalido"));

        await _emissor.EmitirAsync(pedido, CancellationToken.None);
        await _emissor.EmitirAsync(pedido, CancellationToken.None);

        coletor.Soma("uniplus.autorizacao.avisos_sem_destinatario", "tipo", "DECISOES_ENVIADAS_COM_RECUSAS").Should().Be(2);
    }

    [Fact(DisplayName = "O aviso com destinatário chama o registro uma vez, com os identificadores em ordem, e não soma no contador")]
    public async Task ComDestinatario_ChamaORegistroUmaVez()
    {
        IRegistroDeAvisos registro = Substitute.For<IRegistroDeAvisos>();
        registro.RegistrarAsync(default!, default, default!, default).ReturnsForAnyArgs(Task.FromResult<IReadOnlyCollection<Guid>>([]));
        EmissorDeAvisos emissor = new(registro, _metricas, NullLogger<EmissorDeAvisos>.Instance);
        PedidoDeAviso pedido = Pedido(new DestinatarioDoAviso(Rebeca, "rebeca-lima12@uniplus-test.local"), new DestinatarioDoAviso(Pedro, "pedro-vitor@uniplus-test.local"));
        using ColetorDeMedidas coletor = new(_metricas.Medidor);

        await emissor.EmitirAsync(pedido, CancellationToken.None);

        await registro.Received(1).RegistrarAsync(
            "DECISOES_ENVIADAS_COM_RECUSAS", pedido.FatoId, Arg.Is<IReadOnlyList<Guid>>(ids => ids.Count == 2 && ids.SequenceEqual(ids.Order())), Arg.Any<CancellationToken>());
        coletor.Soma("uniplus.autorizacao.avisos_sem_destinatario", "tipo", "DECISOES_ENVIADAS_COM_RECUSAS").Should().Be(0);
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
