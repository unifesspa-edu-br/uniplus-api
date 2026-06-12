namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Handler convention-based de <see cref="ObterObrigatoriedadeLegalQuery"/>.
/// Carrega a regra (filtrada por soft-delete via query filter padrão) e mapeia
/// para o DTO. A regra é cross-cutting por tipo de processo — sem proprietário
/// nem áreas de interesse.
/// </summary>
public static class ObterObrigatoriedadeLegalQueryHandler
{
    public static async Task<ObrigatoriedadeLegalDto?> Handle(
        ObterObrigatoriedadeLegalQuery query,
        IObrigatoriedadeLegalRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        ObrigatoriedadeLegal? regra = await repository
            .ObterPorIdAsync(query.Id, cancellationToken)
            .ConfigureAwait(false);

        return regra is null ? null : ObrigatoriedadeLegalMapping.ToDto(regra);
    }
}
