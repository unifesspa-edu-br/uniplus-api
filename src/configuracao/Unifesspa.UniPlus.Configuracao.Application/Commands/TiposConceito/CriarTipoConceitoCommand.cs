namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// Os textos são <c>string?</c>, não <c>string</c> (ADR-0125): o model binding do <c>[ApiController]</c>
/// interceptaria um campo ausente com um 400 genérico antes de o domínio rodar. O <c>Numerico</c> só vale
/// para a natureza <c>NUMERICO</c>, e os <c>Valores</c>, para a <c>CATEGORICO</c>.
/// A <c>Natureza</c> é o <c>codigo</c> da natureza, como o vocabulário <c>naturezas-conceito</c> o publica.
/// </remarks>
public sealed record CriarTipoConceitoCommand(
    string? Codigo,
    string? Nome,
    string? Descricao,
    [AceitaCodigosDe<NaturezaConceito>] string? Natureza,
    EscalaNumericaCommand? Numerico,
    IReadOnlyList<ValorCategoricoCommand>? Valores) : ICommand<Result<Guid>>;
