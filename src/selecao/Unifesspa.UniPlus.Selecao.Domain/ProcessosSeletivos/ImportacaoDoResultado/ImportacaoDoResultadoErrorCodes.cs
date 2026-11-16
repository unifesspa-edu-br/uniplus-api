namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>Os códigos de recusa da importação do resultado declarada pelo processo (UNI-REQ-0144, UNI-REQ-0151).</summary>
public static class ImportacaoDoResultadoErrorCodes
{
    /// <summary>O relatório declarado não informa o código ou a versão do leiaute.</summary>
    public const string RelatorioSemLeiaute = "ImportacaoDoResultado.RelatorioSemLeiaute";

    /// <summary>O leiaute já está declarado como relatório do processo.</summary>
    public const string RelatorioRepetido = "ImportacaoDoResultado.RelatorioRepetido";
}
