namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// O código não integra o comando porque é imutável. Os valores categóricos enviados substituem em bloco os
/// que o tipo tinha. A <c>Natureza</c> é o <c>codigo</c> da natureza, como o vocabulário <c>naturezas-conceito</c>
/// o publica.
/// </remarks>
public sealed record AtualizarTipoConceitoCommand(
    Guid Id,
    string? Nome,
    string? Descricao,
    [AceitaCodigosDe<NaturezaConceito>] string? Natureza,
    EscalaNumericaCommand? Numerico,
    IReadOnlyList<ValorCategoricoCommand>? Valores) : ICommand<Result>;
