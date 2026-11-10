namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamento HTTP dos erros de domínio da reclassificação das permissões.</summary>
internal static class ErrosDaReclassificacao
{
    public static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() => [.. Leitura(), .. Decisao(), .. Entrada()];

    private static KeyValuePair<string, DomainErrorMapping>[] Leitura() =>
    [
        new(ReclassificacaoErrorCodes.NaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.reclassificacao.nao_encontrada", "Reclassificação não encontrada")),
        new(ReclassificacaoErrorCodes.SemPermissaoNaEtapa, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.reclassificacao.sem_permissao_na_etapa", "Acesso negado: a operação exige a permissão da etapa")),
        new(ReclassificacaoErrorCodes.Superada, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.reclassificacao.superada", "Uma reclassificação mais recente substituiu esta")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Decisao() =>
    [
        new(ReclassificacaoErrorCodes.EtapaJaDecidida, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.reclassificacao.etapa_ja_decidida", "A etapa já foi decidida para a concessão")),
        new(ReclassificacaoErrorCodes.ConcessaoForaDaReclassificacao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.reclassificacao.concessao_fora_da_reclassificacao", "A concessão não está suspensa por esta reclassificação")),
        new(ReclassificacaoErrorCodes.EtapaNaoExigida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.reclassificacao.etapa_nao_exigida", "A etapa não é exigida para a concessão")),
        new(ReclassificacaoErrorCodes.DecisorEBeneficiario, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.reclassificacao.decisor_e_beneficiario", "Ninguém decide a própria concessão")),
        new(ReclassificacaoErrorCodes.MesmoDecisorNasDuasEtapas, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.reclassificacao.mesmo_decisor_nas_duas_etapas", "A mesma pessoa não decide as duas etapas")),
        new(ReclassificacaoErrorCodes.DocumentoNaoLido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.reclassificacao.documento_nao_lido", "O documento instituidor ainda não foi lido")),
        new(ReclassificacaoErrorCodes.EscopoNaoComporta, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.reclassificacao.escopo_nao_comporta", "O escopo da concessão não comporta o novo alcance")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Entrada() =>
    [
        new(ReclassificacaoErrorCodes.MotivoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.reclassificacao.motivo_invalido", "Motivo ausente ou longo demais")),
        new(ReclassificacaoErrorCodes.EtapaInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.reclassificacao.etapa_invalida", "Etapa inválida")),
        new(ReclassificacaoErrorCodes.ResultadoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.reclassificacao.resultado_invalido", "Resultado inválido")),
        new(ReclassificacaoErrorCodes.AlvoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.reclassificacao.alvo_invalido", "Concessões da decisão inválidas")),
    ];
}
