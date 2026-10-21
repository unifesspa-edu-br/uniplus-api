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

    /// <summary>
    /// Até <paramref name="limite"/> arquivos confirmados cuja URL de envio já venceu em <paramref name="agora"/> e cuja
    /// chave de envio ainda não foi limpa, sem rastreamento.
    /// </summary>
    Task<IReadOnlyList<ArquivoPendenteVencido>> ListarEnviosConfirmadosAptosALimpezaAsync(
        DateTimeOffset agora, int limite, CancellationToken cancellationToken = default);

    /// <summary>Marca a chave de envio do arquivo confirmado como limpa, sem passar pelo change tracker.</summary>
    Task MarcarEnvioLimpoAsync(Guid id, DateTimeOffset agora, CancellationToken cancellationToken = default);
}
