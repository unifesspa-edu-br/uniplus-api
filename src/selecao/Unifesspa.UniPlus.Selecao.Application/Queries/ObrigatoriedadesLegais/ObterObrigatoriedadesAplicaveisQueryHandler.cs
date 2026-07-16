namespace Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

using System.Collections.Generic;
using System.Linq;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Resolve regras universais e regras do tipo exato do processo, ambas
/// vigentes em <see cref="ObterObrigatoriedadesAplicaveisQuery.DataReferencia"/>.
/// Não lê relógio: a data jurídica é decisão do chamador (ADR-0114).
/// </summary>
public static class ObterObrigatoriedadesAplicaveisQueryHandler
{
    public static async Task<IReadOnlyList<ObrigatoriedadeLegalDto>> Handle(
        ObterObrigatoriedadesAplicaveisQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(obrigatoriedadeLegalRepository);

        ProcessoSeletivo? processo = await processoSeletivoRepository
            .ObterPorIdAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (processo is null)
        {
            return [];
        }

        IReadOnlyList<ObrigatoriedadeLegal> regras = await obrigatoriedadeLegalRepository
            .ObterVigentesParaTipoProcessoAsync(
                processo.Tipo.ToString(),
                query.DataReferencia,
                cancellationToken)
            .ConfigureAwait(false);

        return [.. regras.Select(ObrigatoriedadeLegalMapping.ToDto)];
    }
}
