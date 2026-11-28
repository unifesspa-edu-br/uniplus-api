namespace Unifesspa.UniPlus.Selecao.API.Contracts.Requests;

using System.ComponentModel;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

/// <summary>
/// Corpo de <see cref="Controllers.ProdutosPrevistosDoProcessoController.Publicar"/> — omite o processo e o
/// item, que vêm da rota. O tipo do ato não é declarado: sai do item que o edital congelou.
/// </summary>
/// <param name="Numero">Número que o documento declara, quando o tipo o tem.</param>
/// <param name="DataDoAto">Data que o documento declara; não conta para prazo, vigência nem escolha do tipo de ato.</param>
/// <param name="Assinante">Quem assina o ato. Obrigatório: ausente ou nulo é recusado (422).</param>
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
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    DateOnly? DataDoAto,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    string? Assinante,
    [property: ObrigatorioNoContrato]
    [property: Description("Obrigatório; ausente ou nulo é recusado (422).")]
    Guid? DocumentoPrincipalId,
    IReadOnlyList<AnexoDoAtoRequest>? Anexos = null,
    bool PoliticaDeDivulgacaoConfirmada = false,
    Guid? AtoCorrigidoId = null,
    string? MotivoDaCorrecao = null)
{
    internal PublicarProdutoDoEditalCommand ParaComando(Guid processoSeletivoId, Guid produtoId) => new(
        processoSeletivoId, produtoId, Numero, DataDoAto, Assinante, DocumentoPrincipalId,
        AnexoDoAtoRequest.ParaComando(Anexos), PoliticaDeDivulgacaoConfirmada, AtoCorrigidoId, MotivoDaCorrecao);
}
