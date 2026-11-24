namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>
/// Os parâmetros do recurso da etapa: cada causa de recusa chega ao pedido com o código, o campo e a posição do recurso.
/// </summary>
public sealed class RecusasDosParametrosDoRecursoDaEtapaTests
{
    [Theory(DisplayName = "Cada causa de recusa dos parâmetros do recurso chega ao pedido da etapa com o código e o campo próprios")]
    [MemberData(nameof(CausasDeRecusaDosParametrosDoRecurso.Nomes), MemberType = typeof(CausasDeRecusaDosParametrosDoRecurso))]
    public async Task Handle_CausaDeRecusa_ChegaComOCodigoEOCampoProprios(string causa)
    {
        (string codigo, string campo, ParametrosDoPedidoDoRecurso pedido) = CausasDeRecusaDosParametrosDoRecurso.Da(causa);

        Result resultado = await DefinirAsync(Recurso(pedido));

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ($"etapas[0].recursos[0].{campo}", $"RecursoDaEtapa.{codigo}"));
    }

    [Fact(DisplayName = "A recusa de um recurso que não é o primeiro da etapa leva a posição dele no campo")]
    public async Task Handle_SegundoRecursoRecusado_LevaAPosicaoNoCampo()
    {
        Result resultado = await DefinirAsync(
            Recurso(ParametrosDoPedidoDoRecurso.Valido),
            Recurso(ParametrosDoPedidoDoRecurso.Valido with { EfeitoSuspensivo = null }, EventoInicialDoPrazo.CienciaIndividual));

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("etapas[0].recursos[1].efeitoSuspensivo", "RecursoDaEtapa.EfeitoSuspensivoNaoDeclarado"));
    }

    [Fact(DisplayName = "A declaração válida, com a guarda da vaga omitida, é aceita e fica com a guarda até o resultado definitivo")]
    public async Task Handle_GuardaOmitida_GuardaAteOResultadoDefinitivo()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();

        Result resultado = await DefinirAsync(processo, Recurso(ParametrosDoPedidoDoRecurso.Valido));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.Etapas.Single().Recursos.Single().Parametros.GuardaDaVaga.Should().Be(GuardaDaVaga.AteOResultadoDefinitivo);
    }

    private static RecursoDaEtapaInput Recurso(ParametrosDoPedidoDoRecurso pedido, EventoInicialDoPrazo eventoInicialDoPrazo = EventoInicialDoPrazo.PublicacaoDoAto) => new(
        eventoInicialDoPrazo.Codigo(), "RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO", "v1",
        pedido.PrazoValor, pedido.PrazoUnidade, "RESULTADO_CLASSIFICACAO", pedido.AdmiteInstanciaSuperior,
        pedido.PrazoSegundaInstanciaValor, pedido.PrazoSegundaInstanciaUnidade, pedido.EfeitoSuspensivo, pedido.GuardaDaVaga);

    private static Task<Result> DefinirAsync(params RecursoDaEtapaInput[] recursos) => DefinirAsync(ProcessoSemEtapas(), recursos);

    private static Task<Result> DefinirAsync(ProcessoSeletivo processo, params RecursoDaEtapaInput[] recursos) => Executar(
        new DefinirEtapasCommand(processo.Id, [EtapaComRecursos(recursos)]),
        RepositorioCom(processo),
        Substitute.For<IUnitOfWork>(),
        cadastros: new(Regras: CatalogoComARegraDeRecurso(), Atos: CatalogoComOAtoDeResultado()));

    private static EtapaProcessoInput EtapaComRecursos(RecursoDaEtapaInput[] recursos) => new(
        "Prova Objetiva", CaraterEtapa.Classificatoria.Codigo(), TipoProvaObjetivaOrigemId, 1m, null, 1,
        Produtos:
        [
            new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "PRELIMINAR"),
            new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "DEFINITIVO"),
        ],
        Bancas: [],
        Recursos: recursos);

    private static IRegraCatalogoReader CatalogoComARegraDeRecurso()
    {
        IRegraCatalogoReader regras = Substitute.For<IRegraCatalogoReader>();
        regras.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(RegraCatalogo.Criar(
            "RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO", "v1", TipoRegra.RegraPrazoRecurso,
            JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999, art. 59"));
        return regras;
    }

    private static ITipoAtoPublicadoReader CatalogoComOAtoDeResultado()
    {
        ITipoAtoPublicadoReader atos = Substitute.For<ITipoAtoPublicadoReader>();
        atos.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(new TipoAtoPublicadoView(
            "RESULTADO_CLASSIFICACAO", "Resultado da classificação",
            CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: true));
        return atos;
    }
}
