namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.AtosNormativos;

using Unifesspa.UniPlus.Publicacoes.Application.Avisos;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Mappings;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

public static class ObterAtoNormativoPorIdQueryHandler
{
    public static async Task<AtoNormativoDto?> Handle(
        ObterAtoNormativoPorIdQuery query,
        IAtoNormativoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        AtoNormativo? ato = await repository
            .ObterPorIdParaLeituraAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);
        if (ato is null)
        {
            return null;
        }

        // Recomputa o aviso (AC4), excluindo o próprio ato do conjunto de conflitantes.
        IReadOnlyList<AvisoNumeracao> avisos = await AvisoNumeracaoCalculator
            .CalcularAsync(repository, ato, excluirId: ato.Id, cancellationToken)
            .ConfigureAwait(false);

        return ato.ToDto() with { Avisos = avisos };
    }
}
