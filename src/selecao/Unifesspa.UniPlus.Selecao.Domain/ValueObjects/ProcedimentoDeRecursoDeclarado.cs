namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O procedimento que o edital declara para revisar o recurso de uma matéria: o tipo de decisão recorrida, o alvo
/// opcional dentro dela e a regra do catálogo que diz como revisar. A identidade é o par tipo e alvo.
/// </summary>
public sealed record ProcedimentoDeRecursoDeclarado
{
    private ProcedimentoDeRecursoDeclarado(TipoMotivoDeDecisao tipoDeDecisao, AlvoDoProcedimentoDeRecurso? alvo, ReferenciaRegra regra)
    {
        TipoDeDecisao = tipoDeDecisao;
        Alvo = alvo;
        Regra = regra;
    }

    public TipoMotivoDeDecisao TipoDeDecisao { get; }

    /// <summary>O alvo, ou nulo quando o procedimento cobre o tipo de decisão inteiro.</summary>
    public AlvoDoProcedimentoDeRecurso? Alvo { get; }

    public ReferenciaRegra Regra { get; }

    /// <exception cref="ArgumentOutOfRangeException">
    /// O tipo é a sentinela <see cref="TipoMotivoDeDecisao.Nenhum"/>: quem chama recusa o tipo inválido antes de montar.
    /// </exception>
    public static ProcedimentoDeRecursoDeclarado De(TipoMotivoDeDecisao tipoDeDecisao, AlvoDoProcedimentoDeRecurso? alvo, ReferenciaRegra regra)
    {
        ArgumentNullException.ThrowIfNull(regra);
        ArgumentOutOfRangeException.ThrowIfEqual(tipoDeDecisao, TipoMotivoDeDecisao.Nenhum);

        return new ProcedimentoDeRecursoDeclarado(tipoDeDecisao, alvo, regra);
    }

    /// <summary>Se os dois cobrem a mesma matéria: o mesmo tipo de decisão e o mesmo alvo, qualquer que seja a regra.</summary>
    public bool CobreAMesmaMateria(ProcedimentoDeRecursoDeclarado outro)
    {
        ArgumentNullException.ThrowIfNull(outro);
        return TipoDeDecisao == outro.TipoDeDecisao && Alvo == outro.Alvo;
    }
}
