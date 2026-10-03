namespace Unifesspa.UniPlus.Regras.Enums;

/// <summary>O tipo de uma restrição de valor do item (UNI-REQ-0145); um item declara no máximo uma de cada.</summary>
public enum TipoRestricaoValor
{
    Nenhuma = 0,

    /// <summary>A resposta numérica fica entre um mínimo e um máximo.</summary>
    FaixaNumerica = 1,

    /// <summary>A resposta de texto tem de um mínimo a um máximo de caracteres.</summary>
    TamanhoTexto = 2,

    /// <summary>A resposta escolhe entre opções fixas ou condicionadas a respostas anteriores.</summary>
    OpcoesPermitidas = 3,

    /// <summary>A resposta escolhe entre as respostas dadas a itens anteriores.</summary>
    OpcoesDasRespostas = 4,

    /// <summary>A resposta é um município da UF respondida num item anterior.</summary>
    MunicipiosDaUf = 5,
}
