namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>
/// O lugar em que o processo decide: uma fase, pela fase canônica de origem, ou uma etapa, pela identidade dela.
/// Tem sempre exatamente uma das duas identidades, e a igualdade é pelo valor.
/// </summary>
public sealed record LugarDaDecisao
{
    private LugarDaDecisao(Guid? faseCanonicaOrigemId, Guid? etapaProcessoId)
    {
        FaseCanonicaOrigemId = faseCanonicaOrigemId;
        EtapaProcessoId = etapaProcessoId;
    }

    /// <summary>A fase canônica de origem, quando o lugar é uma fase; nula quando é uma etapa.</summary>
    public Guid? FaseCanonicaOrigemId { get; }

    /// <summary>A etapa do processo, quando o lugar é uma etapa; nula quando é uma fase.</summary>
    public Guid? EtapaProcessoId { get; }

    public static LugarDaDecisao DaFase(Guid faseCanonicaOrigemId)
    {
        GuardarIdentidade(faseCanonicaOrigemId, nameof(faseCanonicaOrigemId));
        return new(faseCanonicaOrigemId, null);
    }

    public static LugarDaDecisao DaEtapa(Guid etapaProcessoId)
    {
        GuardarIdentidade(etapaProcessoId, nameof(etapaProcessoId));
        return new(null, etapaProcessoId);
    }

    private static void GuardarIdentidade(Guid identidade, string nome)
    {
        if (identidade == Guid.Empty)
        {
            throw new ArgumentException("O lugar da decisão deve ter a identidade da fase ou da etapa.", nome);
        }
    }
}
