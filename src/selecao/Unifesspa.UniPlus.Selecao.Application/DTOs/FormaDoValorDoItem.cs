namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>A forma do valor de um item de resposta.</summary>
public enum FormaDoValorDoItem
{
    [CodigoDeContrato("TEXTO", "Texto", Descricao = "O valor é um texto.")]
    Texto,
    [CodigoDeContrato("NUMERO", "Número", Descricao = "O valor é um número em texto, como foi respondido.")]
    Numero,
    [CodigoDeContrato("SIM_OU_NAO", "Sim ou não", Descricao = "O valor é verdadeiro ou falso.")]
    SimOuNao,
    [CodigoDeContrato("DATA", "Data", Descricao = "O valor é uma data em texto, no formato AAAA-MM-DD.")]
    Data,
    [CodigoDeContrato("OPCAO", "Opção", Descricao = "O valor é um objeto com código e rótulo da opção.")]
    Opcao,
    [CodigoDeContrato("OPCOES", "Opções", Descricao = "O valor é uma lista de objetos com código e rótulo de cada opção.")]
    Opcoes,
    [CodigoDeContrato("ENDERECO", "Endereço", Descricao = "O valor é um objeto com cep, logradouro, numero, complemento, bairro, municipio, municipioCodigoIbge, uf e ufNome, em texto.")]
    Endereco,
}
