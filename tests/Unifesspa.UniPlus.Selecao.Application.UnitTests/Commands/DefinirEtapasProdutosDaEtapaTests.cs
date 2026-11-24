namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class DefinirEtapasProdutosDaEtapaTests
{
    private const string Resultado = "RESULTADO_CLASSIFICACAO";

    /// <summary>
    /// A fase confere o ato contra o catálogo de Publicações; a etapa não conferia. O código
    /// declarado aqui vira o nome do produto no cronograma público — um código com erro de
    /// digitação só apareceria depois de o certame estar no Diário Oficial.
    /// </summary>
    [Fact(DisplayName = "Produto de etapa com ato fora do catálogo é recusado, como na fase")]
    public async Task Handle_ProdutoComAtoForaDoCatalogo_Recusa()
    {
        CenarioDaDefinicao cenario = CenarioDaDefinicao.Novo();

        // Catálogo que não conhece código algum: é o erro de digitação do payload chegando
        // sem correspondência, não uma indisponibilidade do módulo de Publicações.
        Result result = await cenario.DefinirAsync([EtapaDeProvaObjetiva(new ProdutoDaEtapaInput("RESULTADO_PRELMINAR", null))], CatalogoSemAto());

        await cenario.ConferirRecusaSemGravarAsync(result, "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo");
    }

    /// <summary>
    /// Papel preliminar ou definitivo é o par que a janela recursal ancora, e só resultado o
    /// admite. A fase recusa o mesmo.
    /// </summary>
    [Fact(DisplayName = "Produto de etapa com papel sobre ato que não é resultado é recusado")]
    public async Task Handle_PapelEmAtoQueNaoEhResultado_Recusa()
    {
        CenarioDaDefinicao cenario = CenarioDaDefinicao.Novo();

        // O ato existe e está vigente — o que ele não é, é resultado. A recusa tem de vir da
        // segunda pergunta, não da primeira.
        Result result = await cenario.DefinirAsync(
            [EtapaDeProvaObjetiva(new ProdutoDaEtapaInput("EDITAL_ABERTURA", "PRELIMINAR"))], CatalogoComOEditalDeAbertura());

        await cenario.ConferirRecusaSemGravarAsync(result, "ProdutoDaEtapa.PapelEmAtoQueNaoEhResultado");
    }

    /// <summary>
    /// O mesmo tipo de ato aparece muitas vezes num cronograma real — cada etapa publica
    /// preliminar e definitivo, e etapas diferentes publicam o mesmo gabarito. Reler o catálogo
    /// por item multiplica idas ao banco enquanto o <c>SELECT ... FOR UPDATE</c> do certame está
    /// na mão, serializando quem mais estiver editando o mesmo certame por todo esse tempo.
    /// </summary>
    [Fact(DisplayName = "O catálogo é consultado uma vez por código, não uma vez por produto declarado")]
    public async Task Handle_MesmoAtoEmVariosProdutos_ConsultaOCatalogoUmaVezPorCodigo()
    {
        CenarioDaDefinicao cenario = CenarioDaDefinicao.Novo();
        ITipoAtoPublicadoReader catalogo = CatalogoComOResultado();

        // Quatro produtos, dois por etapa, todos do MESMO código: uma consulta basta.
        Result result = await cenario.DefinirAsync(
            [EtapaDeProvaObjetiva(PreliminarEDefinitivo), EtapaDeRedacao(PreliminarEDefinitivo)], catalogo);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        await catalogo.Received(1).ObterVigenteAsync(Resultado, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo classificado como resultado declarado sem papel pela etapa é recusado com ProcessoSeletivo.ResultadoDeclaradoSemPapel")]
    public async Task Handle_ResultadoSemPapel_Recusa()
    {
        CenarioDaDefinicao cenario = CenarioDaDefinicao.Novo();

        Result result = await cenario.DefinirAsync([EtapaDeProvaObjetiva(new ProdutoDaEtapaInput(Resultado, null))], CatalogoComOResultado());

        await cenario.ConferirRecusaSemGravarAsync(result, "ProcessoSeletivo.ResultadoDeclaradoSemPapel");
    }

    /// <summary>
    /// O endpoint substitui a coleção inteira, e o cliente devolve o que leu. Se o catálogo
    /// aposentar um ato que alguma etapa já declarava, reconferir todo produto a cada gravação
    /// trancaria a edição de qualquer etapa do certame — inclusive de quem só quer corrigir um
    /// nome —, e a única saída seria apagar um produto que o operador não declarou hoje. É a
    /// mesma distinção que o tipo da etapa recebe: confere-se o vínculo novo, não o que já
    /// estava lá.
    /// </summary>
    [Fact(DisplayName = "Ato aposentado no catálogo não tranca a gravação da etapa que já o declarava")]
    public async Task Handle_AtoAposentadoJaDeclarado_NaoTrancaAColecao()
    {
        CenarioDaDefinicao cenario = CenarioDaDefinicao.Novo();
        (await cenario.DefinirAsync([EtapaDeProvaObjetiva(PreliminarEDefinitivo)], CatalogoComOResultado())).IsSuccess.Should().BeTrue();
        Guid etapaId = cenario.Processo.Etapas.Single().Id;

        // O catálogo aposenta o ato: ObterVigenteAsync deixa de resolvê-lo.
        Result resultado = await cenario.DefinirAsync(
            [EtapaDeProvaObjetiva(PreliminarEDefinitivo) with { Nome = "Prova Objetiva — 1ª fase", Id = etapaId }], CatalogoSemAto());

        resultado.IsSuccess.Should().BeTrue(
            $"o produto já estava declarado e não é o que esta gravação muda — veio '{resultado.Error?.Code}'");
        cenario.Processo.Etapas.Single().Nome.Should().Be("Prova Objetiva — 1ª fase");
    }

    private static ProdutoDaEtapaInput[] PreliminarEDefinitivo => [new(Resultado, "PRELIMINAR"), new(Resultado, "DEFINITIVO")];

    private static EtapaProcessoInput EtapaDeProvaObjetiva(params ProdutoDaEtapaInput[] produtos) => new(
        "Prova Objetiva", CaraterEtapa.Classificatoria.Codigo(), TipoProvaObjetivaOrigemId, 1m, null, 1,
        Produtos: produtos, Bancas: [], Recursos: []);

    private static EtapaProcessoInput EtapaDeRedacao(params ProdutoDaEtapaInput[] produtos) => new(
        "Redação", CaraterEtapa.Classificatoria.Codigo(), TipoRedacaoOrigemId, 1m, null, 2,
        Produtos: produtos, Bancas: [], Recursos: []);

    private static ITipoAtoPublicadoReader CatalogoSemAto()
    {
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);
        return catalogo;
    }

    private static ITipoAtoPublicadoReader CatalogoComOResultado()
    {
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync(Resultado, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado(Resultado));
        return catalogo;
    }

    private static ITipoAtoPublicadoReader CatalogoComOEditalDeAbertura()
    {
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync("EDITAL_ABERTURA", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                "EDITAL_ABERTURA", "Edital de abertura", CongelaConfiguracao: true, UnicoPorObjeto: true,
                EfeitoIrreversivel: false, EhResultado: false));
        return catalogo;
    }

    /// <summary>O certame sem etapas e os dublês de repositório e de unidade de trabalho que a definição das etapas usa.</summary>
    private sealed record CenarioDaDefinicao(ProcessoSeletivo Processo, IProcessoSeletivoRepository Repositorio, IUnitOfWork UnitOfWork)
    {
        internal static CenarioDaDefinicao Novo()
        {
            ProcessoSeletivo processo = ProcessoSemEtapas();
            IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
            repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
            return new(processo, repositorio, Substitute.For<IUnitOfWork>());
        }

        internal Task<Result> DefinirAsync(IReadOnlyList<EtapaProcessoInput> etapas, ITipoAtoPublicadoReader catalogo) =>
            Executar(new DefinirEtapasCommand(Processo.Id, etapas), Repositorio, UnitOfWork, cadastros: new(Atos: catalogo));

        // A etapa já foi construída quando a recusa acontece. Não basta o handler deixar de chamar
        // SalvarAlteracoesAsync: o SaveChanges automático do Wolverine roda DEPOIS do retorno e
        // persistiria a mutação, e só o descarte do rastreamento a impede.
        internal async Task ConferirRecusaSemGravarAsync(Result result, string codigo)
        {
            result.IsFailure.Should().BeTrue();
            result.Error!.Code.Should().Be(codigo);
            await UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
            UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        }
    }
}
