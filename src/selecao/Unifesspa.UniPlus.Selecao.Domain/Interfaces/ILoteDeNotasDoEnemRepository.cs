namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

/// <summary>
/// Repositório dos lotes de importação das notas do ENEM. Só acrescenta: um lote é evidência
/// append-only, então não há caminho para atualizar ou apagar.
/// </summary>
public interface ILoteDeNotasDoEnemRepository
{
    Task AdicionarAsync(LoteDeNotasDoEnem lote, CancellationToken cancellationToken = default);

    /// <summary>O lote vigente do escopo, sem os filhos, ou nulo se nada foi importado.</summary>
    Task<LoteDeNotasDoEnem?> ObterVigenteAsync(
        Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo, CancellationToken cancellationToken = default);

    /// <summary>O lote do escopo com as ocorrências, sem rastreamento, ou nulo.</summary>
    Task<LoteDeNotasDoEnem?> ObterComOcorrenciasAsync(
        Guid processoSeletivoId,
        Guid etapaProcessoId,
        string edicaoCodigo,
        Guid loteId,
        CancellationToken cancellationToken = default);

    /// <summary>Os lotes do escopo, sem os filhos, do mais recente para o mais antigo.</summary>
    Task<IReadOnlyList<LoteDeNotasDoEnem>> ListarAsync(
        Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo, CancellationToken cancellationToken = default);

    /// <summary>O número de inscrição do Uni+ de cada inscrição pedida, para legendar as ocorrências.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ObterNumerosDeInscricaoAsync(
        IReadOnlyCollection<Guid> inscricaoIds, CancellationToken cancellationToken = default);
}
