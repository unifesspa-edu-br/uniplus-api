namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A decisão do Representante do controlador sobre a proteção de dados de um tipo de documento ou fato do candidato que
/// está numa solicitação (ADR-0153): aprova a proteção declarada, recomenda o ajuste indicando a classificação correta,
/// ou a reprova com a justificativa.
/// </summary>
/// <param name="Item">O cadastro e o identificador do item.</param>
/// <param name="Decisao">A decisão, a classificação recomendada e a justificativa.</param>
/// <param name="ProtecaoConferida">A proteção que o Representante do controlador conferiu.</param>
/// <param name="Lida">O ciclo e a contagem de manifestações que ele leu no item.</param>
/// <remarks>
/// As partes chegam como o corpo as informa, com campos anuláveis, para que o ausente seja recusado no próprio campo,
/// junto das demais violações, num único lote (ADR-0125): quem as lê é <see cref="PedidoDeDecisao"/>. A decisão vale
/// sobre a proteção e o circuito que o Representante do controlador leu: se o item foi editado, ou recebeu manifestação,
/// depois disso, a decisão responde conflito.
/// </remarks>
public sealed record DecidirProtecaoDoCatalogoCommand(
    ItemInformado Item,
    DecisaoInformada Decisao,
    ProtecaoInformada ProtecaoConferida,
    LeituraInformada Lida) : ICommand<Result>;
