namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamento HTTP dos erros de domínio da janela de migração.</summary>
internal static class ErrosDaMigracao
{
    public static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() => [.. Janela(), .. Decisao()];

    private static KeyValuePair<string, DomainErrorMapping>[] Janela() =>
    [
        new(MigracaoErrorCodes.JanelaNaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.migracao.janela_nao_encontrada", "Janela de migração não encontrada")),
        new(MigracaoErrorCodes.JanelaEncerrada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.migracao.janela_encerrada", "A janela de migração já terminou", RecusaAposGravacao: true)),
        new(MigracaoErrorCodes.JanelaEmEncerramento, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.migracao.janela_em_encerramento", "A janela de migração está sendo encerrada; repita a operação")),
        new(MigracaoErrorCodes.PrazoEncerrado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.migracao.prazo_encerrado", "O prazo da janela de migração terminou", RecusaAposGravacao: true)),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Decisao() =>
    [
        new(MigracaoErrorCodes.LoteJaEnviado, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.migracao.lote_ja_enviado", "O lote já foi enviado para esta janela")),
        new(MigracaoErrorCodes.LoteIncompleto, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.migracao.lote_incompleto", "O lote não decide todas as concessões da versão anterior")),
        new(MigracaoErrorCodes.LoteInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.migracao.lote_invalido", "Lote inválido")),
        new(MigracaoErrorCodes.LoteNaoEnviado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.migracao.lote_nao_enviado", "O lote da gestão de acessos ainda não foi enviado")),
        new(MigracaoErrorCodes.DecisaoDaPropriaConcessao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.migracao.decisao_da_propria_concessao", "Ninguém decide a própria concessão")),
        new(MigracaoErrorCodes.MesmaPessoaNasDuasEtapas, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.migracao.mesma_pessoa_nas_duas_etapas", "A mesma pessoa não decide as duas etapas")),
        new(MigracaoErrorCodes.ConcessaoForaDaEtapa, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.migracao.concessao_fora_da_etapa", "A concessão não aguarda esta etapa")),
        new(MigracaoErrorCodes.ConcessaoJaDecidida, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.migracao.concessao_ja_decidida", "A concessão já foi decidida")),
        new(MigracaoErrorCodes.ExclusaoNaoConfirmada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.migracao.exclusao_nao_confirmada", "A exclusão da lista exige confirmação")),
        new(MigracaoErrorCodes.MotivoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.migracao.motivo_invalido", "Motivo ausente ou longo demais")),
        new(MigracaoErrorCodes.SemPermissaoNaEtapa, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.migracao.sem_permissao_na_etapa", "Acesso negado: a operação exige a permissão da etapa")),
    ];
}
