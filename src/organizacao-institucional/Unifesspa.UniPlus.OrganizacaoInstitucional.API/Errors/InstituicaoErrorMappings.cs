namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>Mapeamentos HTTP dos erros de Instituicao (singleton e-MEC).</summary>
internal static class InstituicaoErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(InstituicaoErrorCodes.CodigoEmecObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.instituicao.codigo_emec_obrigatorio",
                "Código e-MEC da instituição é obrigatório")),

        new(InstituicaoErrorCodes.CodigoEmecTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.instituicao.codigo_emec_tamanho",
                "Tamanho do código e-MEC da instituição inválido")),

        new(InstituicaoErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.instituicao.nome_obrigatorio",
                "Nome da instituição é obrigatório")),

        new(InstituicaoErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.instituicao.nome_tamanho",
                "Tamanho do nome da instituição inválido")),

        new(InstituicaoErrorCodes.SiglaObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.instituicao.sigla_obrigatoria",
                "Sigla da instituição é obrigatória")),

        new(InstituicaoErrorCodes.SiglaTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.instituicao.sigla_tamanho",
                "Tamanho da sigla da instituição inválido")),

        new(InstituicaoErrorCodes.OrganizacaoAcademicaObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.instituicao.organizacao_academica_obrigatoria",
                "Organização acadêmica da instituição é obrigatória")),

        new(InstituicaoErrorCodes.OrganizacaoAcademicaTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.instituicao.organizacao_academica_tamanho",
                "Tamanho da organização acadêmica da instituição inválido")),

        new(InstituicaoErrorCodes.CategoriaAdministrativaObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.instituicao.categoria_administrativa_obrigatoria",
                "Categoria administrativa da instituição é obrigatória")),

        new(InstituicaoErrorCodes.CategoriaAdministrativaTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.instituicao.categoria_administrativa_tamanho",
                "Tamanho da categoria administrativa da instituição inválido")),

        new(InstituicaoErrorCodes.CampoOpcionalTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.instituicao.campo_opcional_tamanho",
                "Tamanho de um campo opcional da instituição inválido")),

        new(InstituicaoErrorCodes.JaExisteInstituicaoViva,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.organizacao.instituicao.ja_existe",
                "Já existe uma instituição cadastrada — a plataforma atende uma única instituição")),

        new(InstituicaoErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.organizacao.instituicao.nao_encontrada",
                "Instituição não encontrada")),

        new(InstituicaoErrorCodes.UnidadeRaizNaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.instituicao.unidade_raiz_nao_encontrada",
                "A unidade informada como raiz não foi encontrada ou foi removida")),

        new(InstituicaoErrorCodes.UnidadeRaizNaoEhReitoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.instituicao.unidade_raiz_nao_eh_reitoria",
                "A unidade informada como raiz da instituição deve ser do tipo reitoria")),

        // Referência de cidade do Geo (sede da Instituição, cidade da Unidade, ADR-0090):
        // registrada uma única vez em KernelDomainErrorRegistration. DomainErrorMappingRegistry
        // agrega todo IDomainErrorRegistration num único dicionário global por código; registrar
        // CidadeReferenciaErrorCodes aqui de novo sobrescreveria silenciosamente o mapeamento de
        // outro módulo que registra depois deste na ordem de DI.

        // Referência de endereço estruturado ao Geo (ADR-0096): registrada uma única
        // vez em KernelDomainErrorRegistration, como a referência de cidade. O registry
        // agrega tudo num dicionário por código, então a cópia local só sobrescreveria
        // silenciosamente a de outro módulo conforme a ordem de DI.
    ];
}
