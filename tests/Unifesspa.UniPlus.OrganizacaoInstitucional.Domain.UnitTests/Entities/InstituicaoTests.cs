namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>
/// Testes de unidade do agregado <see cref="Instituicao"/> (Story #585 · #686):
/// validação de formato na factory e na atualização, incluindo a referência de
/// cidade do Geo (ADR-0090) opcional all-or-nothing. O invariante singleton e a
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
        string? cidadeCodigoIbge = null,
        string? cidadeNome = null,
        string? cidadeUf = null,
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
            cidadeCodigoIbge,
            cidadeNome,
            cidadeUf,
            cidadeOrigem: null,
            cidadeDisplayAtualizadoEm: null,
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
            website: null, enderecoSede: "  Rua A, 100  ",
            cidadeCodigoIbge: "1504208", cidadeNome: "  Marabá  ", cidadeUf: "pa",
            cidadeOrigem: null, cidadeDisplayAtualizadoEm: null, unidadeRaizId: null);

        resultado.IsSuccess.Should().BeTrue();
        Instituicao instituicao = resultado.Value!;
        instituicao.CodigoEmec.Should().Be("3990");
        instituicao.Nome.Should().Be("Universidade");
        instituicao.Cnpj.Should().BeNull("string só com espaços vira null");
        instituicao.Mantenedora.Should().Be("Fundação");
        instituicao.EnderecoSede.Should().Be("Rua A, 100");
        instituicao.CidadeCodigoIbge.Should().Be("1504208");
        instituicao.CidadeNome.Should().Be("Marabá");
        instituicao.CidadeUf.Should().Be("PA", "a UF é normalizada para caixa alta");
    }

    [Fact(DisplayName = "Criar sem referência de cidade é válido e deixa o trio nulo (opcional)")]
    public void Criar_SemReferenciaDeCidade_DeixaTrioNulo()
    {
        Instituicao instituicao = CriarValida().Value!;

        instituicao.CidadeCodigoIbge.Should().BeNull();
        instituicao.CidadeNome.Should().BeNull();
        instituicao.CidadeUf.Should().BeNull();
        instituicao.CidadeOrigem.Should().BeNull();
        instituicao.CidadeDisplayAtualizadoEm.Should().BeNull();
    }

    [Theory(DisplayName = "Criar com referência de cidade parcial retorna o campo faltante (all-or-nothing)")]
    [InlineData(null, "Marabá", "PA", CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio)]
    [InlineData("1504208", null, "PA", CidadeReferenciaErrorCodes.NomeObrigatorio)]
    [InlineData("1504208", "Marabá", null, CidadeReferenciaErrorCodes.UfObrigatoria)]
    public void Criar_ComCidadeParcial_RetornaErro(
        string? cidadeCodigoIbge, string? cidadeNome, string? cidadeUf, string codigoEsperado)
    {
        Result<Instituicao> resultado = CriarValida(
            cidadeCodigoIbge: cidadeCodigoIbge, cidadeNome: cidadeNome, cidadeUf: cidadeUf);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(codigoEsperado);
    }

    [Fact(DisplayName = "Criar com UF incoerente com o prefixo do código IBGE é rejeitado (15=PA, não SP)")]
    public void Criar_ComCidadeIncoerente_RetornaUfIncoerente()
    {
        Result<Instituicao> resultado = CriarValida(
            cidadeCodigoIbge: "1504208", cidadeNome: "Marabá", cidadeUf: "SP");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }

    [Fact(DisplayName = "Criar com nome de cidade acima do limite retorna erro de domínio (422), não estoura na persistência")]
    public void Criar_ComCidadeNomeMuitoLongo_RetornaNomeTamanho()
    {
        string nomeLongo = new('A', ReferenciaCidadeGeo.NomeMaxLength + 1);

        Result<Instituicao> resultado = CriarValida(
            cidadeCodigoIbge: "1504208", cidadeNome: nomeLongo, cidadeUf: "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.NomeTamanho);
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
            website: "https://unifesspa.edu.br", enderecoSede: null,
            cidadeCodigoIbge: "1504208", cidadeNome: "Marabá", cidadeUf: "PA",
            cidadeOrigem: ReferenciaCidadeGeo.OrigemGeoApi, cidadeDisplayAtualizadoEm: null, unidadeRaizId: novaRaiz);

        resultado.IsSuccess.Should().BeTrue();
        instituicao.CodigoEmec.Should().Be("4000");
        instituicao.Sigla.Should().Be("UFR");
        instituicao.Situacao.Should().Be("Credenciada");
        instituicao.CidadeCodigoIbge.Should().Be("1504208");
        instituicao.CidadeUf.Should().Be("PA");
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
            website: null, enderecoSede: null,
            cidadeCodigoIbge: null, cidadeNome: null, cidadeUf: null,
            cidadeOrigem: null, cidadeDisplayAtualizadoEm: null, unidadeRaizId: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(InstituicaoErrorCodes.NomeObrigatorio);
        instituicao.Nome.Should().Be("Universidade Federal do Sul e Sudeste do Pará");
    }
}
