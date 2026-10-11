namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Em que pé estão as modalidades em que o candidato concorre, diante das respostas dele (UNI-REQ-0076).</summary>
public enum SituacaoDasModalidades
{
    Nenhuma = 0,

    /// <summary>As respostas levam a ao menos uma das modalidades ofertadas.</summary>
    Concorre = 1,

    /// <summary>As respostas já decidem a derivação, e nenhuma modalidade ofertada se aplica.</summary>
    NaoConcorre = 2,

    /// <summary>Falta responder algo de que a derivação depende.</summary>
    FaltaResponder = 3,
}
