namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

internal static class UnidadeErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
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

        new(UnidadeErrorCodes.MotivoMudancaTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.motivo_mudanca_tamanho",
                "Tamanho do motivo da mudança de identificador inválido")),

        new(UnidadeErrorCodes.TipoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.unidade.tipo_invalido",
                "Tipo de unidade inválido")),

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
