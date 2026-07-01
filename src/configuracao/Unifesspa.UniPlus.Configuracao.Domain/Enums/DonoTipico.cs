namespace Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Dono <b>usual</b> de uma <see cref="Entities.FaseCanonica"/> (UNI-REQ-0064): a
/// unidade que costuma conduzir a fase (CEPS, CRCA, MEC ou CONSEPE). É um rótulo
/// orientativo em domínio fechado — <b>não</b> vincula o processo, que decide o
/// dono real do seu próprio cronograma. Persistido como token UPPER_SNAKE
/// (<see cref="DonosTipicos"/>).
/// </summary>
public enum DonoTipico
{
    /// <summary>Sentinela — indica entrada inválida/corrupção se encontrado em runtime.</summary>
    Nenhum = 0,

    /// <summary>Centro de Processos Seletivos.</summary>
    Ceps = 1,

    /// <summary>Centro de Registro e Controle Acadêmico.</summary>
    Crca = 2,

    /// <summary>Ministério da Educação (ex.: SiSU).</summary>
    Mec = 3,

    /// <summary>Conselho Superior de Ensino, Pesquisa e Extensão.</summary>
    Consepe = 4,
}
