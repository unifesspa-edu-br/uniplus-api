namespace Unifesspa.UniPlus.Autorizacao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamento HTTP dos erros de domínio da revisão das concessões.</summary>
internal static class ErrosDaRevisaoDasConcessoes
{
    public static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() => [.. Revisao(), .. Decisao()];

    private static KeyValuePair<string, DomainErrorMapping>[] Revisao() =>
    [
        new(RevisaoDasConcessoesErrorCodes.RevisaoNaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.autorizacao.revisao_das_concessoes.revisao_nao_encontrada", "Revisão das concessões não encontrada")),
        new(RevisaoDasConcessoesErrorCodes.RevisaoEncerrada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.revisao_das_concessoes.revisao_encerrada", "A revisão das concessões já terminou", RecusaAposGravacao: true)),
        new(RevisaoDasConcessoesErrorCodes.RevisaoEmEncerramento, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.revisao_das_concessoes.revisao_em_encerramento", "A revisão das concessões está sendo encerrada; repita a operação")),
        new(RevisaoDasConcessoesErrorCodes.PrazoEncerrado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.revisao_das_concessoes.prazo_encerrado", "O prazo da revisão das concessões terminou", RecusaAposGravacao: true)),
    ];

    private static KeyValuePair<string, DomainErrorMapping>[] Decisao() =>
    [
        new(RevisaoDasConcessoesErrorCodes.DecisoesJaEnviadas, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.revisao_das_concessoes.decisoes_ja_enviadas", "As decisões já foram enviadas para esta revisão")),
        new(RevisaoDasConcessoesErrorCodes.DecisoesIncompletas, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.revisao_das_concessoes.decisoes_incompletas", "O envio não decide todas as concessões da versão anterior")),
        new(RevisaoDasConcessoesErrorCodes.DecisoesInvalidas, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.revisao_das_concessoes.decisoes_invalidas", "Decisões inválidas")),
        new(RevisaoDasConcessoesErrorCodes.DecisoesNaoEnviadas, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.revisao_das_concessoes.decisoes_nao_enviadas", "As decisões da gestão de acessos ainda não foram enviadas")),
        new(RevisaoDasConcessoesErrorCodes.DecisaoDaPropriaConcessao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.revisao_das_concessoes.decisao_da_propria_concessao", "Ninguém decide a própria concessão")),
        new(RevisaoDasConcessoesErrorCodes.MesmaPessoaNasDuasEtapas, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.revisao_das_concessoes.mesma_pessoa_nas_duas_etapas", "A mesma pessoa não decide as duas etapas")),
        new(RevisaoDasConcessoesErrorCodes.ConcessaoForaDaEtapa, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.revisao_das_concessoes.concessao_fora_da_etapa", "A concessão não aguarda esta etapa")),
        new(RevisaoDasConcessoesErrorCodes.ConcessaoJaDecidida, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.autorizacao.revisao_das_concessoes.concessao_ja_decidida", "A concessão já foi decidida")),
        new(RevisaoDasConcessoesErrorCodes.ExclusaoNaoConfirmada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.revisao_das_concessoes.exclusao_nao_confirmada", "A exclusão da lista exige confirmação")),
        new(RevisaoDasConcessoesErrorCodes.MotivoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.autorizacao.revisao_das_concessoes.motivo_invalido", "Motivo ausente ou longo demais")),
        new(RevisaoDasConcessoesErrorCodes.SemPermissaoNaEtapa, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.autorizacao.revisao_das_concessoes.sem_permissao_na_etapa", "Acesso negado: a operação exige a permissão da etapa")),
    ];
}
