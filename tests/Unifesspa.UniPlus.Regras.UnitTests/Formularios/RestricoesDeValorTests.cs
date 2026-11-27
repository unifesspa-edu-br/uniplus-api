namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

public sealed class RestricoesDeValorTests
{
    private static readonly Result<LimiteDeData> LimiteLido = Result<LimiteDeData>.Success(LimiteDeData.Fixo(new DateOnly(2026, 1, 1)));
    private static readonly Result<LimiteDeData> LimiteRecusado = RestricoesDeValor.Limite("HOJE", null, null, null);

    private static (PredicadoDnf? Quando, IReadOnlyCollection<string> Valores) Grupo(params string[] valores) => (null, valores);

    [Fact(DisplayName = "Opções com o primeiro e o terceiro grupos vazios recusam cada um no seu campo")]
    public void Opcoes_GruposVazios_RecusaUmaVezPorGrupo()
    {
        Result<RestricaoValor> resultado = RestricoesDeValor.Opcoes([Grupo(), Grupo("A"), Grupo()]);

        resultado.Errors.Select(static e => e.Field).Should().Equal("entradas[0].valores", "entradas[2].valores");
        resultado.Errors.Should().OnlyContain(static e => e.Error.Code == RestricaoValorErrorCodes.OpcoesVazias);
    }

    [Fact(DisplayName = "Grupo sem a lista de valores é grupo sem valores, uma recusa por grupo e sem exceção")]
    public void Opcoes_GrupoComValoresNulos_RecusaComoGrupoSemValores()
    {
        Result<RestricaoValor> resultado = RestricoesDeValor.Opcoes([Grupo(), (null, null!)]);

        resultado.Errors.Select(static e => e.Field).Should().Equal("entradas[0].valores", "entradas[1].valores");
    }

    [Fact(DisplayName = "Opções sem nenhum grupo são uma recusa sem campo")]
    public void Opcoes_ListaVazia_RecusaSemCampo()
    {
        Result<RestricaoValor> resultado = RestricoesDeValor.Opcoes([]);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().BeNull();
    }

    [Fact(DisplayName = "Dois limites recusados voltam em duas recusas, o mínimo antes do máximo")]
    public void FaixaDeDataLida_DoisLimitesRecusados_InformaOsDoisNaOrdem()
    {
        Result<RestricaoValor> resultado = RestricoesDeValor.FaixaDeDataLida(LimiteRecusado, LimiteRecusado);

        resultado.Errors.Select(static e => e.Field).Should().Equal("dataMinima", "dataMaxima");
    }

    [Fact(DisplayName = "Só o limite máximo recusado volta no campo dataMaxima")]
    public void FaixaDeDataLida_SoOMaximoRecusado_InformaDataMaxima()
    {
        Result<RestricaoValor> resultado = RestricoesDeValor.FaixaDeDataLida(LimiteLido, LimiteRecusado);

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("dataMaxima");
    }

    [Fact(DisplayName = "Os dois limites ausentes são limites incoerentes, sem campo")]
    public void FaixaDeDataLida_DoisAusentes_RecusaLimitesIncoerentesSemCampo()
    {
        Result<RestricaoValor> resultado = RestricoesDeValor.FaixaDeDataLida(null, null);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static e =>
            e.Field == null && e.Error.Code == RestricaoValorErrorCodes.LimitesIncoerentes);
    }

    [Fact(DisplayName = "Com um limite recusado a coerência entre os limites não é conferida")]
    public void FaixaDeDataLida_UmLimiteRecusado_NaoConfereACoerencia()
    {
        Result<LimiteDeData> posterior = Result<LimiteDeData>.Success(LimiteDeData.Fixo(new DateOnly(2030, 1, 1)));

        Result<RestricaoValor> resultado = RestricoesDeValor.FaixaDeDataLida(posterior, LimiteRecusado);

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(RestricaoValorErrorCodes.LimiteDeDataInvalido);
    }

    [Fact(DisplayName = "Os dois limites lidos formam a faixa")]
    public void FaixaDeDataLida_LimitesLidos_FormaAFaixa()
    {
        RestricoesDeValor.FaixaDeDataLida(LimiteLido, null).IsSuccess.Should().BeTrue();
    }
}
