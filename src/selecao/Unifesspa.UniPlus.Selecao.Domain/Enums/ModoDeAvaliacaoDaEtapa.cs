namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Como a etapa é avaliada, decidido num ponto único a partir do que ela declara e do que o processo
/// cita dela.
/// </summary>
public enum ModoDeAvaliacaoDaEtapa
{
    /// <summary>A etapa tem nota numérica: compõe a média, tem nota mínima, é citada por regra ou tem conceito numérico.</summary>
    NotaNumerica = 1,

    /// <summary>A etapa é avaliada por conceito categórico, que não tem nota.</summary>
    ConceitoCategorico = 2,

    /// <summary>A nota da etapa vem do ENEM.</summary>
    NotaDoEnem = 3,

    /// <summary>A etapa não tem nota nem conceito.</summary>
    SemNotaNemConceito = 4,
}
