namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Story #559 — a apresentação do campo no formulário de inscrição (Rotulo/TipoRenderizacao/
/// Obrigatorio) na factory de <see cref="FatoColetado"/>. A coerência entre TipoRenderizacao e o
/// Dominio do fato no catálogo é validada na Application (cross-módulo) — coberta em
/// <c>DefinirFatosColetadosCommandHandlerTests</c>, não aqui.
/// </summary>
public sealed class FatoColetadoTests
{
    [Fact(DisplayName = "Rótulo vazio é recusado")]
    public void Criar_RotuloVazio_RetornaFalha()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, "", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.RotuloObrigatorio);
    }

    [Fact(DisplayName = "Rótulo só de espaço é recusado")]
    public void Criar_RotuloSoEspaco_RetornaFalha()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, "   ", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.RotuloObrigatorio);
    }

    [Fact(DisplayName = "TipoRenderizacao.Nenhuma (sentinela) é recusado")]
    public void Criar_TipoRenderizacaoNenhuma_RetornaFalha()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, "Cor ou raça", TipoRenderizacao.Nenhuma, Obrigatoriedade.Nunca, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.TipoRenderizacaoObrigatorio);
    }

    [Theory(DisplayName = "O formato existe se, e só se, o campo é de texto")]
    [InlineData(TipoRenderizacao.Texto, null, false)]
    [InlineData(TipoRenderizacao.Texto, "CPF", true)]
    [InlineData(TipoRenderizacao.Booleano, "CPF", false)]
    [InlineData(TipoRenderizacao.Booleano, null, true)]
    public void Criar_FormatoSoEmCampoDeTexto(TipoRenderizacao tipo, string? formato, bool aceito)
    {
        Result<FatoColetado> resultado = FatoColetado.Criar("NOME_SOCIAL", 0, "Nome social", tipo, Obrigatoriedade.Nunca, null, formato: formato);

        if (aceito)
        {
            resultado.Value!.Formato.Should().Be(formato);
        }
        else
        {
            resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.FormatoIncoerente);
        }
    }

    [Fact(DisplayName = "Obrigatoriedade que cita o próprio fato é recusada")]
    public void Criar_ObrigatoriedadeAutorreferente_Recusa()
    {
        Obrigatoriedade quando = Obrigatoriedade.Quando(PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar("PCD", Operador.Igual, System.Text.Json.JsonSerializer.SerializeToElement(true)).Value!)]).Value!);

        FatoColetado.Criar("PCD", 0, "Pessoa com deficiência", TipoRenderizacao.Booleano, quando, null)
            .Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoAutorreferente);
    }

    [Fact(DisplayName = "Ajuda acima do limite é recusada no campo")]
    public void Criar_AjudaLonga_Recusa() =>
        FatoColetado.Criar("PCD", 0, "PCD", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, null, ajuda: new string('a', FatoColetado.AjudaMaxLength + 1))
            .Errors.Should().ContainSingle().Which.Field.Should().Be("ajuda");

    [Fact(DisplayName = "Rótulo com espaços nas bordas é aparado")]
    public void Criar_RotuloComEspacos_EAparado()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, "  Cor ou raça  ", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Rotulo.Should().Be("Cor ou raça");
    }

    [Fact(DisplayName = "Fato válido com apresentação completa é aceito")]
    public void Criar_ApresentacaoCompleta_Aceita()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "BAIXA_RENDA", 0, "Baixa renda", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Rotulo.Should().Be("Baixa renda");
        resultado.Value!.TipoRenderizacao.Should().Be(TipoRenderizacao.Booleano);
        resultado.Value!.Obrigatoriedade.Should().Be(Obrigatoriedade.Sempre);
    }

    [Fact(DisplayName = "Código do fato acima do limite é recusado")]
    public void Criar_FatoCodigoMuitoLongo_Recusa()
    {
        string codigoLongo = new('A', FatoColetado.FatoCodigoMaxLength + 1);

        Result<FatoColetado> resultado = FatoColetado.Criar(
            codigoLongo, 0, "Rótulo", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.FatoCodigoTamanho);
    }

    [Fact(DisplayName = "Rótulo acima do limite é recusado")]
    public void Criar_RotuloMuitoLongo_Recusa()
    {
        string rotuloLongo = new('a', FatoColetado.RotuloMaxLength + 1);

        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, rotuloLongo, TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Nunca, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.RotuloTamanho);
    }

    [Fact(DisplayName = "ADR-0125: violações independentes acumulam num único lote")]
    public void Criar_OrdemNegativaERotuloVazioETipoRenderizacaoAusente_AcumulaAsTresViolacoes()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", -1, "", TipoRenderizacao.Nenhuma, Obrigatoriedade.Nunca, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            FatoColetadoErrorCodes.OrdemInvalida,
            FatoColetadoErrorCodes.RotuloObrigatorio,
            FatoColetadoErrorCodes.TipoRenderizacaoObrigatorio,
        ]);
    }
}
