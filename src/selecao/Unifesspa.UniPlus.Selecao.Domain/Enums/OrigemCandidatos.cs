namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// De onde vêm os candidatos do <see cref="Entities.ProcessoSeletivo"/> (§3.4 da Story
/// #851) — o eixo que deriva o piso mínimo do cronograma de fases, <b>nunca</b> o
/// <see cref="TipoProcesso"/>. Atributo declarado, obrigatório na criação do processo.
/// </summary>
/// <remarks>
/// Prova de indistinguibilidade: um processo do tipo SiSU com
/// <see cref="ImportacaoExterna"/> publica exatamente igual a um processo do tipo PSIQ
/// com a mesma origem — o veredicto do gate depende só deste atributo, nunca do rótulo
/// do tipo.
/// </remarks>
public enum OrigemCandidatos
{
    Nenhuma = 0,

    /// <summary>O certame coleta a própria inscrição — exige ao menos uma fase com <c>ColetaInscricao</c>.</summary>
    InscricaoPropria = 1,

    /// <summary>Os candidatos vêm de importação externa (ex.: SiSU/MEC) — nenhuma fase de coleta é exigida.</summary>
    ImportacaoExterna = 2,
}
