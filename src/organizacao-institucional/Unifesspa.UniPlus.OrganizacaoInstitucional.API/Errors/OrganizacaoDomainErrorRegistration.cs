namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Governance.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, OrganizacaoDomainErrorRegistration>().")]
internal sealed class OrganizacaoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        new(AreaOrganizacionalErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.area_organizacional.nome_obrigatorio",
                "Nome da área é obrigatório")),

        new(AreaOrganizacionalErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.area_organizacional.nome_tamanho",
                "Tamanho do nome da área inválido")),

        new(AreaOrganizacionalErrorCodes.DescricaoObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.area_organizacional.descricao_obrigatoria",
                "Descrição da área é obrigatória")),

        new(AreaOrganizacionalErrorCodes.DescricaoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.area_organizacional.descricao_tamanho",
                "Tamanho da descrição da área inválido")),

        new(AreaOrganizacionalErrorCodes.AdrReferenceObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.area_organizacional.adr_reference_obrigatorio",
                "AdrReferenceCode é obrigatório — adicionar área exige ADR")),

        new(AreaOrganizacionalErrorCodes.AdrReferenceFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.area_organizacional.adr_reference_formato_invalido",
                "AdrReferenceCode em formato inválido")),

        new(AreaOrganizacionalErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.organizacao.area_organizacional.codigo_ja_existe",
                "Já existe uma área organizacional com este código")),

        new(AreaOrganizacionalErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.organizacao.area_organizacional.nao_encontrada",
                "Área organizacional não encontrada")),

        // AreaCodigo.Invalido vem de Governance.Contracts — mapeado aqui para
        // o wire code do módulo OrganizacaoInstitucional, com fallback ao
        // mapping global se outros módulos precisarem.
        new(AreaCodigo.CodigoErroInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.area_codigo.invalido",
                "Código de área inválido")),
    ];
}
