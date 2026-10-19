namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

/// <summary>
/// Corpo de <see cref="Controllers.ProdutosPrevistosDoProcessoController.Publicar"/> — omite o processo e o
/// item, que vêm da rota. O tipo do ato não é declarado: sai do item que o edital congelou.
/// </summary>
/// <param name="Numero">Número que o documento declara, quando o tipo o tem.</param>
/// <param name="DataPublicacao">Data que o documento declara.</param>
/// <param name="Assinante">Quem assina o ato.</param>
/// <param name="DocumentoPrincipalId">O documento a publicar confirmado que é o principal, sempre PDF.</param>
/// <param name="Anexos">Os anexos, na ordem em que o ato os apresenta, até 20. Ausente ou vazio quando não há.</param>
/// <param name="PoliticaDeDivulgacaoConfirmada">
/// Confirma que o PDF segue a política de divulgação do certame que a versão vigente congelou
/// (<c>politicaDeDivulgacao</c> da consulta dos itens). Obrigatória quando o tipo do item é resultado, na
/// publicação e na correção; o sistema não confere o conteúdo do PDF.
/// </param>
/// <param name="AtoCorrigidoId">A publicação do item que este ato corrige, quando é errata ou retificação.</param>
/// <param name="MotivoDaCorrecao">Motivo da correção; obrigatório junto com <paramref name="AtoCorrigidoId"/>.</param>
public sealed record PublicarProdutoDoEditalRequest(
    string? Numero,
    DateOnly DataPublicacao,
    string Assinante,
    Guid DocumentoPrincipalId,
    IReadOnlyList<AnexoDoAtoRequest>? Anexos = null,
    bool PoliticaDeDivulgacaoConfirmada = false,
    Guid? AtoCorrigidoId = null,
    string? MotivoDaCorrecao = null);
