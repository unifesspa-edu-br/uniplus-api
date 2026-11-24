namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Como o valor de uma resposta da inscrição foi obtido.</summary>
public enum OrigemDaResposta
{
    /// <summary>Digitado ou selecionado pelo candidato.</summary>
    [CodigoDeContrato("INFORMADA", "Informada", Descricao = "A resposta foi informada pelo candidato.")]
    Informada = 1,

    /// <summary>Calculado pelo sistema a partir de outra resposta, como a UF a partir do endereço.</summary>
    [CodigoDeContrato("CALCULADA", "Calculada", Descricao = "A resposta foi calculada pelo sistema a partir de outra resposta.")]
    Calculada = 2,
}
