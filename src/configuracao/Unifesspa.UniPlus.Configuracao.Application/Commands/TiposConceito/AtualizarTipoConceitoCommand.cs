namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// O código não integra o comando porque é imutável. Os valores categóricos enviados substituem em bloco os
/// que o tipo tinha.
/// </remarks>
public sealed record AtualizarTipoConceitoCommand(
    Guid Id,
    string? Nome,
    string? Descricao,
    string? Natureza,
    EscalaNumericaCommand? Numerico,
    IReadOnlyList<ValorCategoricoCommand>? Valores) : ICommand<Result>;
