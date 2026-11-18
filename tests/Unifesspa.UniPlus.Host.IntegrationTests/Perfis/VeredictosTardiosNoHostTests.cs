namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// O veredicto que chega depois de a versão ser cancelada ou recusada só grava o documento: a versão não reabre e
/// nenhuma vigência nasce. O mesmo resultado entregue de novo é ignorado.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class VeredictosTardiosNoHostTests : VersoesDoPerfilNoHostTestBase
{
    public VeredictosTardiosNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Theory(DisplayName = "O veredicto tardio grava o documento sem reabrir a versão e sua repetição é ignorada")]
    [InlineData(VeredictoDaVerificacao.Verificado, "cancelada")]
    [InlineData(VeredictoDaVerificacao.Recusado, "cancelada")]
    [InlineData(VeredictoDaVerificacao.ReenvioNecessario, "cancelada")]
    [InlineData(VeredictoDaVerificacao.Verificado, "recusada")]
    [InlineData(VeredictoDaVerificacao.Recusado, "recusada")]
    [InlineData(VeredictoDaVerificacao.ReenvioNecessario, "recusada")]
    public async Task VeredictoTardio_GravaDocumentoSemReabrirVersao(VeredictoDaVerificacao veredicto, string situacao)
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        var cenario = new CenarioDoVeredicto(this, gestor, veredicto, situacao);
        await cenario.ExecutarAsync();

        cenario.Primeiro.Should().Be(DesfechoDoResultadoDaVersao.Aplicado);
        cenario.Repeticao.Should().Be(DesfechoDoResultadoDaVersao.Ignorado);
        cenario.Documento.Should().Be(veredicto == VeredictoDaVerificacao.Verificado ? "verificado" : "recusado");
        (await SituacaoNoBancoAsync(cenario.Envio.DocumentoId)).Should().Be(cenario.Documento);
        (await SituacaoDaVersaoNoBancoAsync(cenario.Envio.VersaoId)).Should().Be(situacao);
        (await VigenciasAsync(cenario.Envio.PerfilId)).Should().BeEmpty();
        (await LerAsync(gestor, $"{RotaPerfis}/{cenario.Envio.PerfilId}")).GetProperty("composicaoEmVigor").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private sealed class CenarioDoVeredicto(VeredictosTardiosNoHostTests teste, HttpClient gestor, VeredictoDaVerificacao veredicto, string situacao)
    {
        public Envio Envio { get; private set; } = null!;
        public DesfechoDoResultadoDaVersao Primeiro { get; private set; }
        public DesfechoDoResultadoDaVersao Repeticao { get; private set; }
        public string Documento { get; private set; } = string.Empty;

        public async Task ExecutarAsync()
        {
            Envio = await teste.PrepararVersaoEncerradaAsync(gestor, situacao);
            Primeiro = await teste.RegistrarDiretoAsync(Envio.DocumentoId, veredicto);
            Documento = await teste.SituacaoNoBancoAsync(Envio.DocumentoId);
            Repeticao = await teste.RegistrarDiretoAsync(Envio.DocumentoId, veredicto);
        }
    }

    private async Task<Envio> PrepararVersaoEncerradaAsync(HttpClient gestor, string situacao)
    {
        Envio envio = await CriarPerfilAsync(gestor, Consultar);
        await ConfirmarEnvioAsync(gestor, envio);
        await EncerrarVersaoAsync(gestor, envio, situacao);

        return envio;
    }

    private async Task EncerrarVersaoAsync(HttpClient gestor, Envio envio, string situacao)
    {
        if (situacao == "cancelada")
        {
            (await CancelarBrutoAsync(gestor, envio)).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
            return;
        }

        using HttpClient gestao = await GestaoAsync();
        (await RecusarBrutoAsync(gestao, envio, EtapaGestao)).StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }
}
