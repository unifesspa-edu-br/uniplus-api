namespace Unifesspa.UniPlus.Ingresso.Application.Commands.Chamadas;

using MediatR;

using Unifesspa.UniPlus.SharedKernel.Results;

public sealed record CriarChamadaCommand(
    Guid EditalId,
    DateTimeOffset DataPublicacao,
    DateTimeOffset PrazoManifestacao) : IRequest<Result<Guid>>;
