namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas do rascunho do processo seletivo. O rascunho que não existe responde como o processo que não existe
/// (<c>ProcessoSeletivo.NaoEncontrado</c>), e as recusas de <c>If-Match</c> (428 e 412) usam os códigos da precondição de concorrência
/// (<c>PrecondicaoErrorCodes</c>).
/// </summary>
internal static class ErrosDoRascunhoDeProcessoSeletivo
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        new(RascunhoDeProcessoSeletivoErrorCodes.JaPublicado, new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.rascunho_de_processo_seletivo.ja_publicado", "O rascunho já virou processo seletivo")),
        new(RascunhoDeProcessoSeletivoErrorCodes.UnidadeAdministradoraAusente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.rascunho_de_processo_seletivo.unidade_administradora_ausente", "O documento não informa a unidade administradora")),
        new(RascunhoDeProcessoSeletivoErrorCodes.UnidadeForaDaPermissao, new DomainErrorMapping(StatusCodes.Status403Forbidden, "uniplus.selecao.rascunho_de_processo_seletivo.unidade_fora_da_permissao", "Sem permissão para configurar processos na unidade informada")),
        new(RascunhoDeProcessoSeletivoErrorCodes.ConteudoDoDocumentoAusente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.rascunho_de_processo_seletivo.conteudo_do_documento_ausente", "O documento do rascunho veio sem conteúdo")),
        new(RascunhoDeProcessoSeletivoErrorCodes.NomeAcimaDoLimite, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.rascunho_de_processo_seletivo.nome_acima_do_limite", "O nome do processo passa do tamanho que a listagem comporta")),
        new(PrecondicaoErrorCodes.Requerida, new DomainErrorMapping(StatusCodes.Status428PreconditionRequired, "uniplus.selecao.precondicao_requerida", "A mutação exige o If-Match com o ETag corrente")),
        new(PrecondicaoErrorCodes.Falhou, new DomainErrorMapping(StatusCodes.Status412PreconditionFailed, "uniplus.selecao.precondicao_falhou", "O If-Match não corresponde ao estado atual do recurso")),
    ];
}
