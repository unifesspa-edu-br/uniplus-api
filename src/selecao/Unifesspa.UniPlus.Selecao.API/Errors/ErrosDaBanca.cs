namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

/// <summary>
/// Recusas da identificação, da composição e da regra de deliberação das bancas, e da banca que o recurso exige.
/// </summary>
internal static class ErrosDaBanca
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
        [.. DaIdentificacaoDaBanca(), .. DaDeclaracaoDaRegra(), .. DaExigenciaNaPublicacao()];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaIdentificacaoDaBanca() =>
    [
        new(BancaErrorCodes.IdDuplicado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.id_duplicado", "Duas bancas da mesma fase ou etapa têm o mesmo identificador")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaDeclaracaoDaRegra() =>
    [
        new(BancaErrorCodes.ModoDeDeliberacaoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.modo_de_deliberacao_invalido", "O modo de deliberação deve ser colegiada com votação ou ato individual")),
        new(BancaErrorCodes.IntegrantesInvalidos, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.integrantes_invalidos", "A banca deve ter ao menos um integrante")),
        new(BancaErrorCodes.SuplentesInvalidos, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.suplentes_invalidos", "O número de suplentes da banca não pode ser negativo")),
        new(BancaErrorCodes.QuorumForaDoIntervalo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.quorum_fora_do_intervalo", "O quórum deve ficar entre um e o número de integrantes da banca")),
        new(BancaErrorCodes.QuorumIncoerente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.quorum_incoerente", "Com o encerramento por todos os titulares, o quórum é o número de integrantes")),
        new(BancaErrorCodes.CriterioObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.criterio_obrigatorio", "A deliberação colegiada declara o critério de votação")),
        new(BancaErrorCodes.EncerramentoObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.encerramento_obrigatorio", "A deliberação colegiada declara o encerramento da votação")),
        new(BancaErrorCodes.DesempateObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.desempate_obrigatorio", "O critério de maioria, em banca com mais de um integrante, declara o desempate")),
        new(BancaErrorCodes.DesempateNaoSeAplica, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.desempate_nao_se_aplica", "O desempate só se aplica ao critério de maioria, em banca com mais de um integrante")),
        new(BancaErrorCodes.SemUnanimidadeObrigatoria, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.sem_unanimidade_obrigatoria", "O critério de unanimidade, em banca com mais de um integrante, declara o resultado sem unanimidade")),
        new(BancaErrorCodes.SemUnanimidadeNaoSeAplica, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.sem_unanimidade_nao_se_aplica", "O resultado sem unanimidade só se aplica ao critério de unanimidade, em banca com mais de um integrante")),
        new(BancaErrorCodes.VotacaoDoRecursoIncompleta, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.votacao_do_recurso_incompleta", "A votação do recurso está incompleta")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaExigenciaNaPublicacao() =>
    [
        new(BancaErrorCodes.RegraDeDeliberacaoObrigatoria, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.regra_de_deliberacao_obrigatoria", "A banca não declara a regra de deliberação, ou a votação do recurso que julga por ato individual")),
        new(BancaErrorCodes.IntegrantesAbaixoDoMinimo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.integrantes_abaixo_do_minimo", "A banca declara menos integrantes que o mínimo exigido")),
        new(BancaErrorCodes.CandidatosAtendidosEmBancaDeRecursos, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.candidatos_atendidos_em_banca_de_recursos", "A banca que julga recurso não define candidatos atendidos")),
        new(BancaErrorCodes.TipoExigeMaisDeUmaBanca, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.tipo_exige_mais_de_uma_banca", "O tipo de banca exige duas ou mais bancas, e há só uma")),
        new(BancaErrorCodes.TipoNaoAdmiteInstanciaSuperior, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.tipo_nao_admite_instancia_superior", "O recurso admite instância superior, mas o item avaliado se encerra na 1ª instância")),
        new(BancaErrorCodes.FaseSemBancaDaSegundaInstancia, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.fase_sem_banca_da_segunda_instancia", "O recurso admite instância superior, e a fase não prevê banca que o julgue em 2ª instância")),
        new(BancaErrorCodes.EtapaSemBancaDaSegundaInstancia, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.etapa_sem_banca_da_segunda_instancia", "O recurso admite instância superior, e nem a etapa nem a fase preveem banca que o julgue em 2ª instância")),
        new(BancaErrorCodes.FaseComBancaDaSegundaInstanciaSemRecursoParaJulgar, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.fase_com_banca_da_segunda_instancia_sem_recurso_para_julgar", "A fase prevê banca que julga o recurso em 2ª instância, e não há recurso para ela julgar")),
        new(BancaErrorCodes.EtapaComBancaDaSegundaInstanciaSemRecursoParaJulgar, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.etapa_com_banca_da_segunda_instancia_sem_recurso_para_julgar", "A etapa prevê banca que julga o recurso em 2ª instância, e nenhum recurso da etapa admite instância superior")),
        new(BancaErrorCodes.FaseComMaisDeUmaBancaDaSegundaInstancia, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.fase_com_mais_de_uma_banca_da_segunda_instancia", "A fase prevê mais de uma banca que julga o recurso em 2ª instância")),
        new(BancaErrorCodes.EtapaComMaisDeUmaBancaDaSegundaInstancia, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.banca.etapa_com_mais_de_uma_banca_da_segunda_instancia", "A etapa prevê mais de uma banca que julga o recurso em 2ª instância")),
        new(BancaErrorCodes.RecursoSemBanca, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.cronograma.recurso_sem_banca", "O recurso não tem banca que o julgue em 1ª instância")),
    ];
}
