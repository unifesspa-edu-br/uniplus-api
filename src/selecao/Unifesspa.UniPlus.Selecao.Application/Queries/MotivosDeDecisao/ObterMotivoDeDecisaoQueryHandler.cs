namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDeDecisao;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Handler convention-based da leitura de um motivo pelo Id.</summary>
public static class ObterMotivoDeDecisaoQueryHandler
{
    public static async Task<MotivoDeDecisaoDto?> Handle(
        ObterMotivoDeDecisaoQuery query,
        IMotivoDeDecisaoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        MotivoDeDecisao? motivo = await repository.ObterPorIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
        return motivo is null ? null : MotivoDeDecisaoMapping.ToDto(motivo);
    }
}
