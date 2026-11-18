namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FatoCitadoDeTeste;

/// <summary>
/// A retificação confere os eixos do fato já coletado contra o que as versões publicadas congelaram
/// (UNI-REQ-0144): o domínio e o tipo de renderização não mudam sob o fato citado, e o fato novo é copiado do
/// catálogo vivo.
/// </summary>
public sealed class EixosDoFatoColetadoNaRetificacaoTests
{
    [Fact(DisplayName = "Retificar recusa o fato citado por condição de documento cujo domínio o catálogo trocou desde a versão publicada")]
    public async Task Retificar_DominioDoFatoCitadoTrocado_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { DominioCongelado = "CATEGORICO" });

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Error!.Code.Should().Be(CodigoDaRecusaDoEixoAlterado);
        resposta.Error.Message.Should().Contain(Fato).And.Contain("domínio");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar recusa junto o fato que o catálogo reclassificou e o fato citado cujo domínio foi trocado, nessa ordem")]
    public async Task Retificar_FatoReclassificadoEFatoComDominioTrocado_RecusaOsDoisNaOrdem()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(
            new SituacaoDoFatoCitado { DominioCongelado = "CATEGORICO", ComItemNovo = true, ItemNovoReclassificadoNoCatalogo = true });

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().Equal("ProcessoSeletivo.FatoColetadoNaoMaisDeclarado", CodigoDaRecusaDoEixoAlterado);
        resposta.Errors[0].Error.Message.Should().Contain(FatoNovo);
        resposta.Errors[1].Error.Message.Should().Contain(Fato).And.Contain("domínio");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar recusa o fato citado que o catálogo não tem e, junto, o fato citado cujo domínio foi trocado, nessa ordem")]
    public async Task Retificar_FatoCitadoAusenteEFatoComDominioTrocado_RecusaOsDoisNaOrdem()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(
            new SituacaoDoFatoCitado { DominioCongelado = "CATEGORICO", ExigenciaCitaFatoAusenteDoCatalogo = true });

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        string[] codigos = [.. resposta.Errors.Select(static e => e.Error.Code)];
        codigos.Should().ContainInOrder("ProcessoSeletivo.FatoCongeladoNaoEncontrado", CodigoDaRecusaDoEixoAlterado);
        resposta.Errors.First(static e => e.Error.Code == "ProcessoSeletivo.FatoCongeladoNaoEncontrado").Error.Message.Should().Contain(FatoAusenteDoCatalogo);
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar com o fato citado que o catálogo deixou de aceitar e de domínio trocado recusa só a coletabilidade")]
    public async Task Retificar_FatoNaoColetavelComDominioTrocado_RecusaSoAColetabilidade()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(
            new SituacaoDoFatoCitado { DominioCongelado = "CATEGORICO", FatoCitadoReclassificadoNoCatalogo = true });

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        resposta.Errors.Select(static e => e.Error.Code).Should().Equal("ProcessoSeletivo.FatoColetadoNaoMaisDeclarado");
        eventos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar sem versão reidratável recusa em vez de pular a versão")]
    public async Task Retificar_VersaoNaoReidratavel_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { Reidratavel = false });

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.Error!.Code.Should().Be("EnvelopeCodec.VersaoDesconhecida");
    }

    [Fact(DisplayName = "Retificação só de datas passa com o catálogo editado num eixo de item coletado que o item não regravou")]
    public async Task Retificar_CatalogoEditadoNoEixoDoItemSemRegravar_Passa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { ClassificacaoNoCatalogo = "SENSIVEL" });

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
    }

    [Fact(DisplayName = "Retificar recopia do catálogo o fato novo e deixa o já coletado com a cópia que a versão publicada congelou")]
    public async Task Retificar_FatosReclassificadosNoCatalogo_RecopiaSoOFatoNovo()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(ReclassificadoNoCatalogoComFatoNovo());

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        cenario.ClassificacaoDoItem(FatoNovo).Should().Be("IDENTIFICADOR");
        cenario.ClassificacaoDoItem(Fato).Should().Be("PESSOAL", "a versão publicada já coletou o fato com essa cópia");
    }

    [Fact(DisplayName = "Retificar recusa o fato já coletado cujo campo ficou incoerente com o catálogo, orientando desfazer a correção")]
    public async Task Retificar_FatoJaColetadoIncoerenteComOCatalogo_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { DominioNoCatalogo = "CATEGORICO" });

        (Result resposta, IEnumerable<object> eventos) = await cenario.RetificarAsync();

        ConferirRecusaPorCampoIncoerenteComOCatalogo(resposta, eventos);
    }

    private static SituacaoDoFatoCitado ReclassificadoNoCatalogoComFatoNovo() =>
        new() { ClassificacaoNoCatalogo = "IDENTIFICADOR", ComItemNovo = true };

    private static void ConferirRecusaPorCampoIncoerenteComOCatalogo(Result resposta, IEnumerable<object> eventos)
    {
        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Should().ContainSingle().Which.Field.Should().MatchRegex(@"^/formularios/\d+/itens/\d+/tipoRenderizacao$");
        resposta.Error!.Message.Should().Contain("desfaça a correção no catálogo");
        eventos.Should().BeEmpty();
    }
}
