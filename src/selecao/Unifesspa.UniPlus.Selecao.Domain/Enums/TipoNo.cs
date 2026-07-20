namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Discrimina um <see cref="Entities.NoExigencia"/> da árvore de satisfação (Story #920) —
/// folha (uma exigência de documento) ou grupo com conector <c>E</c>/<c>OU</c>.
/// </summary>
public enum TipoNo
{
    Nenhum = 0,

    /// <summary>Folha — envolve um <see cref="Entities.DocumentoExigido"/> (1:1). Sem filhos, sem cardinalidade/consequência próprias nesta Story.</summary>
    Folha = 1,

    /// <summary>Grupo E — transparente: exige todos os filhos aplicáveis, sem cardinalidade nem consequência própria.</summary>
    GrupoE = 2,

    /// <summary>Grupo OU/N-de — opaco: exige um mínimo de filhos satisfeitos (<c>QuantidadeMinima</c>), pode carregar consequência e base legal próprias.</summary>
    GrupoOu = 3,
}
