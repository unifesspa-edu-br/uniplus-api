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
        ChaveDistincao normalizada = chave ?? ChaveDistincao.Nenhuma;
        if (!Enum.IsDefined(normalizada))
        {
            return Result<DistincaoDasApresentacoes?>.Failure(new("NoExigencia.ChaveDistincaoInvalida",
                "Escolha um critério válido para distinguir as apresentações."));
        }
        DomainError? recusa = Recusa(normalizada, dataReferencia, ocorrenciasEsperadas, quantidadeMinima);
        return recusa is not null ? Result<DistincaoDasApresentacoes?>.Failure(recusa)
            : Result<DistincaoDasApresentacoes?>.Success(Reidratar(chave, dataReferencia, ocorrenciasEsperadas));
    }

    public static DistincaoDasApresentacoes? Reidratar(ChaveDistincao? chave, DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas) =>
        (chave is null or ChaveDistincao.Nenhuma) && dataReferencia is null && ocorrenciasEsperadas is null ? null : new()
        {
            Chave = chave == ChaveDistincao.Nenhuma ? null : chave,
            DataReferencia = dataReferencia,
            OcorrenciasEsperadas = ocorrenciasEsperadas is null ? null : Array.AsReadOnly(ocorrenciasEsperadas.ToArray()),
        };

    private static DomainError? Recusa(ChaveDistincao chave, DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas, int quantidadeMinima) => chave switch
    {
        ChaveDistincao.Nenhuma => ValidarSemChave(dataReferencia, ocorrenciasEsperadas),
        ChaveDistincao.CompetenciaMensal or ChaveDistincao.ExercicioAnual => ValidarChaveDeCalendario(chave, dataReferencia, ocorrenciasEsperadas, quantidadeMinima),
        ChaveDistincao.Ocorrencia => ValidarChaveDeOcorrencia(dataReferencia, ocorrenciasEsperadas, quantidadeMinima),
        _ => throw new ArgumentOutOfRangeException(nameof(chave)),
    };

    public bool ConferivelPelaContagem => Chave is null || Chave == ChaveDistincao.Ocorrencia && OcorrenciasEsperadas is null;

    public bool AtendidaPor(IReadOnlyList<ApresentacaoDocumento> apresentacoes, int quantidadeMinima)
    {
        ArgumentNullException.ThrowIfNull(apresentacoes);
        List<ApresentacaoDocumento> distintas = [.. apresentacoes.DistinctBy(static a => a.Id)];
        return Chave is null ? distintas.Count >= quantidadeMinima : ConferivelPelaContagem
            ? distintas.Select(static a => a.ChaveDistincao).Where(static c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal).Count() >= quantidadeMinima
            : SlotsEsperados(quantidadeMinima)!.All(slot => distintas.Any(a => a.ChaveDistincao == slot));
    }

    private static DomainError? ValidarSemChave(DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas)
    {
        if (dataReferencia is not null)
        {
            return new("NoExigencia.DataReferenciaIndevidaParaChave", "Informe a data de referência apenas para distinção por competência mensal ou exercício anual.");
        }
        return ocorrenciasEsperadas is not null ? OcorrenciasIndevidas() : null;
    }

    private static DomainError OcorrenciasIndevidas() => new(
        "NoExigencia.OcorrenciasEsperadasIndevidasParaChave", "Informe as ocorrências esperadas apenas para distinção por ocorrência.");

    private static DomainError? ValidarChaveDeCalendario(
        ChaveDistincao chave, DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas, int quantidadeMinima)
    {
        if (dataReferencia is null)
        {
            return new("NoExigencia.DataReferenciaObrigatoriaParaChaveCalendario", "Informe a data de referência para distinção por competência mensal ou exercício anual.");
        }
        return ocorrenciasEsperadas is not null ? OcorrenciasIndevidas() : ValidarJanelaDoCalendario(chave, dataReferencia.Value, quantidadeMinima);
    }

    private static DomainError? ValidarJanelaDoCalendario(ChaveDistincao chave, DateOnly ancora, int quantidadeMinima)
    {
        int unidadeMaisAntiga = chave == ChaveDistincao.CompetenciaMensal
            ? ((ancora.Year - 1) * 12) + ancora.Month - (quantidadeMinima - 1) : ancora.Year - (quantidadeMinima - 1);
        return unidadeMaisAntiga < 1 ? new("NoExigencia.QuantidadeMinimaExcedeJanelaRepresentavel",
            "A quantidade mínima exige unidades regulares anteriores ao calendário representável a partir da data de referência.") : null;
    }

    private static DomainError? ValidarChaveDeOcorrencia(DateOnly? dataReferencia, IReadOnlyList<string>? ocorrenciasEsperadas, int quantidadeMinima)
    {
        if (dataReferencia is not null)
        {
            return new("NoExigencia.DataReferenciaIndevidaParaChave", "Não informe data de referência para distinção por ocorrência.");
        }
        return ocorrenciasEsperadas is null ? null : ValidarOcorrencias(ocorrenciasEsperadas, quantidadeMinima);
    }

    private static DomainError? ValidarOcorrencias(IReadOnlyList<string> ocorrencias, int quantidadeMinima)
    {
        if (ocorrencias.Count == 0)
        {
            return new("NoExigencia.OcorrenciasEsperadasVazia", "Informe ao menos uma ocorrência esperada ou deixe a lista ausente.");
        }
        if (ocorrencias.Any(static id => string.IsNullOrWhiteSpace(id)))
        {
            return new("NoExigencia.OcorrenciasEsperadasComIdVazio", "Informe identificadores não vazios para as ocorrências esperadas.");
        }
        return ValidarIdentidadeEQuantidadeDasOcorrencias(ocorrencias, quantidadeMinima);
    }

    private static DomainError? ValidarIdentidadeEQuantidadeDasOcorrencias(IReadOnlyList<string> ocorrencias, int quantidadeMinima)
    {
        if (ocorrencias.Distinct(StringComparer.Ordinal).Count() != ocorrencias.Count)
        {
            return new("NoExigencia.OcorrenciasEsperadasComIdsDuplicados", "Informe identificadores distintos para as ocorrências esperadas.");
        }
        return quantidadeMinima != ocorrencias.Count ? new("NoExigencia.OcorrenciasEsperadasQuantidadeMinimaDivergente",
            "A quantidade mínima deve ser igual ao número de ocorrências esperadas.") : null;
    }

    public IReadOnlyList<string>? SlotsEsperados(int quantidadeMinima) => Chave switch
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
