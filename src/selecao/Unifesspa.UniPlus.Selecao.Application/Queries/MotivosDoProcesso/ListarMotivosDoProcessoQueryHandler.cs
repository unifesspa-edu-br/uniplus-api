namespace Unifesspa.UniPlus.Selecao.Application.Queries.MotivosDoProcesso;

using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Handler convention-based da leitura dos motivos escolhidos pelo processo.</summary>
public static class ListarMotivosDoProcessoQueryHandler
{
    public static async Task<IReadOnlyList<MotivoDoProcessoDto>> Handle(
        ListarMotivosDoProcessoQuery query,
        IMotivoDoProcessoRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(repository);

        IReadOnlyList<MotivoDoProcesso> escolhidos = await repository.ListarDoProcessoAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return [.. escolhidos
            .OrderBy(static e => e.Tipo)
            .ThenBy(static e => e.Codigo.Valor, StringComparer.Ordinal)
            .Select(MotivoDeDecisaoMapping.ToDto)];
    }
}
