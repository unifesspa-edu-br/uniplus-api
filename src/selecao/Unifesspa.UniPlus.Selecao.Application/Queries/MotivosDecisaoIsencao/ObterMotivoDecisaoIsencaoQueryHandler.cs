namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDecisaoIsencao;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Handler convention-based da leitura de um motivo pelo Id.</summary>
public static class ObterMotivoDecisaoIsencaoQueryHandler
{
    public static async Task<MotivoDecisaoIsencaoDto?> Handle(
        ObterMotivoDecisaoIsencaoQuery query,
        IMotivoDecisaoIsencaoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        MotivoDecisaoIsencao? motivo = await repository
            .ObterPorIdAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return motivo is null ? null : MotivoDecisaoIsencaoMapping.ToDto(motivo);
    }
}
