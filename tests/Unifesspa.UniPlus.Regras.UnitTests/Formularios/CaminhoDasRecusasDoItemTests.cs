namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A forma do item aponta cada recusa no caminho da parte a que ela pertence: a apresentação do campo ou
/// as regras dele.
/// </summary>
public sealed class CaminhoDasRecusasDoItemTests
{
    private static readonly RegrasDoCampo SemRegras = new(null, Obrigatoriedade.Sempre, []);

    [Fact(DisplayName = "O rótulo e a ajuda recusados saem no caminho da apresentação")]
    public void Conferir_RotuloEAjuda_ApontaNaApresentacao()
    {
        ApresentacaoDoCampo apresentacao = new(" ", TipoDeCampo.SimOuNao, new string('a', FormaDoItem.AjudaMaxLength + 1));

        List<FieldError> erros = FormaDoItem.Conferir("CAMPO", 0, apresentacao, formato: null, SemRegras);

        erros.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("apresentacao.rotulo", ItemFormularioErrorCodes.RotuloObrigatorio),
            ("apresentacao.ajuda", ItemFormularioErrorCodes.AjudaTamanho));
    }

    [Fact(DisplayName = "A exibição e a obrigatoriedade que citam o próprio campo saem no caminho das regras")]
    public void Conferir_RegrasAutorreferentes_ApontamNasRegras()
    {
        RegrasDoCampo regras = new(Citando("CAMPO"), Obrigatoriedade.Quando(Citando("CAMPO")), []);

        List<FieldError> erros = FormaDoItem.Conferir("CAMPO", 0, new ApresentacaoDoCampo("Campo", TipoDeCampo.SimOuNao), formato: null, regras);

        erros.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("regras.exibicao", ItemFormularioErrorCodes.RegraAutorreferente),
            ("regras.obrigatoriedade.predicado", ItemFormularioErrorCodes.RegraAutorreferente));
    }

    [Fact(DisplayName = "A restrição fora do tipo e o impedimento que não cabe saem no caminho das regras")]
    public void Conferir_RestricaoEImpedimento_ApontamNasRegras()
    {
        RegrasDoCampo regras = new(null, Obrigatoriedade.Nunca, [new FaixaNumerica(1, 10)], new Impedimento(Citando("CAMPO"), "Não pode."));

        List<FieldError> erros = FormaDoItem.Conferir("CAMPO", 0, new ApresentacaoDoCampo("Campo", TipoDeCampo.Texto), formato: "LIVRE", regras);

        erros.Select(static e => e.Field).Should().Equal("regras.restricoes[0]", "regras.impedimento");
    }

    [Fact]
    public void ValidarFormaBasica_CodigoComEspacosAcimaDoLimite_RecusaTamanho()
    {
        List<FieldError> erros = FormaDoItem.ValidarFormaBasica(
            " AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA ", 0, "Campo");

        erros.Select(static e => (e.Field, e.Error.Code)).Should().ContainSingle()
            .Which.Should().Be(("fatoCodigo", ItemFormularioErrorCodes.FatoCodigoTamanho));
    }

    private static PredicadoDnf Citando(string fato) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;
}
