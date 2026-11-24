namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// De onde vêm as opções de um <see cref="Entities.FatoColetado"/> no processo, copiado da fonte
/// dos valores do dado no catálogo quando a coleta é definida (ADR-0136). A Seleção congela por
/// cópia e não lê o catálogo ao publicar, por isso guarda a origem no próprio campo.
/// </summary>
public enum OrigemValoresColeta
{
    [CodigoDeContrato(
        "CATALOGO",
        "Catálogo",
        Descricao = "Os valores vêm do catálogo, ou o campo não é de escolha.")]
    Catalogo = 0,

    [CodigoDeContrato(
        "OPCOES_DO_PROCESSO",
        "Opções do processo",
        Descricao = "As opções são as que o processo declara.")]
    OpcoesDoProcesso = 1,

    [CodigoDeContrato(
        "MUNICIPIOS_DO_FADA",
        "Municípios do Fator de Atenuação das Desigualdades de Acesso (FADA)",
        Descricao = "As opções são os municípios do Fator de Atenuação das Desigualdades de Acesso (FADA) do processo.")]
    MunicipiosDoFada = 2,

    [CodigoDeContrato(
        "UNIDADES_FEDERATIVAS",
        "Unidades federativas",
        Descricao = "As opções são as unidades federativas, listadas sem consultar o serviço de geografia.")]
    UnidadesFederativas = 3,

    [CodigoDeContrato(
        "MUNICIPIOS_DA_PROVA_PRESENCIAL",
        "Municípios da prova presencial",
        Descricao = "As opções são os municípios da prova presencial do processo.")]
    MunicipiosDaProvaPresencial = 4,
}
