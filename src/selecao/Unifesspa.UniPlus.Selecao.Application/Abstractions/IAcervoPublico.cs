namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Port de escrita no acervo público (ADR-0132): o bucket dedicado ao documento de ato publicado,
/// lido anonimamente por endereço imutável.
/// </summary>
/// <remarks>
/// <b>Caminho único de escrita.</b> Só a materialização da divulgação, que roda depois de o ato ser
/// registrado, depende deste port — é o que garante que nada fica público antes do ato. Documento
/// de candidato não tem caminho até aqui.
/// </remarks>
public interface IAcervoPublico
{
    /// <summary>
    /// Copia no armazenamento o arquivo confirmado, que está no bucket privado em
    /// <paramref name="chavePrivada"/>, para o acervo em <paramref name="chaveNoAcervo"/>, com o que a
    /// leitura anônima precisa dizer gravado no objeto. Objeto já presente no acervo não é regravado:
    /// a chave é derivada do conteúdo e da procedência, então ele é o resultado de uma cópia anterior.
    /// Falha do armazenamento lança <see cref="AcervoPublicoIndisponivelException"/>.
    /// </summary>
    Task CopiarAsync(
        string chavePrivada,
        string chaveNoAcervo,
        DocumentoNoAcervo documento,
        CancellationToken cancellationToken = default);
}

/// <summary>O documento como o acervo o apresenta, e de onde ele veio.</summary>
/// <param name="ContentType">Tipo do conteúdo.</param>
/// <param name="NomeDeApresentacao">Nome com que o arquivo é salvo por quem o baixa.</param>
/// <param name="AtoId">O ato que publicou o documento.</param>
/// <param name="ProcessoSeletivoId">O processo a que o documento se refere.</param>
/// <param name="HashSha256">Hash do conteúdo, o mesmo que o edital congelou.</param>
public sealed record DocumentoNoAcervo(
    string ContentType,
    string NomeDeApresentacao,
    Guid AtoId,
    Guid ProcessoSeletivoId,
    string HashSha256);
