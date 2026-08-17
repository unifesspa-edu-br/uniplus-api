namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
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
            "COR_RACA", 0, "", TipoRenderizacao.SelecaoUnica, obrigatorio: false, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.RotuloObrigatorio);
    }

    [Fact(DisplayName = "Rótulo só de espaço é recusado")]
    public void Criar_RotuloSoEspaco_RetornaFalha()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, "   ", TipoRenderizacao.SelecaoUnica, obrigatorio: false, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.RotuloObrigatorio);
    }

    [Fact(DisplayName = "TipoRenderizacao.Nenhuma (sentinela) é recusado")]
    public void Criar_TipoRenderizacaoNenhuma_RetornaFalha()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, "Cor ou raça", TipoRenderizacao.Nenhuma, obrigatorio: false, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.TipoRenderizacaoObrigatorio);
    }

    [Fact(DisplayName = "Rótulo com espaços nas bordas é aparado")]
    public void Criar_RotuloComEspacos_EAparado()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, "  Cor ou raça  ", TipoRenderizacao.SelecaoUnica, obrigatorio: false, null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Rotulo.Should().Be("Cor ou raça");
    }

    [Fact(DisplayName = "Fato válido com apresentação completa é aceito")]
    public void Criar_ApresentacaoCompleta_Aceita()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "BAIXA_RENDA", 0, "Baixa renda", TipoRenderizacao.Booleano, obrigatorio: true, null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Rotulo.Should().Be("Baixa renda");
        resultado.Value!.TipoRenderizacao.Should().Be(TipoRenderizacao.Booleano);
        resultado.Value!.Obrigatorio.Should().BeTrue();
    }

    [Fact(DisplayName = "Código do fato acima do limite é recusado")]
    public void Criar_FatoCodigoMuitoLongo_Recusa()
    {
        string codigoLongo = new('A', FatoColetado.FatoCodigoMaxLength + 1);

        Result<FatoColetado> resultado = FatoColetado.Criar(
            codigoLongo, 0, "Rótulo", TipoRenderizacao.SelecaoUnica, obrigatorio: false, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.FatoCodigoTamanho);
    }

    [Fact(DisplayName = "Rótulo acima do limite é recusado")]
    public void Criar_RotuloMuitoLongo_Recusa()
    {
        string rotuloLongo = new('a', FatoColetado.RotuloMaxLength + 1);

        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", 0, rotuloLongo, TipoRenderizacao.SelecaoUnica, obrigatorio: false, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.RotuloTamanho);
    }

    [Fact(DisplayName = "ADR-0125: violações independentes acumulam num único lote")]
    public void Criar_OrdemNegativaERotuloVazioETipoRenderizacaoAusente_AcumulaAsTresViolacoes()
    {
        Result<FatoColetado> resultado = FatoColetado.Criar(
            "COR_RACA", -1, "", TipoRenderizacao.Nenhuma, obrigatorio: false, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            FatoColetadoErrorCodes.OrdemInvalida,
            FatoColetadoErrorCodes.RotuloObrigatorio,
            FatoColetadoErrorCodes.TipoRenderizacaoObrigatorio,
        ]);
    }
}
