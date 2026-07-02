namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Formato pedagógico de uma <see cref="Entities.OfertaCurso"/> (story #588,
/// ADR-0066): presencial, semipresencial ou a distância (EaD). Persistido como
/// token UPPER_SNAKE (<see cref="FormatosPedagogicos"/>). Quando o token não é
/// informado na criação/atualização, o default conceitual é
/// <see cref="Presencial"/> (mesmo expediente do default AMPLA de
/// <see cref="NaturezasLegais"/>).
/// </summary>
public enum FormatoPedagogico
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhum = 0,

    /// <summary>Oferta presencial (default conceitual quando o token está ausente).</summary>
    Presencial = 1,

    /// <summary>Oferta semipresencial (híbrida).</summary>
    Semipresencial = 2,

    /// <summary>Oferta a distância (EaD).</summary>
    Ead = 3,
}
