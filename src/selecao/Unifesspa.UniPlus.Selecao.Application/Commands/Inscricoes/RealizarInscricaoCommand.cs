namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using MediatR;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.SharedKernel.Results;

public sealed record RealizarInscricaoCommand(
    Guid CandidatoId,
    Guid EditalId,
    ModalidadeConcorrencia Modalidade,
    string CodigoCursoPrimeiraOpcao,
    string? CodigoCursoSegundaOpcao = null,
    bool ListaEspera = false) : IRequest<Result<Guid>>;
