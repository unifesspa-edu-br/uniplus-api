namespace Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>
/// Repositório da entidade <see cref="TipoAtoPublicado"/> (schema
/// <c>publicacoes</c>, ADR-0097). Todas as leituras excluem registros
/// soft-deleted via query filter por convenção.
/// </summary>
public interface ITipoAtoPublicadoRepository
{
    /// <summary>Carrega a versão do tipo rastreada pelo contexto, para mutação.</summary>
    Task<TipoAtoPublicado?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Carrega a versão do tipo para leitura (<c>AsNoTracking</c>) — projeção em DTO.</summary>
    Task<TipoAtoPublicado?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Resolve a única versão viva de <paramref name="codigo"/> vigente em
    /// <paramref name="data"/>, na janela semiaberta <c>[inicio, fim)</c>.
    /// </summary>
    /// <remarks>
    /// A exclusion constraint do banco garante que existe no máximo uma. Se houver
    /// mais de uma, o repositório <b>lança</b> em vez de desempatar — é bug de
    /// integridade, e engoli-lo devolveria silenciosamente a versão errada.
    /// </remarks>
    Task<TipoAtoPublicado?> ObterVigenteAsync(string codigo, DateOnly data, CancellationToken cancellationToken);

    /// <summary>
    /// Lista versões vivas paginadas por cursor keyset bidirecional
    /// (ADR-0026 + ADR-0089): ordena por <c>Id</c> (Guid v7, ADR-0032) e devolve
    /// as âncoras de <c>prev</c>/<c>next</c> (nulas quando não há aquele lado).
    /// </summary>
    /// <param name="vigentes">
    /// Quando verdadeiro, devolve apenas as versões cuja janela contém a data de
    /// hoje. É o default do endpoint público: uma versão com vigência futura é
    /// planejamento normativo ainda não anunciado, e listá-la a qualquer cliente
    /// o divulgaria antes do ato que a institui.
    /// </param>
    Task<(IReadOnlyList<TipoAtoPublicado> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId,
        int limit,
        PaginationDirection direction,
        bool vigentes,
        CancellationToken cancellationToken);

    Task AdicionarAsync(TipoAtoPublicado tipo, CancellationToken cancellationToken);

    /// <summary>
    /// Marca a versão do tipo para remoção; o <c>SoftDeleteInterceptor</c>
    /// converte em soft-delete preenchendo <c>DeletedBy</c>/<c>DeletedAt</c>, o
    /// que libera a janela de vigência para uma nova versão do mesmo código.
    /// </summary>
    void Remover(TipoAtoPublicado tipo);

    /// <summary>
    /// Verifica se alguma versão viva de <paramref name="codigo"/> intercepta a
    /// janela semiaberta <c>[vigenciaInicio, vigenciaFim)</c>, ignorando
    /// <paramref name="excluirId"/> (a própria versão, na atualização).
    /// </summary>
    /// <remarks>
    /// Serve à mensagem de erro amigável, não à integridade: entre esta checagem e
    /// o <c>SaveChanges</c> cabe uma transação concorrente. Quem garante a
    /// não-sobreposição é a exclusion constraint do banco.
    /// </remarks>
    Task<bool> ExisteSobreposicaoDeVigenciaAsync(
        string codigo,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim,
        Guid? excluirId,
        CancellationToken cancellationToken);
}
