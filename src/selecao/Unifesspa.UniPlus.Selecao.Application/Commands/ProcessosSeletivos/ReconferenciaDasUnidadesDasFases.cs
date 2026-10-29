namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Reconfere, ao congelar, a unidade responsável de cada fase do cronograma contra o cadastro vivo
/// da Organização Institucional: a unidade excluída depois de o cronograma ser definido recusa a
/// publicação, e a que foi renomeada entra no snapshot com a sigla e o nome de agora. O cadastro
/// vivo vale a cada publicação, inclusive a que divulga uma retificação, e a cópia de todas as
/// fases é refeita a partir dele.
/// </summary>
internal static class ReconferenciaDasUnidadesDasFases
{
    /// <summary>
    /// Recusa, no mesmo lote, toda fase cuja unidade não existe mais (ADR-0125). Sem nenhuma recusa,
    /// manda o agregado recopiar as unidades; com recusa, o agregado não muda. A unidade é
    /// resolvida uma vez por id.
    /// </summary>
    public static async Task<Result> AplicarAsync(
        ProcessoSeletivo processo,
        IResolvedorDaUnidadeResponsavel resolvedor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(resolvedor);

        Dictionary<Guid, UnidadeResponsavelSnapshot?> resolvidas = [];
        List<FieldError> erros = [];
        FaseCronograma[] fases = [.. processo.CronogramaFases.OrderBy(static f => f.Ordem)];
        for (int indice = 0; indice < fases.Length; indice++)
        {
            if (fases[indice].UnidadeResponsavelOrigemId is not { } unidadeId)
            {
                continue;
            }

            if (!resolvidas.TryGetValue(unidadeId, out UnidadeResponsavelSnapshot? viva))
            {
                viva = await resolvedor.ResolverAsync(unidadeId, cancellationToken).ConfigureAwait(false);
                resolvidas[unidadeId] = viva;
            }

            if (viva is null)
            {
                erros.Add(new FieldError($"fases[{indice}].unidadeResponsavelId", new DomainError(
                    "FaseCronograma.UnidadeResponsavelNaoEncontrada",
                    $"Unidade responsável {unidadeId} da fase '{fases[indice].Codigo}' não encontrada ou não está mais viva. Escolha outra unidade para a fase.")));
            }
        }

        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        processo.RecopiarUnidadesDasFases(resolvidas.ToDictionary(static p => p.Key, static p => p.Value!));
        return Result.Success();
    }

    /// <summary>Soma as recusas dos dois resultados num só, para o operador ver tudo de uma vez.</summary>
    public static Result Combinar(Result primeiro, Result segundo)
    {
        ArgumentNullException.ThrowIfNull(primeiro);
        ArgumentNullException.ThrowIfNull(segundo);

        return primeiro.IsSuccess && segundo.IsSuccess
            ? Result.Success()
            : Result.ValidationFailure([.. primeiro.Errors, .. segundo.Errors]);
    }
}
