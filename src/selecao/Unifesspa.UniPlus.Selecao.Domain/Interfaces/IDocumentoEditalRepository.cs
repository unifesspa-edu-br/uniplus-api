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

    /// <summary>
    /// Lista os documentos de um único Processo Seletivo, do mais recente para
    /// o mais antigo. O filtro é da consulta, não de um <c>Where</c> aplicado
    /// depois de <see cref="Unifesspa.UniPlus.Kernel.Domain.Interfaces.IRepository{T}.ObterTodosAsync"/>:
    /// a leitura de um processo nunca carrega os documentos de todos os outros.
    /// </summary>
    /// <remarks>
    /// A ordem é <c>CreatedAt</c> decrescente com <c>Id</c> como desempate, e é
    /// parte do contrato — não um efeito colateral do UUIDv7. O <c>Id</c>
    /// nasce no domínio, no instante em que a entidade é construída; o
    /// <c>CreatedAt</c> é carimbado pelo interceptor de auditoria no
    /// <c>SaveChanges</c>. São dois relógios diferentes, e ordenar por um deles
    /// esperando a ordem do outro daria uma janela em que dois envios
    /// concorrentes aparecem trocados. O desempate por <c>Id</c> cobre o
    /// carimbo idêntico, que a resolução do relógio torna possível.
    /// </remarks>
    Task<IReadOnlyList<DocumentoEdital>> ListarPorProcessoAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken = default);

}
