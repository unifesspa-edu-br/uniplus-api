namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class UnidadeAdministradoraSnapshotTests
{
    [Fact(DisplayName = "Criar com dados válidos tem sucesso")]
    public void Criar_Valida_Sucesso()
    {
        Result<UnidadeAdministradoraSnapshot> resultado = UnidadeAdministradoraSnapshot.Criar(
            "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Sigla.Should().Be("CEPS");
        resultado.Value!.Slug.Should().Be("ceps");
        resultado.Value!.Nome.Should().Be("Centro de Processos Seletivos");
        resultado.Value!.Tipo.Should().Be("ADMINISTRATIVA");
    }

    [Theory(DisplayName = "Criar com espaços nas bordas remove-os (Trim)")]
    [InlineData(" CEPS ", "CEPS")]
    public void Criar_ComEspacos_Trima(string entrada, string esperado)
    {
        Result<UnidadeAdministradoraSnapshot> resultado = UnidadeAdministradoraSnapshot.Criar(
            entrada, "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Sigla.Should().Be(esperado);
    }

    [Fact(DisplayName = "Criar com sigla vazia falha")]
    public void Criar_SiglaVazia_Falha()
    {
        Result<UnidadeAdministradoraSnapshot> resultado = UnidadeAdministradoraSnapshot.Criar(
            "", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("UnidadeAdministradoraSnapshot.SiglaObrigatoria");
    }

    [Fact(DisplayName = "Criar com slug vazio falha")]
    public void Criar_SlugVazio_Falha()
    {
        Result<UnidadeAdministradoraSnapshot> resultado = UnidadeAdministradoraSnapshot.Criar(
            "CEPS", "", "Centro de Processos Seletivos", "ADMINISTRATIVA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("UnidadeAdministradoraSnapshot.SlugObrigatorio");
    }

    [Fact(DisplayName = "Criar com nome vazio falha")]
    public void Criar_NomeVazio_Falha()
    {
        Result<UnidadeAdministradoraSnapshot> resultado = UnidadeAdministradoraSnapshot.Criar(
            "CEPS", "ceps", "", "ADMINISTRATIVA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("UnidadeAdministradoraSnapshot.NomeObrigatorio");
    }

    [Fact(DisplayName = "Criar com tipo vazio falha")]
    public void Criar_TipoVazio_Falha()
    {
        Result<UnidadeAdministradoraSnapshot> resultado = UnidadeAdministradoraSnapshot.Criar(
            "CEPS", "ceps", "Centro de Processos Seletivos", "");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("UnidadeAdministradoraSnapshot.TipoObrigatorio");
    }
}
