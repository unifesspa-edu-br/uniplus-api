namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

/// <summary>Repositório do registro das gerações do arquivo de envio ao INEP. Só acrescenta.</summary>
public interface IGeracaoDeEnvioAoInepRepository
{
    Task AdicionarAsync(GeracaoDeEnvioAoInep geracao, CancellationToken cancellationToken = default);

    /// <summary>As gerações do escopo, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<GeracaoDeEnvioAoInep>> ListarAsync(
        Guid processoSeletivoId, Guid etapaProcessoId, string edicaoCodigo, CancellationToken cancellationToken = default);
}
