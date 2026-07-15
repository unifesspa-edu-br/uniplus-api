namespace Unifesspa.UniPlus.Configuracao.Application.Commands.PrecedenciasFase;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Remove (soft-delete) uma aresta de precedência pelo seu <c>Id</c>.</summary>
public sealed record RemoverPrecedenciaFaseCommand(Guid Id) : ICommand<Result>;
