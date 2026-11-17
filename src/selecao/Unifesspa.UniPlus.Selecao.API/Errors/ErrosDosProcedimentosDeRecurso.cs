namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas da declaração de procedimentos de recurso, na janela de etapa e na janela de fase. Os códigos são
/// literais por entidade porque o registro de erros só enxerga literal.
/// </summary>
internal static class ErrosDosProcedimentosDeRecurso
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        new("RecursoDaEtapa.ProcedimentoDeRecursoDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.procedimento_de_recurso_desconhecido", "A regra declarada não é um procedimento de recurso do catálogo")),
        new("RecursoDaEtapa.ProcedimentoDeRecursoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.procedimento_de_recurso_duplicado", "A mesma matéria tem mais de um procedimento de recurso declarado")),
        new("RecursoDaEtapa.ProcedimentoDeRecursoConflitante", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.procedimento_de_recurso_conflitante", "O tipo de decisão tem procedimento sem alvo e procedimento com alvo ao mesmo tempo")),
        new("RecursoDaEtapa.AlvoDoProcedimentoDeRecursoInexistente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.alvo_do_procedimento_de_recurso_inexistente", "O alvo do procedimento de recurso não existe no lugar da janela")),
        new("RecursoDaEtapa.ProcedimentoParaDecisaoNaoDeclarada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.procedimento_para_decisao_nao_declarada", "O procedimento de recurso é de um tipo de decisão que a fase ou a etapa não declara produzir")),
        new("RecursoDaEtapa.ProcedimentoDeRecursoAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.recurso_da_etapa.procedimento_de_recurso_ausente", "Há decisão sem procedimento de recurso para sua revisão")),
        new("RegraRecursoFase.ProcedimentoDeRecursoDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.procedimento_de_recurso_desconhecido", "A regra declarada não é um procedimento de recurso do catálogo")),
        new("RegraRecursoFase.ProcedimentoDeRecursoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.procedimento_de_recurso_duplicado", "A mesma matéria tem mais de um procedimento de recurso declarado")),
        new("RegraRecursoFase.ProcedimentoDeRecursoConflitante", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.procedimento_de_recurso_conflitante", "O tipo de decisão tem procedimento sem alvo e procedimento com alvo ao mesmo tempo")),
        new("RegraRecursoFase.AlvoDoProcedimentoDeRecursoInexistente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.alvo_do_procedimento_de_recurso_inexistente", "O alvo do procedimento de recurso não existe no lugar da janela")),
        new("RegraRecursoFase.ProcedimentoParaDecisaoNaoDeclarada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.procedimento_para_decisao_nao_declarada", "O procedimento de recurso é de um tipo de decisão que a fase ou a etapa não declara produzir")),
        new("RegraRecursoFase.ProcedimentoDeRecursoAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.procedimento_de_recurso_ausente", "Há decisão sem procedimento de recurso para sua revisão")),
    ];
}
