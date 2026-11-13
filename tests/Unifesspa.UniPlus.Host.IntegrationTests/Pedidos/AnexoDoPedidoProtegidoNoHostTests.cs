namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;

/// <summary>
/// O anexo do pedido protegido que declarou o documento na criação: só o solicitante anexa, o PDF tem de 1 byte a 10 MB,
/// e depois da primeira decisão o envio é recusado. O pedido protegido que não declarou é coberto em
/// <see cref="AceiteDoTermoNoHostTests"/>.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class AnexoDoPedidoProtegidoNoHostTests : TermoNoHostTestBase
{
    public AnexoDoPedidoProtegidoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "No pedido protegido que declarou o documento, só o solicitante anexa, e o PDF tem de 1 byte a 10 MB")]
    public async Task Declarado_SoOSolicitanteEDentroDoTamanho()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        Guid id = await DeclaradoParaLeandroAsync(betina, perfil, unidade);

        (await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 100 })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ConferirAsync(await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 0 }), HttpStatusCode.UnprocessableEntity, "pedido.anexo_invalido");
        await ConferirAsync(
            await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 10L * 1024 * 1024 + 1 }), HttpStatusCode.UnprocessableEntity, "pedido.anexo_invalido");
    }

    [Fact(DisplayName = "No pedido protegido que declarou o documento, o envio de anexo depois da primeira decisão é recusado")]
    public async Task Declarado_DepoisDaDecisao_Recusa()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = await DeclaradoParaLeandroAsync(betina, perfil, unidade);
        await AnexarVerificadoAsync(betina, id);
        await AceitarQuandoLiberadoAsync(id);

        (await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes")).StatusCode.Should().Be(HttpStatusCode.OK);

        await ConferirAsync(await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = 100 }), HttpStatusCode.UnprocessableEntity, "pedido.situacao_invalida");
    }

    private static async Task<Guid> DeclaradoParaLeandroAsync(HttpClient betina, Guid perfil, Guid unidade)
    {
        using HttpResponseMessage criado = await EnviarAsync(
            betina, HttpMethod.Post, Rota, new { beneficiarioEmail = Leandro.Email, perfilId = perfil, unidadeId = unidade, justificativa = Justificativa, anexaraDocumento = true });

        return (await CorpoAsync(criado, HttpStatusCode.Created)).GetProperty("id").GetGuid();
    }

    private async Task AceitarQuandoLiberadoAsync(Guid id)
    {
        using HttpClient leandro = Cliente(Leandro);
        await AguardarAsync(async () => await SituacaoAsync(leandro, id) == "aguardando_beneficiario", "o documento verificado não liberou o aceite");
        (await AceitarAsync(leandro, id)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
