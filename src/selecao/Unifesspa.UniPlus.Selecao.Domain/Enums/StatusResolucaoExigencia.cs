namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Veredicto de <see cref="Services.ResolvedorExigenciasDocumentais"/> sobre uma
/// <see cref="Entities.DocumentoExigido"/> congelada, para um candidato (Story #554, PR #903,
/// ADR-0076; 4º valor <see cref="AplicabilidadeIndeterminada"/>, Story #916).
/// </summary>
public enum StatusResolucaoExigencia
{
    Nenhuma = 0,

    /// <summary>O gatilho DNF casou (ou a exigência é GERAL) e há apresentação que a satisfaz — direta, ou via grupo de satisfação.</summary>
    Satisfeita = 1,

    /// <summary>O gatilho DNF casou (ou a exigência é GERAL), mas não há apresentação que a satisfaça ainda.</summary>
    Pendente = 2,

    /// <summary>A exigência é CONDICIONAL e o gatilho não casou (<see cref="Ternario.Falso"/>) para este candidato — não conta contra ele.</summary>
    NaoAplicavel = 3,

    /// <summary>
    /// A exigência é CONDICIONAL e o gatilho avaliou <see cref="Ternario.Indeterminado"/> — um
    /// fato citado no gatilho não está resolvido para este candidato (Story #916). Distinto de
    /// <see cref="Pendente"/> (que já significa "aplicável, falta apresentação"): aqui não se
    /// sabe se a exigência é sequer aplicável. Nunca vira <see cref="Satisfeita"/> só porque o
    /// candidato apresentou algo, e não participa do grupo de satisfação — é resolvida
    /// fato-a-fato, independente de apresentação já existente.
    /// </summary>
    AplicabilidadeIndeterminada = 4,
}
