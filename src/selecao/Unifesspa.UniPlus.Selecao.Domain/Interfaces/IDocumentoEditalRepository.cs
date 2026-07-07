namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Repositório de <see cref="DocumentoEdital"/> — independente de
/// <see cref="IProcessoSeletivoRepository"/> porque o documento não é
/// entidade filha do agregado <see cref="ProcessoSeletivo"/> (ver comentário
/// da entidade).
/// </summary>
public interface IDocumentoEditalRepository : IRepository<DocumentoEdital>
{
    /// <summary>
    /// Reivindica atomicamente a confirmação do documento — <c>UPDATE ...
    /// WHERE id = @id AND status = Pendente</c> condicional, sem passar pelo
    /// change tracker. Duas confirmações concorrentes do mesmo documento
    /// (Idempotency-Keys diferentes) nunca ganham as duas: a perdedora
    /// bloqueia no lock de linha do Postgres e, ao destravar, sua condição
    /// não bate mais (a vencedora já avançou o status), então afeta zero
    /// linhas. Só depois de reivindicar com sucesso o handler lê/valida o
    /// conteúdo e grava a cópia selada — nenhuma confirmação perdedora chega
    /// a escrever no storage.
    /// </summary>
    Task<bool> TentarReivindicarConfirmacaoAsync(Guid id, CancellationToken cancellationToken = default);
}
