namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Enum com código cujo texto difere do nome e do número do membro, para provar que o canal escreve o código declarado.</summary>
public enum ModoDeEntregaDeTeste
{
    [CodigoDeContrato("em_maos", "Em mãos")]
    Presencial = 7,

    [CodigoDeContrato("pelo_correio", "Pelo correio")]
    Postal = 9,
}

/// <summary>Enum sem código: o canal o escreve como já o escrevia antes de existir o código de contrato.</summary>
public enum SituacaoSemCodigoDeTeste
{
    Rascunho = 3,
}
