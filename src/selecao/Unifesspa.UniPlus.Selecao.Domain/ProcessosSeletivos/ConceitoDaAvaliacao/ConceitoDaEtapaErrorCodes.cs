namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

/// <summary>Os códigos de recusa do conceito com que a etapa é avaliada (ADR-0169).</summary>
public static class ConceitoDaEtapaErrorCodes
{
    /// <summary>O código do conceito está ausente, é longo demais ou traz caractere inválido.</summary>
    public const string CodigoInvalido = "ConceitoDaEtapa.CodigoInvalido";

    /// <summary>O nome do conceito está ausente, é longo demais ou traz caractere inválido.</summary>
    public const string NomeInvalido = "ConceitoDaEtapa.NomeInvalido";

    /// <summary>A natureza do conceito está ausente ou não é numérica nem categórica.</summary>
    public const string NaturezaInvalida = "ConceitoDaEtapa.NaturezaInvalida";

    /// <summary>Os parâmetros do conceito não são os da sua natureza: o numérico tem escala e nenhum valor, o categórico tem valores e nenhuma escala.</summary>
    public const string NaturezaIncoerente = "ConceitoDaEtapa.NaturezaIncoerente";

    /// <summary>A escala numérica não tem a mínima menor que a máxima.</summary>
    public const string EscalaInvalida = "ConceitoDaEtapa.EscalaInvalida";

    /// <summary>As casas decimais da escala numérica estão fora do intervalo de 0 a 4.</summary>
    public const string CasasDecimaisInvalidas = "ConceitoDaEtapa.CasasDecimaisInvalidas";

    /// <summary>O conceito categórico tem menos de dois valores.</summary>
    public const string ValoresInsuficientes = "ConceitoDaEtapa.ValoresInsuficientes";

    /// <summary>O código de um valor categórico está ausente, é longo demais ou traz caractere inválido.</summary>
    public const string ValorCodigoInvalido = "ConceitoDaEtapa.ValorCodigoInvalido";

    /// <summary>O rótulo de um valor categórico está ausente, é longo demais ou traz caractere inválido.</summary>
    public const string ValorRotuloInvalido = "ConceitoDaEtapa.ValorRotuloInvalido";

    /// <summary>Dois valores categóricos do conceito têm o mesmo código.</summary>
    public const string ValorRepetido = "ConceitoDaEtapa.ValorRepetido";

    /// <summary>Dois valores categóricos do conceito têm o mesmo rótulo.</summary>
    public const string ValorRotuloRepetido = "ConceitoDaEtapa.ValorRotuloRepetido";
}
