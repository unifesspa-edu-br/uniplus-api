namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Natureza de um <see cref="Entities.TipoConceito"/>: define se o conceito é medido numa escala numérica
/// ou escolhido entre valores categóricos. Persistida como token UPPER_SNAKE (<see cref="NaturezasConceito"/>).
/// </summary>
public enum NaturezaConceito
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhuma = 0,

    /// <summary>Conceito medido numa escala de mínimo, máximo e casas decimais.</summary>
    Numerico = 1,

    /// <summary>Conceito escolhido entre valores categóricos ordenados.</summary>
    Categorico = 2,
}
