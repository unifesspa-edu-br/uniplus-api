namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenarioDoRecursoDaEtapa;

public sealed class DefinirEtapasAncoraDoRecursoTests
{
    [Theory(DisplayName = "Com o tipo fora do catálogo hoje, o recurso novo da etapa é recusado e o que o agregado já tinha segue aceito")]
    [InlineData(false, "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo")]
    [InlineData(true, null)]
    public async Task Handle_RecursoEmTipoSemVersaoVigenteNoCatalogo_RecusaSoONovoEmRelacaoAoAgregado(bool recursoJaNoAgregado, string? codigoEsperado)
    {
        CenarioDoRecursoDaEtapa cenario = new();
        Guid etapaId = await cenario.DefinirComSucessoAsync(Etapa(comRecurso: recursoJaNoAgregado));
        cenario.Publicar();

        Result resultado = await cenario.DefinirAsync(Etapa(etapaId), CatalogoSemOAto());

        resultado.Error?.Code.Should().Be(codigoEsperado, resultado.Error?.Message);
    }

    // A versão publicada tem o recurso. O grafo vem de uma instância à parte, com a identidade da etapa viva, porque a gravação
    // seguinte edita a etapa no lugar.
    [Fact(DisplayName = "O recurso que só a versão publicada tem segue aceito quando o catálogo perde o tipo e o agregado não o tem mais")]
    public async Task Handle_RecursoSoNaVersaoPublicada_CatalogoSemOTipo_Aceita()
    {
        CenarioDoRecursoDaEtapa cenario = new();
        CenarioDoRecursoDaEtapa publicado = new();
        Guid etapaId = await cenario.DefinirComSucessoAsync(Etapa());
        await publicado.DefinirComSucessoAsync(Etapa());
        publicado.IdentificarEtapaComo(etapaId);
        publicado.Processo.Etapas.Single().Recursos.Should().ContainSingle();
        cenario.Publicar(publicado);

        await cenario.DefinirComSucessoAsync(Etapa(etapaId, comRecurso: false));
        cenario.Processo.Etapas.Single().Recursos.Should().BeEmpty();
        publicado.Processo.Etapas.Single().Recursos.Should().ContainSingle();
        await cenario.DefinirComSucessoAsync(Etapa(etapaId), CatalogoSemOAto());
    }

    [Fact(DisplayName = "Em rascunho, o recurso que a etapa já tinha segue aceito quando o catálogo perde o tipo")]
    public async Task Handle_RascunhoComRecursoJaDeclarado_CatalogoSemOTipo_Aceita()
    {
        CenarioDoRecursoDaEtapa cenario = new();
        Guid etapaId = await cenario.DefinirComSucessoAsync(Etapa());

        await cenario.DefinirComSucessoAsync(Etapa(etapaId, "Prova Objetiva — 1ª fase"), CatalogoSemOAto());
    }

    [Theory(DisplayName = "Evento que inicia o prazo do recurso da etapa em ato congelante ou irreversível é recusado, como na fase")]
    [InlineData(true, false, "ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado")]
    [InlineData(false, true, "RecursoDaEtapa.AncoraEmAtoIrreversivel")]
    public async Task Handle_EventoInicialEmAtoQueNaoCabeRecurso_Recusa(
        bool congelaConfiguracao, bool efeitoIrreversivel, string codigoEsperado)
    {
        CenarioDoRecursoDaEtapa cenario = new();

        Result result = await cenario.DefinirAsync(
            Etapa(produtos: SoOPreliminar()), CatalogoComOAto(congelaConfiguracao, efeitoIrreversivel));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(codigoEsperado);
        cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A declaração de instância superior do recurso da etapa chega ao recurso gravado, e não um valor fixo do handler")]
    public async Task Handle_RecursoComInstanciaSuperior_GravaADeclaracao()
    {
        CenarioDoRecursoDaEtapa cenario = new();

        await cenario.DefinirComSucessoAsync(Etapa(admiteInstanciaSuperior: true));

        cenario.Processo.Etapas.Single().Recursos.Single().Parametros.AdmiteInstanciaSuperior.Should().BeTrue();
    }

    /// <summary>
    /// A etapa publica o mesmo ato duas vezes, preliminar e definitivo, e é da publicação preliminar que o prazo do recurso corre.
    /// Resolver a publicação de referência só pelo código do ato devolvia o definitivo sempre que ele viesse primeiro na coleção, e
    /// a etapa era recusada por iniciar o prazo onde não se pode, sem que nada no payload estivesse errado. O definitivo vem primeiro de propósito: é
    /// a ordem que o servidor devolve depois de reler a etapa, e era ela que derrubava a gravação.
    /// </summary>
    [Fact(DisplayName = "A publicação de referência do recurso resolve no produto PRELIMINAR, mesmo com o definitivo antes na coleção")]
    public async Task Handle_PublicacaoDeReferenciaComDefinitivoAntesDoPreliminar_ResolveNoPreliminar()
    {
        CenarioDoRecursoDaEtapa cenario = new();

        await cenario.DefinirComSucessoAsync(Etapa(produtos: ProdutosComODefinitivoPrimeiro()));
    }
}
