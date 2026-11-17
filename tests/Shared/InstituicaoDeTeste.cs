namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Construtor de dados de teste da Instituição: a Unifesspa, sem cidade da sede, sem endereço e sem Unidade raiz, salvo o que o cenário trocar.
/// </summary>
internal static class InstituicaoDeTeste
{
    public static readonly IdentificacaoRegulatoriaDaInstituicao IdentificacaoDaUnifesspa = new(
        "3990", "Universidade Federal do Sul e Sudeste do Pará", "Unifesspa", "Universidade", "Pública Federal");

    /// <summary>A sede em Marabá como o cliente a informa: sem proveniência nem carimbo.</summary>
    public static readonly CidadeDaSede SedeEmMaraba = new("1504208", "Marabá", "PA", null, null);

    public static Result<Instituicao> Criar(
        IdentificacaoRegulatoriaDaInstituicao? identificacao = null,
        CidadeDaSede cidade = default,
        ReferenciaEnderecoGeo? endereco = null,
        Guid? unidadeRaizId = null) =>
        Instituicao.Criar(
            identificacao ?? IdentificacaoDaUnifesspa,
            cidade,
            endereco,
            unidadeRaizId);
}
