namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

/// <summary>
/// Os documentos que o candidato envia para atender as exigências da inscrição: a apresentação e os
/// arquivos dela. Implementa também a limpeza dos pendentes vencidos.
/// </summary>
public interface IDocumentoDaInscricaoRepository : IArquivoEnviadoRepository
{
    Task<ApresentacaoDeDocumento?> ObterApresentacaoAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ArquivoDaApresentacao?> ObterArquivoAsync(Guid id, CancellationToken cancellationToken = default);

    Task AdicionarApresentacaoAsync(ApresentacaoDeDocumento apresentacao, CancellationToken cancellationToken = default);

    Task AdicionarArquivoAsync(ArquivoDaApresentacao arquivo, CancellationToken cancellationToken = default);

    void Atualizar(ArquivoDaApresentacao arquivo);

    /// <summary>
    /// Remove o arquivo e, se a apresentação ficou sem nenhum, a própria apresentação.
    /// </summary>
    Task RemoverArquivoAsync(ArquivoDaApresentacao arquivo, CancellationToken cancellationToken = default);

    /// <summary>Os arquivos de todas as apresentações da inscrição, sem rastreamento.</summary>
    Task<IReadOnlyList<ArquivoDaApresentacao>> ListarArquivosDaInscricaoAsync(
        Guid inscricaoId, CancellationToken cancellationToken = default);
}
