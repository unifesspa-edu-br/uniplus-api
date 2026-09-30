namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// De onde vêm as opções de um <see cref="Entities.FatoColetado"/> no processo, copiado da fonte
/// dos valores do fato no catálogo quando a coleta é definida (ADR-0136). A Seleção congela por
/// cópia e não lê o catálogo ao publicar, por isso guarda a origem no próprio campo.
/// </summary>
/// <remarks>A numeração dos membros é identidade de persistência; o envelope grava o nome do membro.</remarks>
public enum OrigemValoresColeta
{
    /// <summary>Os valores vêm do catálogo, ou o campo não é de seleção.</summary>
    Catalogo = 0,

    /// <summary>As opções são as que o processo declara (fonte <c>PROCESSO</c>).</summary>
    OpcoesDoProcesso = 1,

    /// <summary>As opções são os municípios do bônus regional do processo (fonte <c>MUNICIPIOS_BONUS</c>).</summary>
    MunicipiosDoBonus = 2,
}
