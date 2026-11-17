namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Unidades;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Mappings;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

public static class ObterUnidadePorIdQueryHandler
{
    public static async Task<Result<UnidadeDto>> Handle(
        ObterUnidadePorIdQuery query,
        IUnidadeRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        Unidade? unidade = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return unidade is null
            ? Result<UnidadeDto>.Failure(UnidadeHandlerErros.NaoEncontrada())
            : Result<UnidadeDto>.Success(unidade.ToDto());
    }
}
