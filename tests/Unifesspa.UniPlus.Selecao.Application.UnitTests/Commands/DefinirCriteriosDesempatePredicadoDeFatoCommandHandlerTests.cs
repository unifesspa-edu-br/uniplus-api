namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDosCriteriosDeDesempate;

/// <summary>
/// O critério de desempate por predicado de fato: o fato vem do vocabulário fechado do catálogo de fatos e o
/// predicado precisa do fato, do operador e do valor.
/// </summary>
public sealed class DefinirCriteriosDesempatePredicadoDeFatoCommandHandlerTests
{
    private static CriterioDesempateInput PredicadoDoFato(string? fato, string? operador, string? valor) =>
        new(1, CriterioDesempateCodigo.PredicadoFato, "v1", null, null, fato, operador, valor);

    [Fact(DisplayName = "Predicado sem fato e sem valor recusa nos dois campos")]
    public async Task Handle_PredicadoSemFatoESemValor_RecusaNosDoisCampos()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(CriterioDesempateCodigo.PredicadoFato);

        Result result = await cenario.DefinirAsync(Comando(processo.Id, PredicadoDoFato(null, "IGUAL", null)));

        result.Errors.Select(static e => e.Field).Should().Equal("criterios[0].fato", "criterios[0].valor");
    }

    [Fact(DisplayName = "Handle com DESEMPATE-PREDICADO-FATO sobre fato do vocabulário fechado resolve e persiste")]
    public async Task Handle_PredicadoFato_FatoConhecido_Persiste()
    {
        ProcessoSeletivo processo = NovoProcesso("PSE 2026", TiposDeProcessoDeTeste.PSECampo);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(CriterioDesempateCodigo.PredicadoFato);

        Result result = await cenario.DefinirAsync(Comando(processo.Id, PredicadoDoFato("PROFESSOR_RURAL", "IGUAL", "true")));

        result.IsSuccess.Should().BeTrue();
        ParametrosDesempatePredicadoFato parametros = (ParametrosDesempatePredicadoFato)processo.CriteriosDesempate.Single().Parametros;
        parametros.Condicao.Fato.Should().Be("PROFESSOR_RURAL");
        parametros.Condicao.Valor.GetBoolean().Should().BeTrue();
    }

    [Fact(DisplayName = "Handle com DESEMPATE-PREDICADO-FATO sobre fato fora do vocabulário fechado recusa")]
    public async Task Handle_PredicadoFato_FatoDesconhecido_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("PSE 2026", TiposDeProcessoDeTeste.PSECampo);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(CriterioDesempateCodigo.PredicadoFato);

        Result result = await cenario.DefinirAsync(Comando(processo.Id, PredicadoDoFato("FATO_INEXISTENTE", "IGUAL", "S")));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("PredicadoDnf.FatoDesconhecido");
    }

    [Fact(DisplayName = "Handle com DESEMPATE-PREDICADO-FATO incompleto recusa")]
    public async Task Handle_PredicadoFatoIncompleto_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso("PSE 2026", TiposDeProcessoDeTeste.PSECampo);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(CriterioDesempateCodigo.PredicadoFato);

        Result result = await cenario.DefinirAsync(Comando(processo.Id, PredicadoDoFato("PROFESSOR_RURAL", null, null)));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CriterioDesempate.PredicadoFatoIncompleto");
    }
}
