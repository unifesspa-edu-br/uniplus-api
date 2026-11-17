namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.UnitTests.Commands;

using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Enderecos;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Os comandos da Instituição com a identificação da Unifesspa, sem cidade, sem endereço e sem Unidade
/// raiz, salvo o que o cenário trocar.
/// </summary>
internal static class ComandosDeInstituicaoDeTeste
{
    public static CriarInstituicaoCommand Criacao(Guid? unidadeRaizId = null) => new(
        "3990", "Universidade Federal do Sul e Sudeste do Pará", "Unifesspa", "Universidade", "Pública Federal",
        Cnpj: null, Mantenedora: null, CodigoMantenedoraEmec: null, Situacao: null, AtoCredenciamento: null,
        AtoRecredenciamento: null, ConceitoInstitucional: null, Igc: null, Website: null,
        Endereco: null, CidadeCodigoIbge: null, CidadeNome: null, CidadeUf: null, unidadeRaizId);

    public static AtualizarInstituicaoCommand Atualizacao(
        Guid id,
        Guid? unidadeRaizId = null,
        string? cidadeCodigoIbge = null,
        string? cidadeNome = null,
        string? cidadeUf = null) => new(
        id, "3990", "Universidade Federal do Sul e Sudeste do Pará", "Unifesspa", "Universidade", "Pública Federal",
        Cnpj: null, Mantenedora: null, CodigoMantenedoraEmec: null, Situacao: null, AtoCredenciamento: null,
        AtoRecredenciamento: null, ConceitoInstitucional: null, Igc: null, Website: null, Endereco: null,
        CidadeCodigoIbge: cidadeCodigoIbge, CidadeNome: cidadeNome, CidadeUf: cidadeUf, unidadeRaizId);

    public static EnderecoGeoInput EnderecoEmMaraba() =>
        new("68507590", "Folha 31", "s/n", null, "Nova Marabá", null,
            new CidadeReferenciaInput("1504208", "Marabá", "PA"),
            -5.3m, -49.1m, NivelResolucaoEndereco.Logradouro, "logradouro");

    public static Unidade UnidadeDoTipo(TipoUnidade tipo) =>
        Unidade.Criar(
            "Reitoria",
            new IdentificadoresDaUnidade("reitoria", "REIT", "REIT001", null),
            CidadeDaUnidade.Nenhuma,
            new VigenciaDaUnidade(new DateOnly(2026, 1, 1), null),
            null,
            tipo,
            false).Value!;
}
