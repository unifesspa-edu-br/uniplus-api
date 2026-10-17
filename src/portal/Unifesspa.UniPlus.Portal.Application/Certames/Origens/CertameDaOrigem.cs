namespace Unifesspa.UniPlus.Portal.Application.Certames.Origens;

/// <summary>
/// O que a Portal usa do detalhe público do certame servido por Seleção.
/// </summary>
/// <remarks>
/// Declarado aqui, e não reaproveitado do módulo Seleção: a Portal não referencia a camada de
/// aplicação de outro módulo, e trazer só os campos usados impede que um campo novo da origem
/// atravesse a página pública por omissão.
/// </remarks>
/// <param name="ProcessoSeletivoId">Chave com que a linha do tempo é pedida a Publicações.</param>
/// <param name="AtoCriadorId">
/// Ato normativo que criou a versão servida — é por ele que a Portal sabe se a linha do tempo
/// alcança o conteúdo que está mostrando.
/// </param>
/// <param name="Nome">Título do certame congelado na publicação.</param>
/// <param name="NumeroEdital">Número do edital como declarado na publicação, quando declarado.</param>
/// <param name="DocumentoEditalUrl">
/// Endereço público, no acervo, do edital da versão servida — o documento do ato criador. Nulo
/// quando a versão não tem documento publicado.
/// </param>
/// <param name="EditaisPorAto">
/// Endereço público, no acervo, do edital de cada versão já divulgada, pelo ato que a criou. Inclui
/// as versões anteriores à servida; vazio quando a origem não informa nenhuma.
/// </param>
/// <param name="Situacao">Situação da janela de inscrição no instante da leitura.</param>
/// <param name="DocumentosPorAto">
/// Os documentos no acervo de cada versão já divulgada — o edital e os anexos —, pelo ato que a
/// criou. Ausente ou sem o ato quando a origem não os informa.
/// </param>
public sealed record CertameDaOrigem(
    Guid ProcessoSeletivoId,
    Guid AtoCriadorId,
    string Nome,
    string? NumeroEdital,
    Uri? DocumentoEditalUrl,
    IReadOnlyDictionary<Guid, Uri> EditaisPorAto,
    SituacaoDaInscricao Situacao,
    IReadOnlyDictionary<Guid, IReadOnlyList<DocumentoDoEvento>>? DocumentosPorAto = null);
