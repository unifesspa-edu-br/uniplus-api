namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Queries.Instituicoes;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.DTOs;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Mappings;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;

public static class ObterInstituicaoQueryHandler
{
    public static async Task<InstituicaoDto?> Handle(
        ObterInstituicaoQuery query,
        IInstituicaoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        Instituicao? instituicao = await repository
            .ObterParaLeituraAsync(cancellationToken)
            .ConfigureAwait(false);

        return instituicao?.ToDto();
    }
}
