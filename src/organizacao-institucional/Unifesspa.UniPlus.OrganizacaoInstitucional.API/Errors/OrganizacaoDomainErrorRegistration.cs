namespace Unifesspa.UniPlus.OrganizacaoInstitucional.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, OrganizacaoDomainErrorRegistration>().")]
internal sealed class OrganizacaoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
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

        // ── Instituicao (singleton e-MEC) ────────────────────────────────
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

        // ── Referência de cidade do Geo (sede da Instituição) ─────────────
        new(CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.cidade_referencia.codigo_ibge_obrigatorio",
                "Código IBGE da cidade é obrigatório")),

        new(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.cidade_referencia.codigo_ibge_formato_invalido",
                "Código IBGE da cidade em formato inválido")),

        new(CidadeReferenciaErrorCodes.UfObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.cidade_referencia.uf_obrigatoria",
                "UF da cidade é obrigatória")),

        new(CidadeReferenciaErrorCodes.UfIncoerente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.cidade_referencia.uf_incoerente",
                "UF informada incompatível com o prefixo do código IBGE")),

        new(CidadeReferenciaErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.cidade_referencia.nome_obrigatorio",
                "Nome da cidade é obrigatório")),

        new(CidadeReferenciaErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.organizacao.cidade_referencia.nome_tamanho",
                "Nome da cidade excede o tamanho máximo")),
    ];
}
