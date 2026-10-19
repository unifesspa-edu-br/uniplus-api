namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using DTOs;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Publica um item que a versão vigente do edital declara — o resultado preliminar da homologação, por
/// exemplo — com o PDF e os anexos, e, quando o ato corrige uma publicação anterior do mesmo item, a
/// indicação dela e o motivo (ADR-0103).
/// </summary>
/// <remarks>
/// O administrador escolhe o item, não o tipo do ato: o tipo vem do item que o edital congelou.
/// </remarks>
/// <param name="ProcessoSeletivoId">O processo que publica o item.</param>
/// <param name="ProdutoDoEditalId">O <c>Id</c> congelado do produto de fase ou de etapa que o ato cumpre.</param>
/// <param name="Numero">Número que o documento declara, quando o tipo o tem.</param>
/// <param name="DataPublicacao">Data que o documento declara.</param>
/// <param name="Assinante">Quem assina o ato.</param>
/// <param name="DocumentoPrincipalId">O documento a publicar confirmado que é o principal, sempre PDF.</param>
/// <param name="Anexos">Os anexos, na ordem em que o ato os apresenta; nulo ou vazio quando não há.</param>
/// <param name="PoliticaDeDivulgacaoConfirmada">
/// Se o administrador confirmou que o PDF segue a política de divulgação do certame que a versão vigente
/// congelou. Exigida quando o tipo do ato é resultado, na publicação e na correção.
/// </param>
/// <param name="AtoCorrigidoId">A publicação do item que este ato corrige; nulo na primeira publicação.</param>
/// <param name="MotivoDaCorrecao">Motivo da correção; vem junto com <paramref name="AtoCorrigidoId"/>.</param>
public sealed record PublicarProdutoDoEditalCommand(
    Guid ProcessoSeletivoId,
    Guid ProdutoDoEditalId,
    string? Numero,
    DateOnly DataPublicacao,
    string Assinante,
    Guid DocumentoPrincipalId,
    IReadOnlyList<AnexoSolicitadoDoAto>? Anexos = null,
    bool PoliticaDeDivulgacaoConfirmada = false,
    Guid? AtoCorrigidoId = null,
    string? MotivoDaCorrecao = null) : ICommand<Result<AtoPublicadoDto>>, IDadosDoAtoPublicado;
