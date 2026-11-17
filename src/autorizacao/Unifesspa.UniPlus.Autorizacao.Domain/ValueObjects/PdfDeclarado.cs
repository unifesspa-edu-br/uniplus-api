namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>O PDF que o módulo recebe por URL assinada: o tamanho que o cliente declara tem de 1 byte ao máximo admitido.</summary>
public static class PdfDeclarado
{
    /// <summary>O tamanho máximo do PDF, em megabytes, como os textos de recusa o dizem.</summary>
    public const int TamanhoMaximoEmMegabytes = 10;

    /// <summary>O tamanho máximo do PDF, em bytes; é o limite do pedido ao verificador.</summary>
    public const long TamanhoMaximoBytes = TamanhoMaximoEmMegabytes * 1024L * 1024;

    /// <summary>Verdadeiro quando o tamanho declarado vai de 1 byte ao máximo.</summary>
    public static bool TamanhoAdmitido(long tamanhoDeclarado) =>
        tamanhoDeclarado is >= 1 and <= TamanhoMaximoBytes;
}
