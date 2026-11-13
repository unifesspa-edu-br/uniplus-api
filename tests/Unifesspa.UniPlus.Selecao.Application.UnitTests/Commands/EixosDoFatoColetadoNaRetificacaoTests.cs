namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FatoCitadoDeTeste;

/// <summary>
/// Os dois caminhos que congelam uma retificação (o atalho sem sessão e o fechamento da sessão) conferem os
/// eixos do fato já coletado contra o que as versões publicadas congelaram (UNI-REQ-0144): o domínio e o tipo
/// de renderização não mudam sob o fato citado, e o fato novo é copiado do catálogo vivo.
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

    [Fact(DisplayName = "Retificar sem versão reidratável recusa, como a abertura da sessão, em vez de pular a versão")]
    public async Task Retificar_VersaoNaoReidratavel_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { Reidratavel = false });

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.Error!.Code.Should().Be("EnvelopeCodec.VersaoDesconhecida");
        resposta.Error.Message.Should().NotContain("descartada", "sem sessão não há retificação a descartar");
    }

    [Fact(DisplayName = "Retificação só de datas passa com o catálogo editado num eixo de item coletado que o item não regravou")]
    public async Task Retificar_CatalogoEditadoNoEixoDoItemSemRegravar_Passa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { ClassificacaoNoCatalogo = "SENSIVEL" });

        (Result resposta, IEnumerable<object> _) = await cenario.RetificarAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
    }

    [Fact(DisplayName = "Fechar a retificação recusa o fato citado por condição de documento cujo domínio o catálogo trocou desde a versão publicada")]
    public async Task Fechar_DominioDoFatoCitadoTrocado_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { DominioCongelado = "CATEGORICO" });
        cenario.AbrirSessao();

        (Result resposta, IEnumerable<object> eventos) = await cenario.FecharAsync();

        resposta.Error!.Code.Should().Be(CodigoDaRecusaDoEixoAlterado);
        eventos.Should().BeEmpty();
        cenario.Processo.Rascunho.Should().NotBeNull("a recusa não destrói a sessão editorial");
    }

    [Fact(DisplayName = "Fechar a retificação recusa o item cujo eixo chegou à configuração diferente do que a versão publicada congelou")]
    public async Task Fechar_ItemComEixoTrocado_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { ItemPublicadoComoData = true });
        cenario.AbrirSessao();

        (Result resposta, IEnumerable<object> _) = await cenario.FecharAsync();

        resposta.Error!.Code.Should().Be(CodigoDaRecusaDoEixoAlterado);
        resposta.Error.Message.Should().Contain(Fato).And.Contain("tipo de renderização");
    }

    [Fact(DisplayName = "Fechar a retificação sem troca de eixo passa mesmo com o catálogo editado num eixo de item coletado")]
    public async Task Fechar_CatalogoEditadoNoEixoDoItemSemRegravar_Passa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { ClassificacaoNoCatalogo = "SENSIVEL" });
        cenario.AbrirSessao();

        (Result resposta, IEnumerable<object> _) = await cenario.FecharAsync();

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

    [Fact(DisplayName = "Fechar a retificação recopia do catálogo o fato novo e deixa o já coletado com a cópia que a versão publicada congelou")]
    public async Task Fechar_FatosReclassificadosNoCatalogo_RecopiaSoOFatoNovo()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(ReclassificadoNoCatalogoComFatoNovo());
        cenario.AbrirSessao();

        (Result resposta, IEnumerable<object> _) = await cenario.FecharAsync();

        resposta.IsSuccess.Should().BeTrue(resposta.Error?.Message);
        cenario.ClassificacaoDoItem(FatoNovo).Should().Be("IDENTIFICADOR");
        cenario.ClassificacaoDoItem(Fato).Should().Be("PESSOAL", "a versão publicada já coletou o fato com essa cópia");
    }

    [Fact(DisplayName = "Fechar a retificação recusa o fato já coletado cujo campo ficou incoerente com o catálogo, orientando desfazer a correção")]
    public async Task Fechar_FatoJaColetadoIncoerenteComOCatalogo_Recusa()
    {
        CenarioDeRetificacaoPublicada cenario = CenarioDeRetificacaoPublicada.Publicado(new SituacaoDoFatoCitado { DominioNoCatalogo = "CATEGORICO" });
        cenario.AbrirSessao();

        (Result resposta, IEnumerable<object> eventos) = await cenario.FecharAsync();

        ConferirRecusaPorCampoIncoerenteComOCatalogo(resposta, eventos);
    }

    private static SituacaoDoFatoCitado ReclassificadoNoCatalogoComFatoNovo() =>
        new() { ClassificacaoNoCatalogo = "IDENTIFICADOR", ComItemNovo = true };

    private static void ConferirRecusaPorCampoIncoerenteComOCatalogo(Result resposta, IEnumerable<object> eventos)
    {
        resposta.IsFailure.Should().BeTrue();
        resposta.Errors.Should().ContainSingle().Which.Field.Should().MatchRegex(@"^formularios\[[A-Z_]+\]\.itens\[\d+\]\.tipoRenderizacao$");
        resposta.Error!.Message.Should().Contain("desfaça a correção no catálogo");
        eventos.Should().BeEmpty();
    }
}
