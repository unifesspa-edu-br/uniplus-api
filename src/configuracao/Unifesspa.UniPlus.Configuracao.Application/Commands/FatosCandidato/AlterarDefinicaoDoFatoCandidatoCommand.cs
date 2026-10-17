namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Altera os eixos do fato que o administrador cadastrou (ADR-0136): o código, a origem e o
/// vínculo não mudam. Os eixos chegam como token canônico UPPER_SNAKE.
/// </summary>
public sealed record AlterarDefinicaoDoFatoCandidatoCommand(Guid Id, DefinicaoDoFatoInput Definicao) : ICommand<Result>;
