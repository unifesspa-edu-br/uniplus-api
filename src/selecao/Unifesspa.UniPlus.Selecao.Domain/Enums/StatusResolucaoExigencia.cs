namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Veredicto de <see cref="Services.ResolvedorExigenciasDocumentais"/> sobre uma
/// <see cref="Entities.DocumentoExigido"/> congelada, para um candidato (Story #554, PR-e,
/// ADR-0076).
/// </summary>
public enum StatusResolucaoExigencia
{
    Nenhuma = 0,

    /// <summary>O gatilho DNF casou (ou a exigência é GERAL) e há apresentação que a satisfaz — direta, ou via grupo de satisfação.</summary>
    Satisfeita = 1,

    /// <summary>O gatilho DNF casou (ou a exigência é GERAL), mas não há apresentação que a satisfaça ainda.</summary>
    Pendente = 2,

    /// <summary>A exigência é CONDICIONAL e o gatilho não casou para este candidato — não conta contra ele.</summary>
    NaoAplicavel = 3,
}
