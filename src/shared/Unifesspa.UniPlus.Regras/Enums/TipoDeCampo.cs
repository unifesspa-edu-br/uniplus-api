namespace Unifesspa.UniPlus.Regras.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Como o campo que produz um dado é apresentado no formulário. Conjunto fechado por design, coerente com o tipo do
/// dado no catálogo do candidato: <see cref="SimOuNao"/> só para o tipo <c>SIM_OU_NAO</c>, <see cref="Numero"/> só
/// para <c>NUMERICO</c>, <see cref="EscolhaUnica"/>/<see cref="EscolhaMultipla"/> para <c>OPCAO</c> conforme a
/// quantidade de valores do dado no catálogo, <see cref="Texto"/> para <c>TEXTO</c>, <see cref="Data"/> para
/// <c>DATA</c> e <see cref="Endereco"/> para <c>ENDERECO</c>.
/// </summary>
/// <remarks>
/// A identidade persistida é o código declarado (ADR-0176); a numeração dos membros não é gravada
/// nem é peso de ordenação — mesmo raciocínio de <see cref="Operador"/>.
/// </remarks>
[DescricaoDoContrato("Como o campo do formulário é apresentado e respondido pelo candidato.")]
public enum TipoDeCampo
{
    /// <summary>Campo numérico — só aceito quando o dado é do tipo <c>NUMERICO</c>.</summary>
    [CodigoDeContrato("NUMERO", "Número", Descricao = "Campo em que o candidato digita um número inteiro.")]
    Numero = 1,

    /// <summary>Campo de sim ou não — só aceito quando o dado é do tipo <c>SIM_OU_NAO</c>.</summary>
    [CodigoDeContrato("SIM_OU_NAO", "Sim ou não", Descricao = "Campo em que o candidato responde sim ou não.")]
    SimOuNao = 2,

    /// <summary>
    /// Escolha de um único valor — aceito para dado do tipo <c>OPCAO</c> com um valor só.
    /// </summary>
    [CodigoDeContrato("ESCOLHA_UNICA", "Escolha de um valor", Descricao = "Campo em que o candidato escolhe uma opção entre as oferecidas.")]
    EscolhaUnica = 3,

    /// <summary>
    /// Escolha de vários valores — aceito para dado do tipo <c>OPCAO</c> com vários valores.
    /// </summary>
    [CodigoDeContrato("ESCOLHA_MULTIPLA", "Escolha de vários valores", Descricao = "Campo em que o candidato escolhe uma ou mais opções entre as oferecidas.")]
    EscolhaMultipla = 4,

    /// <summary>
    /// Campo de texto — só aceito quando o dado é do tipo <c>TEXTO</c>; o formato do dado no
    /// catálogo (livre, CPF, e-mail…) diz como a resposta é conferida.
    /// </summary>
    [CodigoDeContrato("TEXTO", "Texto", Descricao = "Campo em que o candidato digita um texto, conferido pelo formato do dado.")]
    Texto = 5,

    /// <summary>Campo de data — só aceito quando o dado é do tipo <c>DATA</c>.</summary>
    [CodigoDeContrato("DATA", "Data", Descricao = "Campo em que o candidato informa uma data do calendário.")]
    Data = 6,

    /// <summary>
    /// Endereço estruturado, composto a partir da busca do CEP no Geo (ADR-0096) — só aceito quando o
    /// dado é do tipo <c>ENDERECO</c>.
    /// </summary>
    [CodigoDeContrato("ENDERECO", "Endereço", Descricao = "Campo de endereço completo, preenchido a partir da busca do CEP.")]
    Endereco = 7,

    /// <summary>
    /// Município escolhido entre os da UF respondida antes, listados pelo Geo no cliente (ADR-0096) —
    /// só aceito quando o dado é de opção com um valor e de origem <c>MUNICIPIOS</c>. Não congela lista:
    /// a resposta é o código IBGE, conferido pela UF.
    /// </summary>
    [CodigoDeContrato("MUNICIPIO", "Município", Descricao = "Campo em que o candidato escolhe o município entre os da unidade federativa já respondida.")]
    Municipio = 8,
}
