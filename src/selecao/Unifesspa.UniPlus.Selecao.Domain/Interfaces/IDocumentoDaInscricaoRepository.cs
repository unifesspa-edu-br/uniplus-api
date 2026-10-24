namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using Entities;

using Enums;

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

    /// <summary>Todas as apresentações da inscrição, confirmadas ou não, sem rastreamento.</summary>
    Task<IReadOnlyList<ApresentacaoDeDocumento>> ListarApresentacoesDaInscricaoAsync(
        Guid inscricaoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reivindica atomicamente a confirmação — <c>UPDATE ... WHERE id = @id AND situacao = Pendente</c>, sem passar
    /// pelo change tracker. A remoção do pendente vencido e a confirmação disputam o mesmo bloqueio de linha: quem
    /// chega depois encontra a condição falsa e afeta zero linhas, em vez de estourar concorrência na gravação.
    /// </summary>
    Task<bool> TentarReivindicarConfirmacaoAsync(
        Guid id, string etag, DateTimeOffset agora, DateTimeOffset proximaPublicacaoEm, CancellationToken cancellationToken = default);

    /// <summary>
    /// Conclui atomicamente a verificação — <c>UPDATE ... SET situacao = @situacao WHERE id = @id AND situacao = EmVerificacao</c>,
    /// sem passar pelo change tracker. Com resultados duplicados em réplicas distintas, só o primeiro afeta a linha: o
    /// segundo encontra a condição falsa, em vez de regravar colunas com uma leitura já velha.
    /// </summary>
    Task<bool> TentarConcluirVerificacaoAsync(
        Guid id, SituacaoDoArquivoDoCandidato situacao, CancellationToken cancellationToken = default);

    /// <summary>Devolve ao pendente o arquivo que acabou de ser reivindicado, quando a confirmação não pôde seguir.</summary>
    Task DesfazerReivindicacaoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Os arquivos de todas as apresentações da inscrição, sem rastreamento.</summary>
    Task<IReadOnlyList<ArquivoDaApresentacao>> ListarArquivosDaInscricaoAsync(
        Guid inscricaoId, CancellationToken cancellationToken = default);

    /// <summary>Os arquivos em verificação cuja próxima publicação já venceu em <paramref name="agora"/>, rastreados.</summary>
    Task<IReadOnlyList<ArquivoDaApresentacao>> ListarEmVerificacaoParaRepublicarAsync(
        DateTimeOffset agora, int limite, CancellationToken cancellationToken = default);

    /// <summary>Tenta obter, até o fim da transação, a exclusividade da reconciliação; quem não obtém desiste.</summary>
    Task<bool> TentarObterExclusividadeDaReconciliacaoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Agenda a remoção, a partir de <paramref name="limparApos"/>, de uma chave definitiva do arquivo do candidato no
    /// bucket definitivo. O bucket de envio não entra: a expiração dele cuida do que sobra.
    /// </summary>
    Task AgendarLimpezaAsync(string objectKey, DateTimeOffset limparApos, CancellationToken cancellationToken = default);

    /// <summary>Até <paramref name="limite"/> chaves agendadas cuja hora já passou em <paramref name="agora"/>, sem rastreamento.</summary>
    Task<IReadOnlyList<ChaveALimpar>> ListarChavesVencidasAsync(DateTimeOffset agora, int limite, CancellationToken cancellationToken = default);

    /// <summary>Tira a chave da agenda depois de apagada do armazenamento.</summary>
    Task RemoverChaveAgendadaAsync(Guid id, CancellationToken cancellationToken = default);
}
