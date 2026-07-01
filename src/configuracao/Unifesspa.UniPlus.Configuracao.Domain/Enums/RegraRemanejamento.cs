namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Regra de remanejamento das vagas não preenchidas de uma
/// <see cref="Entities.Modalidade"/> de concorrência (UNI-REQ-0011): descreve, em
/// domínio fechado, para onde vagas ociosas migram (cascata legal, destino único
/// ou par cruzado). Persistida como token UPPER_SNAKE (<see cref="RegrasRemanejamento"/>).
/// É opcional — modalidades de ampla concorrência não remanejam como cota.
/// </summary>
public enum RegraRemanejamento
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhuma = 0,

    /// <summary>Segue a cascata de remanejamento legalmente estabelecida (cotas reservadas).</summary>
    SegueCascata = 1,

    /// <summary>Remaneja para um destino único informado em <c>RemanejamentoArgs.Destino</c>.</summary>
    DestinoUnico = 2,

    /// <summary>Remaneja de forma cruzada entre um par (<c>Par</c>) com fallback (<c>Fallback</c>).</summary>
    Cruzado = 3,
}
