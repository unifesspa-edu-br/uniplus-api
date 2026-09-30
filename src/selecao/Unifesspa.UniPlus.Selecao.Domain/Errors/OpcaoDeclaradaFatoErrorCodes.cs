namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>Códigos de erro das opções que o processo declara para um fato (issue #1619).</summary>
public static class OpcaoDeclaradaFatoErrorCodes
{
    public const string CodigoInvalido = "OpcaoDeclaradaFato.CodigoInvalido";
    public const string RotuloInvalido = "OpcaoDeclaradaFato.RotuloInvalido";
    public const string ListaVazia = "OpcaoDeclaradaFato.ListaVazia";
    public const string CodigoRepetido = "OpcaoDeclaradaFato.CodigoRepetido";
    public const string GeridasPelaOfertaDeAtendimento = "OpcaoDeclaradaFato.GeridasPelaOfertaDeAtendimento";
    public const string ReferenciadaPorExigenciaViva = "OpcaoDeclaradaFato.ReferenciadaPorExigenciaViva";
    public const string FonteNaoEhDoProcesso = "OpcaoDeclaradaFato.FonteNaoEhDoProcesso";
}
