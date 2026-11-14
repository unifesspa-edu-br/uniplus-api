namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// Os textos são <c>string?</c>, não <c>string</c> (ADR-0125): o model binding do <c>[ApiController]</c>
/// interceptaria um campo ausente com um 400 genérico antes de o domínio rodar. O <c>Numerico</c> só vale
/// para a natureza <c>numerico</c>, e os <c>Valores</c>, para a <c>categorico</c>.
/// </remarks>
public sealed record CriarTipoConceitoCommand(
    string? Codigo,
    string? Nome,
    string? Descricao,
    string? Natureza,
    EscalaNumericaCommand? Numerico,
    IReadOnlyList<ValorCategoricoCommand>? Valores) : ICommand<Result<Guid>>;
