namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Repositório de <see cref="DocumentoAPublicar"/> — independente de
/// <see cref="IProcessoSeletivoRepository"/> porque o documento não é
/// entidade filha do agregado <see cref="ProcessoSeletivo"/> (ver comentário
/// da entidade).
/// </summary>
public interface IDocumentoAPublicarRepository : IRepository<DocumentoAPublicar>, IArquivoEnviadoRepository
{
    /// <summary>
    /// Lista os documentos de um único Processo Seletivo, do mais recente para
    /// o mais antigo. O filtro é da consulta, não de um <c>Where</c> aplicado
    /// depois de <see cref="Unifesspa.UniPlus.Kernel.Domain.Interfaces.IRepository{T}.ObterTodosAsync"/>:
    /// a leitura de um processo nunca carrega os documentos de todos os outros. Com
    /// <paramref name="finalidade"/>, só os documentos enviados para ela; <see langword="null"/> não
    /// filtra.
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
    Task<IReadOnlyList<DocumentoAPublicar>> ListarPorProcessoAsync(
        Guid processoSeletivoId,
        FinalidadeDoDocumento? finalidade,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Os documentos de um processo com os ids dados, sem rastreamento. Id que não existe ou que é de
    /// outro processo não volta: quem pede decide o que fazer com a ausência.
    /// </summary>
    Task<IReadOnlyList<DocumentoAPublicar>> ListarDoProcessoAsync(
        Guid processoSeletivoId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}
