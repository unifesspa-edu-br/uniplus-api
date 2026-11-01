namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A decisão da proteção de dados sobre um item do catálogo que está numa solicitação (ADR-0153): aprova a
/// proteção declarada, ou a nega e indica a classificação correta.
/// </summary>
/// <param name="Cadastro"><c>TIPO_DOCUMENTO</c> ou <c>FATO</c>.</param>
/// <param name="ItemId">Identificador do tipo de documento ou do fato.</param>
/// <param name="Decisao"><c>APROVADA</c> ou <c>NEGADA</c>.</param>
/// <param name="ClassificacaoProtecao">Token canônico da classificação que o aprovador conferiu, como a listagem a mostra.</param>
/// <param name="HipoteseLegal">Token canônico da hipótese legal que o aprovador conferiu.</param>
/// <param name="FinalidadeTratamento">A finalidade do tratamento que o aprovador conferiu.</param>
/// <param name="ClassificacaoRecomendada">Token canônico da classificação; obrigatório na negativa e ignorado na aprovação.</param>
/// <remarks>
/// <c>Cadastro</c>, <c>Decisao</c> e a proteção conferida são <c>string?</c> para que o campo ausente seja recusado no
/// próprio campo, junto das demais violações (ADR-0125). A decisão vale sobre a proteção que o aprovador viu: se o
/// item foi editado e reenviado depois disso, a proteção vigente difere da conferida e a decisão responde conflito.
/// </remarks>
public sealed record DecidirProtecaoDoCatalogoCommand(
    string? Cadastro,
    Guid ItemId,
    string? Decisao,
    string? ClassificacaoProtecao,
    string? HipoteseLegal,
    string? FinalidadeTratamento,
    string? ClassificacaoRecomendada = null) : ICommand<Result>;
