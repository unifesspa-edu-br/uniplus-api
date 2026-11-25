namespace Unifesspa.UniPlus.Autorizacao.IntegrationTests.Providencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.API.Filtros;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class FiltroDeSituacaoTests
{
    private static readonly DomainError Recusa = new("Providencia.FiltroInvalido", "Situação inválida. Use um dos códigos publicados para a situação da providência.");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Ler_AusenteOuVazio_NaoFiltra(string? texto)
    {
        Result<SituacaoDaProvidencia?> resultado = FiltroDeSituacao.Ler<SituacaoDaProvidencia>(texto, Recusa);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeNull();
    }

    [Fact]
    public void Ler_CodigoExato_ReconheceSituacao()
    {
        Result<SituacaoDaProvidencia?> resultado = FiltroDeSituacao.Ler<SituacaoDaProvidencia>("ABERTA", Recusa);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Be(SituacaoDaProvidencia.Aberta);
    }

    [Theory]
    [InlineData("aberta")]
    [InlineData(" ABERTA ")]
    [InlineData("   ")]
    [InlineData("todas")]
    [InlineData("arquivada")]
    public void Ler_CodigoDesconhecido_DevolveRecusaSemValorOuLista(string texto)
    {
        Result<SituacaoDaProvidencia?> resultado = FiltroDeSituacao.Ler<SituacaoDaProvidencia>(texto, Recusa);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(Recusa);
        resultado.Error!.Message.Should().NotContain(texto).And.NotContain("ABERTA").And.NotContain("RESPONDIDA");
    }
}
