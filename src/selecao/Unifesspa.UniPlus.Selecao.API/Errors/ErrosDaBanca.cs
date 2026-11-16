namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

/// <summary>
/// Recusas da composição e da regra de deliberação das bancas, e da banca que o recurso exige.
/// </summary>
internal static class ErrosDaBanca
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
        [.. DaDeclaracaoDaRegra(), .. DaExigenciaNaPublicacao()];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaDeclaracaoDaRegra() =>
    [
        new(BancaErrorCodes.ModoDeDeliberacaoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.modo_de_deliberacao_invalido", "O modo de deliberação da banca deve ser colegiada com votação ou ato individual")),
        new(BancaErrorCodes.IntegrantesInvalidos, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.integrantes_invalidos", "A banca deve ter ao menos um integrante")),
        new(BancaErrorCodes.SuplentesInvalidos, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.suplentes_invalidos", "O número de suplentes da banca não pode ser negativo")),
        new(BancaErrorCodes.AtoIndividualSemVotacao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.ato_individual_sem_votacao", "A deliberação por ato individual não declara campos de votação")),
        new(BancaErrorCodes.QuorumForaDoIntervalo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.quorum_fora_do_intervalo", "O quórum deve ficar entre um e o número de integrantes da banca")),
        new(BancaErrorCodes.QuorumIncoerente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.quorum_incoerente", "Com o encerramento por todos os titulares, o quórum é o número de integrantes")),
        new(BancaErrorCodes.CriterioObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.criterio_obrigatorio", "A deliberação colegiada declara o critério de votação")),
        new(BancaErrorCodes.EncerramentoObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.encerramento_obrigatorio", "A deliberação colegiada declara o encerramento da votação")),
        new(BancaErrorCodes.DesempateObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.desempate_obrigatorio", "O critério de maioria, em banca com mais de um integrante, declara o desempate")),
        new(BancaErrorCodes.DesempateNaoSeAplica, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.desempate_nao_se_aplica", "O desempate só se aplica ao critério de maioria, em banca com mais de um integrante")),
        new(BancaErrorCodes.SemUnanimidadeObrigatoria, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.sem_unanimidade_obrigatoria", "O critério de unanimidade, em banca com mais de um integrante, declara o resultado sem unanimidade")),
        new(BancaErrorCodes.SemUnanimidadeNaoSeAplica, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.sem_unanimidade_nao_se_aplica", "O resultado sem unanimidade só se aplica ao critério de unanimidade, em banca com mais de um integrante")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaExigenciaNaPublicacao() =>
    [
        new(BancaErrorCodes.RegraDeDeliberacaoObrigatoria, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.regra_de_deliberacao_obrigatoria", "Toda banca declara a regra de deliberação para o processo ser publicado")),
        new(BancaErrorCodes.IntegrantesAbaixoDoMinimo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.integrantes_abaixo_do_minimo", "A banca declara menos integrantes que o mínimo exigido")),
        new(BancaErrorCodes.RecorteEmBancaDeRecursos, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.recorte_em_banca_de_recursos", "A banca de análise de recursos julga todos os recursos da fase e não declara recorte de competência")),
        new(BancaErrorCodes.RecursoSemBanca, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.cronograma.recurso_sem_banca", "Fase ou etapa com recurso exige banca de análise de recursos")),
    ];
}
