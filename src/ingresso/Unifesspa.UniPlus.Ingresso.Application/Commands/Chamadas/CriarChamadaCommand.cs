namespace Unifesspa.UniPlus.Ingresso.Application.Commands.Chamadas;

using MediatR;

using Unifesspa.UniPlus.Kernel.Results;

public sealed record CriarChamadaCommand(
    Guid EditalId,
    DateTimeOffset DataPublicacao,
    DateTimeOffset PrazoManifestacao) : IRequest<Result<Guid>>;
