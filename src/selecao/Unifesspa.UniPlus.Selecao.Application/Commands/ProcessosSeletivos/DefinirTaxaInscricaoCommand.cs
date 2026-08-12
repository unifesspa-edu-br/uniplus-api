namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Define (ou remove) a taxa de inscrição e os fundamentos de isenção do processo (issue
/// #1112). <c>Cobra</c> nulo remove a declaração — o processo volta a "ainda não declarado",
/// que BLOQUEIA a publicação (CA-01), diferente do toggle de <c>DefinirBonusRegionalCommand</c>
/// (onde ausência é estado publicável). <c>Fundamentos</c> não vazio exige
/// <c>ConfirmacaoFundamentos</c> == <see langword="true"/> (CA-06).
/// </summary>
public sealed record DefinirTaxaInscricaoCommand(
    Guid ProcessoSeletivoId,
    bool? Cobra,
    decimal? Valor,
    IReadOnlyList<string>? Fundamentos,
    bool ConfirmacaoFundamentos,
    PrecondicaoIfMatch Precondicao) : ICommand<Result<MutacaoAceita>>;
