namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <remarks>
/// O código é <c>string?</c>, não <c>string</c> (ADR-0125): o model binding do <c>[ApiController]</c> interceptaria um
/// campo ausente com um 400 genérico antes de o domínio rodar. A <c>Definicao</c> ausente equivale a uma definição com
/// todos os campos ausentes, e o domínio recusa cada um com o seu código.
/// </remarks>
public sealed record CriarTipoConceitoCommand(
    string? Codigo,
    DefinicaoDoTipoConceitoInput? Definicao) : ICommand<Result<Guid>>;
