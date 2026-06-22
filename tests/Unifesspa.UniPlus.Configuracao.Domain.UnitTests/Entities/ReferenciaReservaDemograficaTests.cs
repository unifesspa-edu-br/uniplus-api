namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ReferenciaReservaDemograficaTests
{
    private const string BaseLegal = "Lei 12.711/2012, art. 10, III";

    [Fact(DisplayName = "Criar com dados válidos preenche os percentuais e fica ativa")]
    public void Criar_DadosValidos_Preenche()
    {
        Result<ReferenciaReservaDemografica> resultado =
            ReferenciaReservaDemografica.Criar("2022", 78.50m, 1.20m, 8.40m, BaseLegal);

        resultado.IsSuccess.Should().BeTrue();
        ReferenciaReservaDemografica referencia = resultado.Value!;
        referencia.Id.Should().NotBe(Guid.Empty);
        referencia.CensoReferencia.Should().Be("2022");
        referencia.PpiPercentual.Valor.Should().Be(78.50m);
        referencia.QuilombolaPercentual.Valor.Should().Be(1.20m);
        referencia.PcdPercentual.Valor.Should().Be(8.40m);
        referencia.BaseLegal.Should().Be(BaseLegal);
        referencia.IsDeleted.Should().BeFalse();
    }

    [Theory(DisplayName = "Criar com Censo ausente ou em branco falha")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_SemCenso_Falha(string censo)
    {
        Result<ReferenciaReservaDemografica> resultado =
            ReferenciaReservaDemografica.Criar(censo, 10m, 10m, 10m, BaseLegal);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ReferenciaReservaDemograficaErrorCodes.CensoObrigatorio);
    }

    [Fact(DisplayName = "Criar sem base legal falha")]
    public void Criar_SemBaseLegal_Falha()
    {
        Result<ReferenciaReservaDemografica> resultado =
            ReferenciaReservaDemografica.Criar("2022", 10m, 10m, 10m, "   ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ReferenciaReservaDemograficaErrorCodes.BaseLegalObrigatoria);
    }

    [Theory(DisplayName = "Criar com percentual fora do intervalo falha (um por percentual e limite)")]
    [InlineData(-0.01, 10, 10)]
    [InlineData(100.01, 10, 10)]
    [InlineData(10, -5, 10)]
    [InlineData(10, 100.01, 10)]
    [InlineData(10, 10, -0.01)]
    [InlineData(10, 10, 150)]
    public void Criar_PercentualForaDeFaixa_Falha(decimal ppi, decimal quilombola, decimal pcd)
    {
        Result<ReferenciaReservaDemografica> resultado =
            ReferenciaReservaDemografica.Criar("2022", ppi, quilombola, pcd, BaseLegal);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ReferenciaReservaDemograficaErrorCodes.PercentualForaDeFaixa);
    }

    [Theory(DisplayName = "Criar com percentual no limite (0 e 100) é aceito")]
    [InlineData(0)]
    [InlineData(100)]
    public void Criar_PercentualNoLimite_Aceita(decimal valor)
    {
        Result<ReferenciaReservaDemografica> resultado =
            ReferenciaReservaDemografica.Criar("2022", valor, valor, valor, BaseLegal);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Atualizar aplica novos percentuais e preserva o identificador")]
    public void Atualizar_DadosValidos_PreservaId()
    {
        ReferenciaReservaDemografica referencia =
            ReferenciaReservaDemografica.Criar("2022", 78.50m, 1.20m, 8.40m, BaseLegal).Value!;
        Guid idOriginal = referencia.Id;

        Result resultado = referencia.Atualizar("2022", 79.00m, 1.30m, 8.50m, BaseLegal);

        resultado.IsSuccess.Should().BeTrue();
        referencia.Id.Should().Be(idOriginal);
        referencia.PpiPercentual.Valor.Should().Be(79.00m);
    }

    [Fact(DisplayName = "Atualizar com percentual inválido falha")]
    public void Atualizar_PercentualInvalido_Falha()
    {
        ReferenciaReservaDemografica referencia =
            ReferenciaReservaDemografica.Criar("2022", 78.50m, 1.20m, 8.40m, BaseLegal).Value!;

        Result resultado = referencia.Atualizar("2022", 120.00m, 1.20m, 8.40m, BaseLegal);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ReferenciaReservaDemograficaErrorCodes.PercentualForaDeFaixa);
    }
}
