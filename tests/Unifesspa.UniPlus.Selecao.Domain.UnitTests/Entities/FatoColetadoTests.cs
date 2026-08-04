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
}
