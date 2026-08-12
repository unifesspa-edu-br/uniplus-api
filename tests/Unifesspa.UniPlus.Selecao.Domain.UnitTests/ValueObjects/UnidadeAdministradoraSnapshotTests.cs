namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
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

    // ── Cidade (issue #1114) — opcional all-or-nothing ────────────────────

    [Fact(DisplayName = "Criar sem cidade tem sucesso — snapshot legado, pré-issue #1114")]
    public void Criar_SemCidade_Sucesso()
    {
        Result<UnidadeAdministradoraSnapshot> resultado = UnidadeAdministradoraSnapshot.Criar(
            "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.CidadeCodigoIbge.Should().BeNull();
        resultado.Value.CidadeNome.Should().BeNull();
        resultado.Value.CidadeUf.Should().BeNull();
    }

    [Fact(DisplayName = "Criar com trio de cidade completo tem sucesso e normaliza UF para uppercase")]
    public void Criar_ComCidadeCompleta_Sucesso()
    {
        Result<UnidadeAdministradoraSnapshot> resultado = UnidadeAdministradoraSnapshot.Criar(
            "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA",
            cidadeCodigoIbge: "1504208", cidadeNome: "Marabá", cidadeUf: "pa");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.CidadeCodigoIbge.Should().Be("1504208");
        resultado.Value.CidadeNome.Should().Be("Marabá");
        resultado.Value.CidadeUf.Should().Be("PA");
    }

    [Theory(DisplayName = "Criar com cidade parcialmente preenchida falha — bicondicional all-or-nothing")]
    [InlineData(null, "Marabá", "PA")]
    [InlineData("1504208", null, "PA")]
    [InlineData("1504208", "Marabá", null)]
    public void Criar_ComCidadeParcial_Falha(string? codigo, string? nome, string? uf)
    {
        Result<UnidadeAdministradoraSnapshot> resultado = UnidadeAdministradoraSnapshot.Criar(
            "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA",
            cidadeCodigoIbge: codigo, cidadeNome: nome, cidadeUf: uf);

        resultado.IsFailure.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar com código IBGE incoerente com a UF falha")]
    public void Criar_ComCidadeUfIncoerente_Falha()
    {
        Result<UnidadeAdministradoraSnapshot> resultado = UnidadeAdministradoraSnapshot.Criar(
            "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA",
            cidadeCodigoIbge: "1504208", cidadeNome: "Marabá", cidadeUf: "SP");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }
}
