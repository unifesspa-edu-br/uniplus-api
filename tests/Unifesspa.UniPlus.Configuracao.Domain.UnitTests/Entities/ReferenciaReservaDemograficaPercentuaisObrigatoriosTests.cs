namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os três percentuais são obrigatórios e zero é um valor válido: o percentual ausente (ou nulo, que o domínio não
/// distingue) é recusado como "obrigatório", acumulado com as demais recusas, em vez de virar zero em silêncio.
/// </summary>
public sealed class ReferenciaReservaDemograficaPercentuaisObrigatoriosTests
{
    private const string BaseLegal = "Lei 12.711/2012, art. 10, III";

    [Theory(DisplayName = "Criar sem um percentual recusa como obrigatório, nomeando só ele")]
    [InlineData("ppiPercentual")]
    [InlineData("quilombolaPercentual")]
    [InlineData("pcdPercentual")]
    public void Criar_SemUmPercentual_RecusaSoEsseCampo(string campo)
    {
        Result<ReferenciaReservaDemografica> resultado = Criar(campo);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be(campo);
        recusa.Error.Code.Should().Be(CodigosDeValidacao.Obrigatorio);
        recusa.Error.Message.Should().StartWith("Informe ");
    }

    [Theory(DisplayName = "Atualizar sem um percentual recusa como obrigatório e não muta a referência")]
    [InlineData("ppiPercentual")]
    [InlineData("quilombolaPercentual")]
    [InlineData("pcdPercentual")]
    public void Atualizar_SemUmPercentual_RecusaENaoMuta(string campo)
    {
        ReferenciaReservaDemografica referencia = Criar(null).Value!;

        Result resultado = Atualizar(referencia, campo);

        resultado.Errors.Should().ContainSingle(e => e.Field == campo && e.Error.Code == CodigosDeValidacao.Obrigatorio);
        referencia.PpiPercentual.Valor.Should().Be(10m);
        referencia.QuilombolaPercentual.Valor.Should().Be(20m);
        referencia.PcdPercentual.Valor.Should().Be(30m);
    }

    [Fact(DisplayName = "Os três percentuais ausentes acumulam com o censo e a base legal ausentes, na ordem do contrato")]
    public void Criar_TudoAusente_AcumulaNaOrdemDoContrato()
    {
        Result<ReferenciaReservaDemografica> resultado = ReferenciaReservaDemografica.Criar(null, null, null, null, null);

        resultado.Errors.Select(e => e.Field).Should().Equal("censoReferencia", "ppiPercentual", "quilombolaPercentual", "pcdPercentual", "baseLegal");
        resultado.Errors.Where(e => e.Field is not ("censoReferencia" or "baseLegal"))
            .Should().OnlyContain(e => e.Error.Code == CodigosDeValidacao.Obrigatorio);
    }

    [Fact(DisplayName = "Percentual ausente e outro fora da faixa acumulam, cada um com o seu código")]
    public void Criar_AusenteEForaDeFaixa_AcumulaComCodigosDistintos()
    {
        Result<ReferenciaReservaDemografica> resultado = ReferenciaReservaDemografica.Criar("2022", null, 150m, 10m, BaseLegal);

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            ("ppiPercentual", CodigosDeValidacao.Obrigatorio),
            ("quilombolaPercentual", ReferenciaReservaDemograficaErrorCodes.PercentualForaDeFaixa));
    }

    [Fact(DisplayName = "Percentual informado como zero é aceito: zero é resposta, ausente é que falta")]
    public void Criar_ComPercentuaisZero_Sucesso()
    {
        ReferenciaReservaDemografica referencia = ReferenciaReservaDemografica.Criar("2022", 0m, 0m, 0m, BaseLegal).Value!;

        referencia.PpiPercentual.Valor.Should().Be(0m);
        referencia.QuilombolaPercentual.Valor.Should().Be(0m);
        referencia.PcdPercentual.Valor.Should().Be(0m);
    }

    [Fact(DisplayName = "ValidarCamposDoPayload recusa o percentual ausente sem instanciar o agregado")]
    public void ValidarCamposDoPayload_PercentualAusente_Recusa()
    {
        Result resultado = ReferenciaReservaDemografica.ValidarCamposDoPayload("2022", 10m, null, 10m, BaseLegal);

        resultado.Errors.Should().ContainSingle(e => e.Field == "quilombolaPercentual" && e.Error.Code == CodigosDeValidacao.Obrigatorio);
    }

    private static Result<ReferenciaReservaDemografica> Criar(string? campoAusente) =>
        ReferenciaReservaDemografica.Criar(
            "2022",
            Percentual("ppiPercentual", campoAusente, 10m),
            Percentual("quilombolaPercentual", campoAusente, 20m),
            Percentual("pcdPercentual", campoAusente, 30m),
            BaseLegal);

    private static Result Atualizar(ReferenciaReservaDemografica referencia, string campoAusente) =>
        referencia.Atualizar(
            "2022",
            Percentual("ppiPercentual", campoAusente, 11m),
            Percentual("quilombolaPercentual", campoAusente, 21m),
            Percentual("pcdPercentual", campoAusente, 31m),
            BaseLegal);

    private static decimal? Percentual(string campo, string? campoAusente, decimal valor) =>
        campo == campoAusente ? null : valor;
}
