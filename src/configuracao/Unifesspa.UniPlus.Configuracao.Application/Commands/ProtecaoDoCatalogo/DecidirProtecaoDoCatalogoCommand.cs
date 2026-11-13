namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A decisão da proteção de dados sobre um item do catálogo que está numa solicitação (ADR-0153): aprova a
/// proteção declarada, recomenda o ajuste indicando a classificação correta, ou a reprova com a justificativa.
/// </summary>
/// <param name="Cadastro"><c>TIPO_DOCUMENTO</c> ou <c>FATO</c>.</param>
/// <param name="ItemId">Identificador do tipo de documento ou do fato.</param>
/// <param name="Decisao"><c>APROVAR</c>, <c>RECOMENDAR_AJUSTE</c> ou <c>REPROVAR</c>.</param>
/// <param name="ClassificacaoProtecao">Token canônico da classificação que o aprovador conferiu, como a listagem a mostra.</param>
/// <param name="HipoteseLegal">Token canônico da hipótese legal que o aprovador conferiu.</param>
/// <param name="FinalidadeTratamento">A finalidade do tratamento que o aprovador conferiu.</param>
/// <param name="ClassificacaoRecomendada">Token canônico da classificação; obrigatório no ajuste e recusado nas demais decisões.</param>
/// <param name="Justificativa">
/// Motivo da decisão, de até 2.000 caracteres: obrigatório na reprovação; na aprovação e no ajuste, opcional, salvo
/// diante de manifestação técnica desfavorável vigente, que o exige (UNI-REQ-0374). Fica registrado no ato da decisão.
/// </param>
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
    string? ClassificacaoRecomendada = null,
    string? Justificativa = null) : ICommand<Result>;
