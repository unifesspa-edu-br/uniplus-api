namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O tipo de um dado do candidato, que decide o campo que a pessoa preenche, os operadores que uma regra pode usar sobre
/// ele e a forma do valor. O dado de texto, de data e de endereço nunca é citado numa regra; a data só entra por meio de um
/// dado calculado a partir dela.
/// </summary>
[DescricaoDoContrato("O tipo de um dado do candidato: decide o campo que a pessoa preenche, as comparações que uma regra pode fazer com ele e a forma do valor.")]
public enum TipoDoDado
{
    [CodigoDeContrato("SIM_OU_NAO", "Sim ou não", Descricao = "Resposta que só pode ser sim ou não.")]
    SimOuNao = 1,

    [CodigoDeContrato("OPCAO", "Opção", Descricao = "Resposta escolhida entre as opções de uma lista fechada.")]
    Opcao = 2,

    [CodigoDeContrato("NUMERICO", "Número", Descricao = "Número inteiro.")]
    Numerico = 3,

    [CodigoDeContrato("TEXTO", "Texto", Descricao = "Texto livre ou num formato conhecido, como CPF, e-mail ou telefone.")]
    Texto = 4,

    [CodigoDeContrato("DATA", "Data", Descricao = "Data do calendário.")]
    Data = 5,

    [CodigoDeContrato("ENDERECO", "Endereço", Descricao = "Endereço com CEP e cidade.")]
    Endereco = 6,
}
