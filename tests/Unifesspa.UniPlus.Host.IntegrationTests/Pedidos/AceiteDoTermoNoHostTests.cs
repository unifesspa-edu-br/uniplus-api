namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Host.IntegrationTests.Instalacao;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// A ordem documento verificado, aceite do termo e gestor nos três casos de aceite, quem aceita, a verificação recusada,
/// a aprovação com documento pendente e a versão fixada depois de nova publicação.
/// </summary>
[Collection(MonolitoDaInstalacaoCollection.Name)]
public sealed class AceiteDoTermoNoHostTests : TermoNoHostTestBase
{
    public AceiteDoTermoNoHostTests(MonolitoDaInstalacaoFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName = "Pedido próprio sem documento: o envio é o aceite, e o pedido vai ao gestor com a evidência")]
    public async Task ParaSiSemDocumento_AceiteNoEnvio()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient betina = Cliente(Betina);

        JsonElement pedido = await CorpoAsync(await PedirAsync(betina, perfil, unidade, Vigente!.Id), HttpStatusCode.Created);

        pedido.GetProperty("situacao").GetString().Should().Be("aguardando_gestor");
        JsonElement aceite = pedido.GetProperty("termo").GetProperty("aceite");
        aceite.GetProperty("versaoId").GetGuid().Should().Be(Vigente.Id);
        aceite.GetProperty("resumo").GetString().Should().Be(Vigente.Resumo);
        aceite.GetProperty("pessoa").GetProperty("sub").GetString().Should().Be(Betina.Sub.ToString());
        (await TiposDaTrilhaAsync(betina, pedido.GetProperty("id").GetGuid())).Should().Contain("termo_aceito");
    }

    [Fact(DisplayName = "Pedido próprio com documento: aceite e aprovação esperam a verificação, que leva a aguardar o beneficiário")]
    public async Task ParaSiComDocumento_AceiteDepoisDaVerificacao()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        Guid id = (await CorpoAsync(await PedirAsync(betina, perfil, unidade, Vigente!.Id, anexara: true), HttpStatusCode.Created)).GetProperty("id").GetGuid();

        await ConferirAsync(await AceitarAsync(betina, id), HttpStatusCode.UnprocessableEntity, "pedido.anexo_nao_verificado");
        await ConferirAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.UnprocessableEntity, "pedido.");
        await AnexarVerificadoAsync(betina, id);
        await AguardarAsync(async () => await SituacaoAsync(betina, id) == "aguardando_beneficiario", "o pedido próprio protegido não passou a aguardar o aceite");

        (await AceitarAsync(betina, id)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SituacaoAsync(betina, id)).Should().Be("aguardando_gestor");
    }

    [Fact(DisplayName = "Pedido para outra pessoa: só o beneficiário aceita o termo, junto com o pedido")]
    public async Task ParaOutraPessoa_SoOBeneficiarioAceita()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient betina = Cliente(Betina);
        using HttpClient leandro = Cliente(Leandro);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);

        await ConferirAsync(await AceitarAsync(betina, id), HttpStatusCode.Forbidden, "pedido.nao_e_o_beneficiario");
        await ConferirAsync(
            await EnviarAsync(leandro, HttpMethod.Post, $"{Rota}/{id}/aceite", new { termoVersaoApresentadaId = Guid.NewGuid() }), HttpStatusCode.Conflict, "pedido.termo_divergente");
        (await AceitarAsync(leandro, id)).StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement aceite = (await TermoDoPedidoAsync(leandro, id)).GetProperty("aceite");
        aceite.GetProperty("pessoa").GetProperty("sub").GetString().Should().Be(Leandro.Sub.ToString());
    }

    [Fact(DisplayName = "Verificação recusada mantém o pedido aguardando o documento; outro anexo verificado libera o aceite")]
    public async Task VerificacaoRecusada_ContinuaAguardandoODocumento()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient betina = Cliente(Betina);
        Guid id = (await CorpoAsync(await PedirAsync(betina, perfil, unidade, Vigente!.Id, anexara: true), HttpStatusCode.Created)).GetProperty("id").GetGuid();
        Guid recusado = await AnexarEmVerificacaoAsync(betina, id);

        await InjetarResultadoAsync(recusado, VeredictoDaVerificacao.Recusado);
        await AguardarAsync(async () => await SituacaoDoAnexoAsync(betina, id, recusado) == "recusado", "o anexo não chegou a recusado");

        (await SituacaoAsync(betina, id)).Should().Be("aguardando_documento");
        await AnexarVerificadoAsync(betina, id);
        await AguardarAsync(async () => await SituacaoAsync(betina, id) == "aguardando_beneficiario", "o novo anexo não liberou o aceite");
    }

    [Fact(DisplayName = "O pedido protegido que não declarou documento não recebe anexo")]
    public async Task SemDeclaracao_NaoRecebeAnexo()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient betina = Cliente(Betina);
        Guid id = await CriarAsync(betina, perfil, unidade, Leandro);

        await ConferirAsync(
            await EnviarAsync(betina, HttpMethod.Post, $"{Rota}/{id}/anexos", new { tamanhoDoPdf = Pdf.Length }), HttpStatusCode.UnprocessableEntity, "pedido.anexo_nao_declarado");
    }

    [Fact(DisplayName = "Nova publicação não derruba: o pedido com a versão anterior é aceito e concedido, e a trilha da concessão mostra o aceite")]
    public async Task NovaPublicacao_VersaoFixadaContinuaValendo()
    {
        (Guid perfil, Guid unidade) = await CenarioAsync();
        using HttpClient leandro = Cliente(Leandro);
        using HttpClient diogo = await PublicadorAsync();
        Guid id = await CriarAsync(leandro, perfil, unidade, null);
        await CorpoAsync(await PublicarAsync(diogo, "segunda versão", Vigente!.Id), HttpStatusCode.Created);

        Guid concessao = await AprovarNasDuasEtapasAsync(id);

        JsonElement trilha = await LerAsync(diogo, $"/api/autorizacao/concessoes/{concessao}/trilha");
        trilha.EnumerateArray().Single(e => e.GetProperty("tipo").GetString() == "termo_aceito")
            .GetProperty("detalhe").GetString().Should().Be($"Versão 1, resumo {Vigente.Resumo}.");
    }

    private async Task<Guid> AprovarNasDuasEtapasAsync(Guid id)
    {
        using HttpClient diogo = await ClienteComPermissoesAsync(Diogo, AprovarFuncional);
        using HttpClient marlene = await ClienteComPermissoesAsync(Marlene, AprovarProtecaoDeDados);
        await CorpoAsync(await EnviarAsync(diogo, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.OK);
        JsonElement aprovado = await CorpoAsync(await EnviarAsync(marlene, HttpMethod.Post, $"{Rota}/{id}/aprovacoes"), HttpStatusCode.OK);

        return aprovado.GetProperty("concessaoId").GetGuid();
    }
}
