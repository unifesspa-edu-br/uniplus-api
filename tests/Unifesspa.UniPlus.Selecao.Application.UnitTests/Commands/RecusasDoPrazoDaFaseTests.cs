namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;

public sealed class RecusasDoPrazoDaFaseTests
{
    private static readonly ParametrosDoPedidoDoRecurso VariasRecusas = ParametrosDoPedidoDoRecurso.Valido with
    {
        PrazoValor = 0m,
        EfeitoSuspensivo = null,
        GuardaDaVaga = "talvez",
        PrazoSegundaInstanciaValor = 1m,
        PrazoSegundaInstanciaUnidade = "HORAS",
    };

    [Fact]
    public async Task Handle_ParametrosInvalidos_AcumulaRecusasCadaUmaNoSeuCampo()
    {
        Result resultado = await DefinirFaseComAsync(VariasRecusas);

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("fases[0].regraRecurso.prazoValor", "RegraRecursoFase.PrazoNaoPositivo"),
            ("fases[0].regraRecurso.efeitoSuspensivo", "RegraRecursoFase.EfeitoSuspensivoNaoDeclarado"),
            ("fases[0].regraRecurso.guardaDaVaga", "RegraRecursoFase.GuardaDaVagaNaoDeclaravel"),
            ("fases[0].regraRecurso.prazoSegundaInstanciaValor", "RegraRecursoFase.PrazoSegundaInstanciaSemInstanciaSuperior"));
    }

    [Theory(DisplayName = "Cada causa de recusa dos parâmetros do recurso chega ao pedido da fase com o código e o campo próprios")]
    [MemberData(nameof(CausasDeRecusaDosParametrosDoRecurso.Nomes), MemberType = typeof(CausasDeRecusaDosParametrosDoRecurso))]
    public async Task Handle_CausaDeRecusa_ChegaComOCodigoEOCampoProprios(string causa)
    {
        (string codigo, string campo, ParametrosDoPedidoDoRecurso pedido) = CausasDeRecusaDosParametrosDoRecurso.Da(causa);

        Result resultado = await DefinirFaseComAsync(pedido);

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ($"fases[0].regraRecurso.{campo}", $"RegraRecursoFase.{codigo}"));
    }

    [Fact(DisplayName = "A instância superior ausente é recusada com o código genérico de campo obrigatório, acumulada com as demais recusas")]
    public async Task Handle_InstanciaSuperiorAusente_RecusaComOCodigoGenericoAcumulada()
    {
        Result resultado = await DefinirFaseComAsync(ParametrosDoPedidoDoRecurso.Valido with
        {
            PrazoValor = 0m,
            AdmiteInstanciaSuperior = null,
            PrazoSegundaInstanciaValor = 1m,
            PrazoSegundaInstanciaUnidade = "HORAS",
        });

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("fases[0].regraRecurso.prazoValor", "RegraRecursoFase.PrazoNaoPositivo"),
            ("fases[0].regraRecurso.admiteInstanciaSuperior", CodigosDeValidacao.Obrigatorio));
    }

    private static Task<Result> DefinirFaseComAsync(ParametrosDoPedidoDoRecurso pedido)
    {
        CenarioDoCronogramaDeFases cenario = CenarioDoCronogramaDeFases.EmRascunho()
            .ComTipoDeAto(TipoAtoDaClassificacao()).ComRegraNoCatalogo(RegraDePrazoDeRecurso());
        FaseCronogramaInput fase = cenario.CadastrarFase(FaseCanonicaRecorrivel()) with
        {
            Produtos = [ProdutoDaClassificacao("PRELIMINAR")],
            RegraRecurso = RecursoDoPedido(pedido),
        };

        return cenario.DefinirFasesAsync(fase);
    }

    private static RegraRecursoFaseInput RecursoDoPedido(ParametrosDoPedidoDoRecurso pedido) => RecursoAncoradoNaClassificacao() with
    {
        PrazoValor = pedido.PrazoValor,
        PrazoUnidade = pedido.PrazoUnidade,
        AdmiteInstanciaSuperior = pedido.AdmiteInstanciaSuperior,
        PrazoSegundaInstanciaValor = pedido.PrazoSegundaInstanciaValor,
        PrazoSegundaInstanciaUnidade = pedido.PrazoSegundaInstanciaUnidade,
        EfeitoSuspensivo = pedido.EfeitoSuspensivo,
        GuardaDaVaga = pedido.GuardaDaVaga,
    };
}
