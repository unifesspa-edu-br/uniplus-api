namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O tipo de uma restrição de valor do item (UNI-REQ-0145); um item declara no máximo uma de cada.</summary>
[DescricaoDoContrato("O que a restrição limita na resposta do candidato a um item do formulário.")]
public enum TipoRestricaoValor
{
    /// <summary>A resposta numérica fica entre um mínimo e um máximo.</summary>
    [CodigoDeContrato(
        "FAIXA_NUMERICA",
        "Faixa numérica",
        Descricao = "A resposta numérica fica entre um mínimo e um máximo.")]
    FaixaNumerica = 1,

    /// <summary>A resposta de texto tem de um mínimo a um máximo de caracteres.</summary>
    [CodigoDeContrato(
        "TAMANHO_TEXTO",
        "Tamanho do texto",
        Descricao = "A resposta de texto tem de um mínimo a um máximo de caracteres.")]
    TamanhoTexto = 2,

    /// <summary>A resposta escolhe entre opções fixas ou condicionadas a respostas anteriores.</summary>
    [CodigoDeContrato(
        "OPCOES_PERMITIDAS",
        "Opções permitidas",
        Descricao = "A resposta escolhe entre opções fixas ou condicionadas a respostas anteriores.")]
    OpcoesPermitidas = 3,

    /// <summary>A resposta escolhe entre as respostas dadas a itens anteriores.</summary>
    [CodigoDeContrato(
        "OPCOES_DAS_RESPOSTAS",
        "Opções formadas pelas respostas",
        Descricao = "A resposta escolhe entre as respostas dadas a itens anteriores.")]
    OpcoesDasRespostas = 4,

    /// <summary>A resposta é um município do estado respondido num item anterior.</summary>
    [CodigoDeContrato(
        "MUNICIPIOS_DA_UF",
        "Municípios do estado",
        Descricao = "A resposta é um município do estado respondido num item anterior.")]
    MunicipiosDaUf = 5,

    /// <summary>A resposta de texto está no formato do fato no catálogo, como CPF ou e-mail.</summary>
    [CodigoDeContrato(
        "FORMATO_TEXTO",
        "Formato do texto",
        Descricao = "A resposta de texto está no formato do fato no catálogo, como CPF ou e-mail. Vem do catálogo e não se declara no item.")]
    FormatoTexto = 6,

    /// <summary>A resposta de data fica entre uma data mínima e uma máxima, fixas ou contadas do dia do envio.</summary>
    [CodigoDeContrato(
        "FAIXA_DE_DATA",
        "Faixa de data",
        Descricao = "A resposta de data fica entre uma data mínima e uma máxima, fixas ou contadas do dia do envio.")]
    FaixaDeData = 7,
}
