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
/// <param name="Selo">
/// Selo do conteúdo emitido pela origem, tal como recebido. Nulo quando a origem não o enviou.
/// </param>
public sealed record CertameDaOrigem(
    Guid ProcessoSeletivoId,
    Guid AtoCriadorId,
    string Nome,
    string? NumeroEdital,
    Uri? DocumentoEditalUrl,
    string? Selo);
