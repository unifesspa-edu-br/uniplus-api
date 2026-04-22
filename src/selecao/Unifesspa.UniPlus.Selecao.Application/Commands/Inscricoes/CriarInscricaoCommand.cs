namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using MediatR;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

public sealed record CriarInscricaoCommand(
    Guid CandidatoId,
    Guid EditalId,
    ModalidadeConcorrencia Modalidade,
    string CodigoCursoPrimeiraOpcao) : IRequest<Result<Guid>>;
