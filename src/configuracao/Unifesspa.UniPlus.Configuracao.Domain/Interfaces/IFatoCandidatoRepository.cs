namespace Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Escrita e leitura de manutenção do catálogo de fatos do candidato (ADR-0136).</summary>
public interface IFatoCandidatoRepository
{
    /// <summary>O fato com os valores de domínio, rastreado para mutação.</summary>
    Task<FatoCandidato?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    Task<FatoCandidato?> ObterPorIdParaLeituraAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lista paginada por cursor, com filtro opcional por origem e por ativo.</summary>
    Task<(IReadOnlyList<FatoCandidato> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
        Guid? afterId, int limit, PaginationDirection direction, OrigemFato? origem, bool? ativo, CancellationToken cancellationToken);

    Task AdicionarAsync(FatoCandidato fato, CancellationToken cancellationToken);

    /// <summary>Se o código já é usado por algum fato, ativo ou não: o código nunca é reutilizado.</summary>
    Task<bool> CodigoExisteAsync(string codigo, CancellationToken cancellationToken);

    /// <summary>O catálogo inteiro, com os valores de domínio, sem rastreamento.</summary>
    Task<IReadOnlyList<FatoCandidato>> ListarTodosAsync(CancellationToken cancellationToken);

    /// <summary>Serializa, até o fim da transação, as escritas de regras padrão do catálogo.</summary>
    Task TravarRegrasPadraoParaEscritaAsync(CancellationToken cancellationToken);
}
