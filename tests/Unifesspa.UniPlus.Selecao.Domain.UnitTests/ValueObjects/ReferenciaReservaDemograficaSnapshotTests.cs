namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class ReferenciaReservaDemograficaSnapshotTests
{
    [Fact(DisplayName = "Criar com dados válidos tem sucesso")]
    public void Criar_Valida_Sucesso()
    {
        Result<ReferenciaReservaDemograficaSnapshot> resultado = ReferenciaReservaDemograficaSnapshot.Criar(
            Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "Censo 2022 (IBGE)");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.CensoReferencia.Should().Be("2022");
    }

    [Fact(DisplayName = "Criar com censo vazio falha")]
    public void Criar_CensoVazio_Falha()
    {
        Result<ReferenciaReservaDemograficaSnapshot> resultado = ReferenciaReservaDemograficaSnapshot.Criar(
            Guid.CreateVersion7(), "", 79m, 1.5m, 8.5m, "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ReferenciaReservaDemograficaSnapshot.CensoObrigatorio");
    }

    [Theory(DisplayName = "Criar com percentual fora de [0,100] falha")]
    [InlineData(-1, 1.5, 8.5)]
    [InlineData(79, 101, 8.5)]
    [InlineData(79, 1.5, -0.1)]
    public void Criar_PercentualInvalido_Falha(decimal ppi, decimal quilombola, decimal pcd)
    {
        Result<ReferenciaReservaDemograficaSnapshot> resultado = ReferenciaReservaDemograficaSnapshot.Criar(
            Guid.CreateVersion7(), "2022", ppi, quilombola, pcd, "base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ReferenciaReservaDemograficaSnapshot.PercentualInvalido");
    }

    [Fact(DisplayName = "Criar com base legal vazia falha")]
    public void Criar_BaseLegalVazia_Falha()
    {
        Result<ReferenciaReservaDemograficaSnapshot> resultado = ReferenciaReservaDemograficaSnapshot.Criar(
            Guid.CreateVersion7(), "2022", 79m, 1.5m, 8.5m, "");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ReferenciaReservaDemograficaSnapshot.BaseLegalObrigatoria");
    }
}
