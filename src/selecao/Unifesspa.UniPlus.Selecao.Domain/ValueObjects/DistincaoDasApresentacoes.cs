namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Os critérios que distinguem as apresentações da mesma exigência documental.</summary>
public sealed record DistincaoDasApresentacoes
{
    public ChaveDistincao? Chave { get; private init; }
    public DateOnly? DataReferencia { get; private init; }
    public IReadOnlyList<string>? OcorrenciasEsperadas { get; private init; }

    private DistincaoDasApresentacoes() { }

    public static Result<DistincaoDasApresentacoes?> Declarar(
        ChaveDistincao? chave, DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas, int quantidadeMinima)
    {
        List<FieldError> recusas = ConferenciaDaDistincao.Conferir(new(chave, dataReferencia, ocorrenciasEsperadas), quantidadeMinima);
        return recusas.Count > 0 ? Result<DistincaoDasApresentacoes?>.Failure(recusas[0].Error)
            : Result<DistincaoDasApresentacoes?>.Success(Reidratar(chave, dataReferencia, ocorrenciasEsperadas));
    }

    public static DistincaoDasApresentacoes? Reidratar(ChaveDistincao? chave, DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas) =>
        (chave is null or ChaveDistincao.Nenhuma) && dataReferencia is null && ocorrenciasEsperadas is null ? null : new()
        {
            Chave = chave == ChaveDistincao.Nenhuma ? null : chave,
            DataReferencia = dataReferencia,
            OcorrenciasEsperadas = ocorrenciasEsperadas is null ? null : Array.AsReadOnly(ocorrenciasEsperadas.ToArray()),
        };

    public bool ConferivelPelaContagem => Chave is null || Chave == ChaveDistincao.Ocorrencia && OcorrenciasEsperadas is null;

    public bool AtendidaPor(IReadOnlyList<ApresentacaoDocumento> apresentacoes, int quantidadeMinima)
    {
        ArgumentNullException.ThrowIfNull(apresentacoes);
        List<ApresentacaoDocumento> distintas = [.. apresentacoes.DistinctBy(static a => a.Id)];
        return Chave is null ? distintas.Count >= quantidadeMinima : ConferivelPelaContagem
            ? distintas.Select(static a => a.ChaveDistincao).Where(static c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal).Count() >= quantidadeMinima
            : IdentificacoesDasApresentacoesExigidas(quantidadeMinima)!.All(slot => distintas.Any(a => a.ChaveDistincao == slot));
    }

    public IReadOnlyList<string>? IdentificacoesDasApresentacoesExigidas(int quantidadeMinima) => Chave switch
    {
        ChaveDistincao.CompetenciaMensal => ComputarCompetencias(DataReferencia!.Value, quantidadeMinima),
        ChaveDistincao.ExercicioAnual => ComputarExercicios(DataReferencia!.Value, quantidadeMinima),
        ChaveDistincao.Ocorrencia => OcorrenciasEsperadas,
        _ => null,
    };

    /// <summary>As N competências mensais ("AAAA-MM") regulares imediatamente ≤ <paramref name="ancora"/>, do mais recente ao mais antigo — <see cref="DateOnly.AddMonths"/> já resolve virada de ano.</summary>
    private static List<string> ComputarCompetencias(DateOnly ancora, int quantidade)
    {
        List<string> competencias = new(quantidade);
        DateOnly cursor = ancora;
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

    /// <summary>Os N exercícios anuais ("AAAA") regulares imediatamente ≤ <paramref name="ancora"/>, do mais recente ao mais antigo.</summary>
    private static List<string> ComputarExercicios(DateOnly ancora, int quantidade)
    {
        List<string> exercicios = new(quantidade);
        for (int i = 0; i < quantidade; i++)
        {
            exercicios.Add($"{ancora.Year - i:D4}");
        }

        return exercicios;
    }

}
