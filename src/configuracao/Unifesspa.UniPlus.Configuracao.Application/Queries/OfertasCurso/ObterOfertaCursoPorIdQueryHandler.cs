namespace Unifesspa.UniPlus.Configuracao.Application.Queries.OfertasCurso;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class ObterOfertaCursoPorIdQueryHandler
{
    public static async Task<Result<OfertaCursoDto>> Handle(
        ObterOfertaCursoPorIdQuery query,
        IOfertaCursoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        OfertaCurso? oferta = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return oferta is null
            ? Result<OfertaCursoDto>.Failure(new DomainError(OfertaCursoErrorCodes.NaoEncontrada, "Oferta de curso não encontrada."))
            : Result<OfertaCursoDto>.Success(oferta.ToDto());
    }
}
