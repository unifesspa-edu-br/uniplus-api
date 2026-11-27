namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Pagination;

using AwesomeAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.WebUtilities;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio;
using Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Apoio.ControllersDeTeste;

public sealed class FiltrosDasListagensTests : IDisposable
{
    private readonly AcoesDoMvcDeTeste _mvc = new(typeof(CursorDeTesteController), typeof(ForaDoContratoDeTesteController));

    [Fact(DisplayName = "Os filtros declarados saem na ordem da declaração, com o nome declarado e sem os da paginação")]
    public void Da_AcaoComFiltros_DevolveOsDeclaradosNaOrdem()
    {
        FiltrosDeclaradosDaListagem filtros = new FiltrosDasListagens(_mvc.Descricoes)
            .Da(_mvc.Acao(typeof(CursorDeTesteController), nameof(CursorDeTesteController.ListarComFiltros)));

        IReadOnlyList<string> partes = filtros.PartesDoLink(
            Consulta("tipoProcesso=SISU&TIPO=2&Q=ok&busca=b&limit=5&cursor=x"), comprimentoMaximoDoValor: 256);

        partes.Should().Equal("q=ok", "busca=b", "tipo=2", "tipoProcesso=SISU");
    }

    [Fact(DisplayName = "Só os filtros em array aceitam vários valores")]
    public void Da_FiltroEmArray_AceitaVariosValoresESimplesNao()
    {
        FiltrosDeclaradosDaListagem filtros = new FiltrosDasListagens(_mvc.Descricoes)
            .Da(_mvc.Acao(typeof(CursorDeTesteController), nameof(CursorDeTesteController.ListarComFiltros)));

        filtros.PartesDoLink(Consulta("tipo=1&tipo=2&tipoProcesso=a&tipoProcesso=b"), 256)
            .Should().Equal("tipo=1", "tipo=2", "tipoProcesso=a");
    }

    [Fact(DisplayName = "A ação sem filtros declarados não repete nenhum parâmetro")]
    public void Da_AcaoSemFiltros_NaoRepeteNada()
    {
        FiltrosDeclaradosDaListagem filtros = new FiltrosDasListagens(_mvc.Descricoes)
            .Da(_mvc.Acao(typeof(CursorDeTesteController), nameof(CursorDeTesteController.PublicEditais)));

        filtros.PartesDoLink(Consulta("q=ok&qualquer=x"), 256).Should().BeEmpty();
    }

    [Fact(DisplayName = "A declaração de uma ação é calculada uma vez")]
    public void Da_MesmaAcaoDuasVezes_DevolveAMesmaDeclaracao()
    {
        FiltrosDasListagens listagens = new(_mvc.Descricoes);
        ControllerActionDescriptor acao = _mvc.Acao(typeof(CursorDeTesteController), nameof(CursorDeTesteController.ListarComFiltros));

        listagens.Da(acao).Should().BeSameAs(listagens.Da(acao));
    }

    [Fact(DisplayName = "Ação paginada fora do contrato publicado é recusada na montagem")]
    public void Da_AcaoForaDoContrato_Lanca()
    {
        FiltrosDasListagens listagens = new(_mvc.Descricoes);
        ControllerActionDescriptor acao = _mvc.Acao(typeof(ForaDoContratoDeTesteController), nameof(ForaDoContratoDeTesteController.Listar));

        Action buscar = () => listagens.Da(acao);

        buscar.Should().Throw<InvalidOperationException>().WithMessage("*precisa constar do contrato publicado*");
    }

    public void Dispose() => _mvc.Dispose();

    private static QueryCollection Consulta(string consulta) =>
        new(QueryHelpers.ParseQuery("?" + consulta));
}
