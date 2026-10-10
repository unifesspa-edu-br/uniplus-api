namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Como o valor de uma resposta da inscrição foi obtido.</summary>
public enum OrigemDaResposta
{
    /// <summary>Digitado ou selecionado pelo candidato.</summary>
    Informada = 1,

    /// <summary>Calculado pelo sistema a partir de outra resposta, como a UF a partir do endereço.</summary>
    Derivada = 2,
}
