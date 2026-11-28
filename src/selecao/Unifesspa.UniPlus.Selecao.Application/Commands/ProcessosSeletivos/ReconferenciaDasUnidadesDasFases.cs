namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ProcessosSeletivos.Ordenacao;
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
    /// Recusa, no mesmo lote, toda fase cuja unidade não existe mais (ADR-0125). Sem recusa, devolve as
    /// unidades do cadastro atual que o agregado copia para as fases; o agregado não muda aqui. A unidade é resolvida
    /// uma vez por id.
    /// </summary>
    public static async Task<Result<IReadOnlyDictionary<Guid, UnidadeResponsavelSnapshot>>> ConferirAsync(
        ProcessoSeletivo processo,
        IResolvedorDaUnidadeResponsavel resolvedor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(resolvedor);

        Dictionary<Guid, UnidadeResponsavelSnapshot?> resolvidas = [];
        List<FieldError> erros = await ResolverAsUnidadesDasFasesAsync(processo, resolvedor, resolvidas, cancellationToken).ConfigureAwait(false);

        return erros.Count > 0
            ? Result<IReadOnlyDictionary<Guid, UnidadeResponsavelSnapshot>>.ValidationFailure(erros)
            : Result<IReadOnlyDictionary<Guid, UnidadeResponsavelSnapshot>>.Success(resolvidas.ToDictionary(static p => p.Key, static p => p.Value!));
    }

    private static async Task<List<FieldError>> ResolverAsUnidadesDasFasesAsync(
        ProcessoSeletivo processo,
        IResolvedorDaUnidadeResponsavel resolvedor,
        Dictionary<Guid, UnidadeResponsavelSnapshot?> resolvidas,
        CancellationToken cancellationToken)
    {
        List<FieldError> erros = [];
        FaseCronograma[] fases = [.. OrdemDaConfiguracao.Fases(processo.CronogramaFases)];
        for (int indice = 0; indice < fases.Length; indice++)
        {
            if (fases[indice].UnidadeResponsavel?.OrigemId is { } unidadeId
                && await ResolverUmaVezAsync(unidadeId, resolvedor, resolvidas, cancellationToken).ConfigureAwait(false) is null)
            {
                erros.Add(UnidadeNaoEncontrada(indice, fases[indice]));
            }
        }

        return erros;
    }

    private static async Task<UnidadeResponsavelSnapshot?> ResolverUmaVezAsync(
        Guid unidadeId,
        IResolvedorDaUnidadeResponsavel resolvedor,
        Dictionary<Guid, UnidadeResponsavelSnapshot?> resolvidas,
        CancellationToken cancellationToken)
    {
        if (!resolvidas.TryGetValue(unidadeId, out UnidadeResponsavelSnapshot? atual))
        {
            atual = await resolvedor.ResolverAsync(unidadeId, cancellationToken).ConfigureAwait(false);
            resolvidas[unidadeId] = atual;
        }

        return atual;
    }

    private static FieldError UnidadeNaoEncontrada(int indice, FaseCronograma fase) => new(
        $"fases[{indice}].unidadeResponsavel",
        new DomainError(
            "FaseCronograma.UnidadeResponsavelNaoEncontrada",
            $"A unidade responsável '{fase.UnidadeResponsavel!.Sigla}' da fase {fase.Citacao} não existe no cadastro de unidades ou foi removida. Escolha outra unidade para a fase."));
}
