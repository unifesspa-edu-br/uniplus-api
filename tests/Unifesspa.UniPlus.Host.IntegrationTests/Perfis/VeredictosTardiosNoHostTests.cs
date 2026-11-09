namespace Unifesspa.UniPlus.Host.IntegrationTests.Perfis;

using System.Diagnostics.CodeAnalysis;
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
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class VeredictosTardiosNoHostTests : VersoesDoPerfilNoHostTestBase
{
    public VeredictosTardiosNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Theory(DisplayName = "Veredicto tardio de versão cancelada ou recusada só grava o documento, e o replay é ignorado")]
    [InlineData(VeredictoDaVerificacao.Verificado, "cancelada")]
    [InlineData(VeredictoDaVerificacao.Recusado, "cancelada")]
    [InlineData(VeredictoDaVerificacao.ReenvioNecessario, "cancelada")]
    [InlineData(VeredictoDaVerificacao.Verificado, "recusada")]
    [InlineData(VeredictoDaVerificacao.Recusado, "recusada")]
    [InlineData(VeredictoDaVerificacao.ReenvioNecessario, "recusada")]
    public async Task VeredictoTardio_SoGravaODocumento(VeredictoDaVerificacao veredicto, string situacaoDaVersao)
    {
        await ResetarAsync();
        using HttpClient gestor = await GestorAsync();
        Envio envio = await CriarPerfilAsync(gestor, Consultar);
        await ConfirmarEnvioAsync(gestor, envio);
        await EncerrarVersaoAsync(gestor, envio, situacaoDaVersao);

        DesfechoDoResultadoDaVersao primeiro = await RegistrarDiretoAsync(envio.DocumentoId, veredicto);
        string documento = await SituacaoNoBancoAsync(envio.DocumentoId);
        DesfechoDoResultadoDaVersao replay = await RegistrarDiretoAsync(envio.DocumentoId, veredicto);

        primeiro.Should().Be(DesfechoDoResultadoDaVersao.Aplicado);
        replay.Should().Be(DesfechoDoResultadoDaVersao.Ignorado);
        documento.Should().Be(veredicto == VeredictoDaVerificacao.Verificado ? "verificado" : "recusado");
        (await SituacaoNoBancoAsync(envio.DocumentoId)).Should().Be(documento);
        (await SituacaoDaVersaoNoBancoAsync(envio.VersaoId)).Should().Be(situacaoDaVersao);
        (await VigenciasAsync(envio.PerfilId)).Should().BeEmpty();
        (await LerAsync(gestor, $"{RotaPerfis}/{envio.PerfilId}")).GetProperty("composicaoEmVigor").ValueKind.Should().Be(JsonValueKind.Null);
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
