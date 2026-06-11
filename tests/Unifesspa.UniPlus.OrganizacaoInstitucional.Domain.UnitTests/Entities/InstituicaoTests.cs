namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// Testes de unidade do agregado <see cref="Instituicao"/> (Story #585):
/// validação de formato na factory e na atualização. O invariante singleton e a
/// conferência do tipo da Unidade raiz são responsabilidade do handler e estão
/// cobertos nos testes de Application/Integration.
/// </summary>
public sealed class InstituicaoTests
{
    private static Result<Instituicao> CriarValida(
        string codigoEmec = "3990",
        string nome = "Universidade Federal do Sul e Sudeste do Pará",
        string sigla = "Unifesspa",
        string organizacaoAcademica = "Universidade",
        string categoriaAdministrativa = "Pública Federal",
        Guid? unidadeRaizId = null) =>
        Instituicao.Criar(
            codigoEmec,
            nome,
            sigla,
            organizacaoAcademica,
            categoriaAdministrativa,
            cnpj: null,
            mantenedora: null,
            codigoMantenedoraEmec: null,
            situacao: null,
            atoCredenciamento: null,
            atoRecredenciamento: null,
            conceitoInstitucional: null,
            igc: null,
            website: null,
            enderecoSede: null,
            municipioSede: null,
            unidadeRaizId);

    [Fact(DisplayName = "Criar com campos válidos retorna sucesso com Id Guid v7 não vazio (CA-01)")]
    public void Criar_ComCamposValidos_RetornaSucessoComGuidV7()
    {
        Result<Instituicao> resultado = CriarValida();

        resultado.IsSuccess.Should().BeTrue();
        Instituicao instituicao = resultado.Value!;
        instituicao.Id.Should().NotBe(Guid.Empty);
        instituicao.Id.Version.Should().Be(7);
        instituicao.CodigoEmec.Should().Be("3990");
        instituicao.Sigla.Should().Be("Unifesspa");
        instituicao.IsDeleted.Should().BeFalse();
    }

    [Theory(DisplayName = "Criar sem um campo obrigatório retorna o erro correspondente (CA-03)")]
    [InlineData("", "Nome", "SIGLA", "Org", "Cat", InstituicaoErrorCodes.CodigoEmecObrigatorio)]
    [InlineData("3990", "", "SIGLA", "Org", "Cat", InstituicaoErrorCodes.NomeObrigatorio)]
    [InlineData("3990", "Nome", "", "Org", "Cat", InstituicaoErrorCodes.SiglaObrigatoria)]
    [InlineData("3990", "Nome", "SIGLA", "", "Cat", InstituicaoErrorCodes.OrganizacaoAcademicaObrigatoria)]
    [InlineData("3990", "Nome", "SIGLA", "Org", "", InstituicaoErrorCodes.CategoriaAdministrativaObrigatoria)]
    public void Criar_SemCampoObrigatorio_RetornaErro(
        string codigoEmec,
        string nome,
        string sigla,
        string organizacaoAcademica,
        string categoriaAdministrativa,
        string codigoEsperado)
    {
        Result<Instituicao> resultado = CriarValida(
            codigoEmec, nome, sigla, organizacaoAcademica, categoriaAdministrativa);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(codigoEsperado);
    }

    [Fact(DisplayName = "Criar normaliza Trim dos campos e converte opcionais em branco para null")]
    public void Criar_NormalizaTrimEOpcionaisVazios()
    {
        Result<Instituicao> resultado = Instituicao.Criar(
            "  3990  ", "  Universidade  ", "  Unifesspa  ", "  Universidade  ", "  Pública Federal  ",
            cnpj: "   ", mantenedora: "  Fundação  ", codigoMantenedoraEmec: null, situacao: null,
            atoCredenciamento: null, atoRecredenciamento: null, conceitoInstitucional: null, igc: null,
            website: null, enderecoSede: null, municipioSede: "  Marabá  ", unidadeRaizId: null);

        resultado.IsSuccess.Should().BeTrue();
        Instituicao instituicao = resultado.Value!;
        instituicao.CodigoEmec.Should().Be("3990");
        instituicao.Nome.Should().Be("Universidade");
        instituicao.Cnpj.Should().BeNull("string só com espaços vira null");
        instituicao.Mantenedora.Should().Be("Fundação");
        instituicao.MunicipioSede.Should().Be("Marabá");
    }

    [Fact(DisplayName = "Atualizar com campos válidos altera o estado regulatório e o vínculo")]
    public void Atualizar_ComCamposValidos_AlteraEstado()
    {
        Instituicao instituicao = CriarValida().Value!;
        Guid novaRaiz = Guid.CreateVersion7();

        Result resultado = instituicao.Atualizar(
            "4000", "Universidade Federal Renomeada", "UFR", "Universidade", "Pública Federal",
            cnpj: "12.345.678/0001-99", mantenedora: null, codigoMantenedoraEmec: null, situacao: "Credenciada",
            atoCredenciamento: "Portaria 123", atoRecredenciamento: null, conceitoInstitucional: "4", igc: "4",
            website: "https://unifesspa.edu.br", enderecoSede: null, municipioSede: "Marabá", unidadeRaizId: novaRaiz);

        resultado.IsSuccess.Should().BeTrue();
        instituicao.CodigoEmec.Should().Be("4000");
        instituicao.Sigla.Should().Be("UFR");
        instituicao.Situacao.Should().Be("Credenciada");
        instituicao.UnidadeRaizId.Should().Be(novaRaiz);
    }

    [Fact(DisplayName = "Atualizar sem nome retorna NomeObrigatorio e preserva o estado anterior")]
    public void Atualizar_SemNome_RetornaErro()
    {
        Instituicao instituicao = CriarValida().Value!;

        Result resultado = instituicao.Atualizar(
            "3990", "", "Unifesspa", "Universidade", "Pública Federal",
            cnpj: null, mantenedora: null, codigoMantenedoraEmec: null, situacao: null,
            atoCredenciamento: null, atoRecredenciamento: null, conceitoInstitucional: null, igc: null,
            website: null, enderecoSede: null, municipioSede: null, unidadeRaizId: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.NomeObrigatorio);
        instituicao.Nome.Should().Be("Universidade Federal do Sul e Sudeste do Pará");
    }
}
