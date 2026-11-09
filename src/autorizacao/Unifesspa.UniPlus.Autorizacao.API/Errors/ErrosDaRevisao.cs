namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamento HTTP dos erros de domínio de: revisão de acessos.</summary>
internal static class ErrosDaRevisao
{
    public static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
        [.. Revisao(), .. Acesso()];

    private static KeyValuePair<string, DomainErrorMapping>[] Revisao() =>
    [
        new(RevisaoErrorCodes.NaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.revisao.nao_encontrada", "Revisão de acessos não encontrada")),
        new(RevisaoErrorCodes.ItemNaoEncontrado, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.revisao.item_nao_encontrado", "Item da revisão não encontrado")),
        new(RevisaoErrorCodes.JaHaEmAndamento, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.revisao.ja_ha_em_andamento", "Já existe revisão de acessos em andamento")),
        new(RevisaoErrorCodes.ItemJaDecidido, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.revisao.item_ja_decidido", "O item da revisão já foi decidido")),
        new(RevisaoErrorCodes.ConcessaoNaoAtiva, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.revisao.concessao_nao_ativa", "A concessão do item já não está ativa")),
        new(RevisaoErrorCodes.DecisorEBeneficiario, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.revisao.decisor_e_beneficiario", "Ninguém decide o item da própria concessão")),
        new(RevisaoErrorCodes.DecisorNaoDecideOItem, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.revisao.decisor_nao_decide_o_item", "O item cabe à outra governança")),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Acesso() =>
    [
        new(RevisaoErrorCodes.SemPermissao, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.auth.forbidden", "Acesso negado")),
        new(RevisaoErrorCodes.IdentidadeIncompleta, new DomainErrorMapping(StatusCodes.Status401Unauthorized, "uniplus.autorizacao.revisao.identidade_incompleta", "Identidade do solicitante incompleta")),
        new(RevisaoErrorCodes.AutorNaoIdentificado, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.revisao.autor_nao_identificado", "Autor da operação não identificado")),
        MapeamentosComuns.Concorrencia(RevisaoErrorCodes.ConflitoDeConcorrencia),
    ];
}
