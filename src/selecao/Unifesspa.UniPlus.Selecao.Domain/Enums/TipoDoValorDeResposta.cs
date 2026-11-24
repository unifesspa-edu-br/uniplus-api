namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O tipo em que o valor de uma resposta da inscrição é gravado.</summary>
public enum TipoDoValorDeResposta
{
    [CodigoDeContrato("SIM_OU_NAO", "Sim ou não", Descricao = "A resposta contém um valor de sim ou não.")]
    SimOuNao = 1,
    [CodigoDeContrato("NUMERO", "Número", Descricao = "A resposta contém um número.")]
    Numero = 2,

    /// <summary>Valor de vocabulário fechado: opção categórica, UF. É o que a classificação filtra.</summary>
    [CodigoDeContrato("OPCAO", "Opção", Descricao = "A resposta contém uma opção escolhida entre as cadastradas.")]
    Opcao = 3,

    /// <summary>Texto livre, como um nome.</summary>
    [CodigoDeContrato("TEXTO", "Texto", Descricao = "A resposta contém texto.")]
    Texto = 4,

    [CodigoDeContrato("DATA", "Data", Descricao = "A resposta contém uma data.")]
    Data = 5,
    [CodigoDeContrato("ENDERECO", "Endereço", Descricao = "A resposta contém um endereço.")]
    Endereco = 6,

    /// <summary>
    /// Município pelo código IBGE: o rascunho guarda só o código, e a inscrição enviada guarda o código com o nome que o
    /// Geo conferiu no envio.
    /// </summary>
    [CodigoDeContrato("MUNICIPIO", "Município", Descricao = "A resposta identifica um município.")]
    Municipio = 7,
}
