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

        new(AreaOrganizacionalErrorCodes.TipoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.area_organizacional.tipo_invalido",
                "Tipo de área organizacional inválido")),

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

        // ── Unidade ──────────────────────────────────────────────────────
        new(UnidadeErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.nome_obrigatorio",
                "Nome da unidade é obrigatório")),

        new(UnidadeErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.nome_tamanho",
                "Tamanho do nome da unidade inválido")),

        new(UnidadeErrorCodes.SiglaObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.sigla_obrigatoria",
                "Sigla da unidade é obrigatória")),

        new(UnidadeErrorCodes.SiglaTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.sigla_tamanho",
                "Tamanho da sigla da unidade inválido")),

        new(UnidadeErrorCodes.SiglaJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.organizacao.unidade.sigla_ja_existe",
                "Já existe uma unidade ativa com esta sigla")),

        new(UnidadeErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.codigo_obrigatorio",
                "Código da unidade é obrigatório")),

        new(UnidadeErrorCodes.CodigoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.codigo_tamanho",
                "Tamanho do código da unidade inválido")),

        new(UnidadeErrorCodes.CodigoJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.organizacao.unidade.codigo_ja_existe",
                "Já existe uma unidade ativa com este código")),

        new(UnidadeErrorCodes.SlugObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.slug_obrigatorio",
                "Slug da unidade é obrigatório")),

        new(UnidadeErrorCodes.SlugTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.slug_tamanho",
                "Tamanho do slug da unidade inválido")),

        new(UnidadeErrorCodes.SlugFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.slug_formato_invalido",
                "Slug deve estar no formato kebab-case (ex.: ceps, faculdade-de-ciencias)")),

        new(UnidadeErrorCodes.SlugJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.organizacao.unidade.slug_ja_existe",
                "Já existe uma unidade ativa com este slug")),

        new(UnidadeErrorCodes.AliasTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.alias_tamanho",
                "Tamanho do alias da unidade inválido")),

        new(UnidadeErrorCodes.TipoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.tipo_invalido",
                "Tipo de unidade inválido")),

        new(UnidadeErrorCodes.OrigemInvalida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.origem_invalida",
                "Origem da unidade inválida")),

        new(UnidadeErrorCodes.VigenciaFimAnteriorAoInicio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.vigencia_fim_anterior_ao_inicio",
                "Data de encerramento de vigência não pode ser anterior à data de início")),

        new(UnidadeErrorCodes.SuperiorNaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.superior_nao_encontrado",
                "Unidade superior informada não encontrada")),

        new(UnidadeErrorCodes.SuperiorFormaCiclo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.superior_forma_ciclo",
                "A unidade superior informada formaria ciclo na hierarquia")),

        new(UnidadeErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.organizacao.unidade.nao_encontrada",
                "Unidade não encontrada")),

        new(UnidadeErrorCodes.RemocaoBloqueadaPorSubordinadas,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.organizacao.unidade.remocao_bloqueada_por_subordinadas",
                "Não é possível remover uma unidade que possui subordinadas ativas")),

        new(UnidadeErrorCodes.RemocaoBloqueadaPorInstituicao,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.organizacao.unidade.remocao_bloqueada_por_instituicao",
                "Não é possível remover uma unidade que é raiz de uma instituição")),
    ];
}
