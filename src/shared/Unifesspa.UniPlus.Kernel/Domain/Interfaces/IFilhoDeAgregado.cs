namespace Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Entidade própria (não owned) que pertence a um agregado: mudar uma linha dela é mudar o
/// agregado, então a persistência marca o dono como alterado e o token de concorrência dele
/// avança. Sem isto, duas edições simultâneas só de filhos do mesmo dono não se enxergariam.
/// </summary>
/// <remarks>
/// Implementar de forma explícita, para que o EF não mapeie os membros como colunas.
/// </remarks>
public interface IFilhoDeAgregado
{
    /// <summary>O tipo do dono, como entidade do modelo do EF.</summary>
    Type TipoDoDono { get; }

    /// <summary>O identificador do dono.</summary>
    Guid DonoId { get; }
}
