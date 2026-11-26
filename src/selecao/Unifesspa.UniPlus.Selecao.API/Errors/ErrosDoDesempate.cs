namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas dos critérios de desempate, inclusive o desempate por área do ENEM.
/// </summary>
internal static class ErrosDoDesempate
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. DaOrdemDosCriterios(),
        .. DaDefinicaoDoCriterio(),
        .. DoDesempatePorAreaDoEnem(),
    ];

    /// <summary>Ordem única dos critérios e etapa que eles referenciam.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaOrdemDosCriterios() =>
    [
        new("ProcessoSeletivo.OrdemDesempateDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ordem_desempate_duplicada", "Cada critério de desempate deve ter uma ordem única dentro do processo")),
        new("ProcessoSeletivo.EtapaRefDesempateInexistente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_ref_desempate_inexistente", "O critério de desempate referencia uma etapa que não existe neste processo")),
        new("ProcessoSeletivo.EtapaReferenciadaPorDesempate", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_referenciada_por_desempate", "A etapa é referenciada por um critério de desempate e não pode ser removida sem antes reconfigurar o desempate")),
    ];

    /// <summary>Parâmetros de cada critério de desempate e a regra que ele referencia.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaDefinicaoDoCriterio() =>
    [
        new("CriterioDesempate.OrdemInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.ordem_invalida", "A ordem do critério de desempate deve ser maior que zero")),
        new("CriterioDesempate.ParametrosIncompativeisComRegra", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.parametros_incompativeis_com_regra", "Os parâmetros informados não correspondem à regra referenciada")),
        new("CriterioDesempate.IdadeMinimaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.idade_minima_invalida", "A idade mínima do critério IDOSO deve ser maior que zero")),
        new("CriterioDesempate.RegraNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.regra_nao_encontrada", "Regra de desempate não encontrada no catálogo de regras")),
        new("CriterioDesempate.RegraTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.regra_tipo_invalido", "A regra referenciada não é uma regra de desempate")),
        new("CriterioDesempate.EtapaRefObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.etapa_ref_obrigatorio", "EtapaRef é obrigatório para a regra DESEMPATE_MAIOR_NOTA_ETAPA")),
        new("CriterioDesempate.IdadeMinimaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.idade_minima_obrigatoria", "IdadeMinima é obrigatória para a regra DESEMPATE_IDOSO")),
        new("CriterioDesempate.PredicadoFatoIncompleto", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.predicado_fato_incompleto", "Fato, Operador e Valor são obrigatórios para a regra DESEMPATE_CONDICAO_SOBRE_DADO")),
    ];

    /// <summary>Forma da ordem de áreas e coerência dela com o quadro de pesos por área congelado na classificação.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DoDesempatePorAreaDoEnem() =>
    [
        new(DesempatePorAreaEnemErrorCodes.AreasObrigatorias, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.areas_obrigatorias", "O critério de desempate por área do ENEM exige ao menos uma área")),
        new(DesempatePorAreaEnemErrorCodes.AreasEmExcesso, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.areas_em_excesso", "A ordem de desempate por área do ENEM tem áreas demais")),
        new(DesempatePorAreaEnemErrorCodes.AreaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.area_invalida", "Código de área do ENEM inválido na ordem de desempate")),
        new(DesempatePorAreaEnemErrorCodes.AreaRepetida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.area_repetida", "A mesma área do ENEM aparece mais de uma vez na ordem de desempate")),
        new(ProcessoSeletivoErrorCodes.CriteriosDesempateEmExcesso, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.criterios_desempate_em_excesso", "A ordem de desempate tem critérios demais")),
        new(DesempatePorAreaEnemErrorCodes.AreaCitadaPorOutroCriterio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.area_enem_citada_por_outro_criterio", "A área do ENEM já é comparada por outro critério de desempate")),
        new(DesempatePorAreaEnemErrorCodes.SemQuadro, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.desempate_area_enem_sem_quadro", "Desempate por área do ENEM exige classificação baseada em ENEM com quadro de pesos por área")),
        new(DesempatePorAreaEnemErrorCodes.ForaDoQuadro, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.desempate_area_enem_fora_do_quadro", "O desempate cita área do ENEM que o quadro de pesos por área não tem")),
    ];
}
