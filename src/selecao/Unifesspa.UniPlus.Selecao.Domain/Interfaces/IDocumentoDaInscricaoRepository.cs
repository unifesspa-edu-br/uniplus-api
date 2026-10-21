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
    /// Agenda a limpeza de uma chave do armazenamento a partir de <paramref name="limparApos"/>. A URL
    /// pré-assinada de envio vale até o prazo e um PUT nela recria o objeto em claro, então a chave de
    /// envio de um arquivo removido, descartado ou confirmado só se apaga de vez depois dele.
    /// </summary>
    Task AgendarLimpezaAsync(string objectKey, DateTimeOffset limparApos, CancellationToken cancellationToken = default);

    /// <summary>Até <paramref name="limite"/> chaves agendadas cuja hora já passou em <paramref name="agora"/>, sem rastreamento.</summary>
    Task<IReadOnlyList<ChaveALimpar>> ListarChavesVencidasAsync(DateTimeOffset agora, int limite, CancellationToken cancellationToken = default);

    /// <summary>Tira a chave da agenda depois de apagada do armazenamento.</summary>
    Task RemoverChaveAgendadaAsync(Guid id, CancellationToken cancellationToken = default);
}
