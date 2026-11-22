namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Os critérios que distinguem as apresentações da mesma exigência documental.</summary>
public sealed record DistincaoDasApresentacoes
{
    public CriterioDeDistincaoDasApresentacoes? Criterio { get; private init; }
    public DateOnly? DataReferencia { get; private init; }
    public IReadOnlyList<string>? OcorrenciasEsperadas { get; private init; }

    private DistincaoDasApresentacoes() { }

    public static Result<DistincaoDasApresentacoes?> Declarar(
        CriterioDeDistincaoDasApresentacoes? criterio, DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas, int quantidadeMinima)
    {
        List<FieldError> recusas = ConferenciaDaDistincao.Conferir(new(criterio, dataReferencia, ocorrenciasEsperadas), quantidadeMinima);
        return recusas.Count > 0 ? Result<DistincaoDasApresentacoes?>.Failure(recusas[0].Error)
            : Result<DistincaoDasApresentacoes?>.Success(Reidratar(criterio, dataReferencia, ocorrenciasEsperadas));
    }

    public static DistincaoDasApresentacoes? Reidratar(CriterioDeDistincaoDasApresentacoes? criterio, DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas) =>
        (criterio is null or CriterioDeDistincaoDasApresentacoes.Nenhuma) && dataReferencia is null && ocorrenciasEsperadas is null ? null : new()
        {
            Criterio = criterio == CriterioDeDistincaoDasApresentacoes.Nenhuma ? null : criterio,
            DataReferencia = dataReferencia,
            OcorrenciasEsperadas = ocorrenciasEsperadas is null ? null : Array.AsReadOnly(ocorrenciasEsperadas.ToArray()),
        };

    public bool ConferivelPelaContagem => Criterio is null || Criterio == CriterioDeDistincaoDasApresentacoes.Ocorrencia && OcorrenciasEsperadas is null;

    public bool AtendidaPor(IReadOnlyList<ApresentacaoDocumento> apresentacoes, int quantidadeMinima)
    {
        ArgumentNullException.ThrowIfNull(apresentacoes);
        List<ApresentacaoDocumento> distintas = [.. apresentacoes.DistinctBy(static a => a.Id)];
        return Criterio is null ? distintas.Count >= quantidadeMinima : ConferivelPelaContagem
            ? distintas.Select(static a => a.IdentificacaoDaApresentacao).Where(static c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal).Count() >= quantidadeMinima
            : IdentificacoesDasApresentacoesExigidas(quantidadeMinima)!.All(identificacao => distintas.Any(a => a.IdentificacaoDaApresentacao == identificacao));
    }

    public IReadOnlyList<string>? IdentificacoesDasApresentacoesExigidas(int quantidadeMinima) => Criterio switch
    {
        CriterioDeDistincaoDasApresentacoes.CompetenciaMensal => ComputarCompetencias(DataReferencia!.Value, quantidadeMinima),
        CriterioDeDistincaoDasApresentacoes.ExercicioAnual => ComputarExercicios(DataReferencia!.Value, quantidadeMinima),
        CriterioDeDistincaoDasApresentacoes.Ocorrencia => OcorrenciasEsperadas,
        _ => null,
    };

    /// <summary>As N competências mensais ("AAAA-MM") regulares imediatamente ≤ <paramref name="dataReferencia"/>, do mais recente ao mais antigo — <see cref="DateOnly.AddMonths"/> já resolve virada de ano.</summary>
    private static List<string> ComputarCompetencias(DateOnly dataReferencia, int quantidade)
    {
        List<string> competencias = new(quantidade);
        DateOnly cursor = dataReferencia;
        for (int i = 0; i < quantidade; i++)
        {
            competencias.Add($"{cursor.Year:D4}-{cursor.Month:D2}");
            if (i < quantidade - 1)
            {
                cursor = cursor.AddMonths(-1);
            }
        }

        return competencias;
    }

    /// <summary>Os N exercícios anuais ("AAAA") regulares imediatamente ≤ <paramref name="dataReferencia"/>, do mais recente ao mais antigo.</summary>
    private static List<string> ComputarExercicios(DateOnly dataReferencia, int quantidade)
    {
        List<string> exercicios = new(quantidade);
        for (int i = 0; i < quantidade; i++)
        {
            exercicios.Add($"{dataReferencia.Year - i:D4}");
        }

        return exercicios;
    }

}
