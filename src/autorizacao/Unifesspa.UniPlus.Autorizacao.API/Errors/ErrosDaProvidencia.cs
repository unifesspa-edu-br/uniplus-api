namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamento HTTP dos erros de domínio de: providência sobre concessão.</summary>
internal static class ErrosDaProvidencia
{
    public static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
        [.. Providencia(), .. Acesso()];

    private static KeyValuePair<string, DomainErrorMapping>[] Providencia() =>
    [
        new(ProvidenciaErrorCodes.NaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.providencia.nao_encontrada", "Providência não encontrada")),
        new(ProvidenciaErrorCodes.TipoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.providencia.tipo_invalido", "Tipo de providência inválido")),
        new(ProvidenciaErrorCodes.FundamentoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.providencia.fundamento_invalido", "Fundamento da providência inválido")),
        new(ProvidenciaErrorCodes.MotivoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.providencia.motivo_invalido", "Motivo da resposta inválido")),
        new(ProvidenciaErrorCodes.ConcessaoNaoAtiva, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.providencia.concessao_nao_ativa", "A concessão da providência já não está ativa")),
        new(ProvidenciaErrorCodes.JaHaAberta, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.providencia.ja_ha_aberta", "Já existe providência aberta para a concessão")),
        new(ProvidenciaErrorCodes.JaRespondida, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.providencia.ja_respondida", "A providência já foi respondida")),
        new(ProvidenciaErrorCodes.JaAtendida, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.providencia.ja_atendida", "A providência já foi atendida")),
        new(ProvidenciaErrorCodes.FiltroDeSituacaoInvalido, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.autorizacao.providencia.filtro_de_situacao_invalido", "Situação do filtro de providências inválida")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Acesso() =>
    [
        new(ProvidenciaErrorCodes.SolicitanteNaoResponde, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.providencia.solicitante_nao_responde", "Quem solicitou a providência não a responde")),
        new(ProvidenciaErrorCodes.BeneficiarioNaoResponde, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.providencia.beneficiario_nao_responde", "O beneficiário da concessão não responde a providência")),
        new(ProvidenciaErrorCodes.SemPermissao, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.auth.forbidden", "Acesso negado")),
        new(ProvidenciaErrorCodes.AutorNaoIdentificado, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.providencia.autor_nao_identificado", "Autor da operação não identificado")),
        MapeamentosComuns.Concorrencia(ProvidenciaErrorCodes.ConflitoDeConcorrencia),
    ];
}
