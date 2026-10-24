namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Enums;

using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>Os formatos do arquivo do candidato nas três formas em que circulam: o domínio, a fila do verificador e o nome de apresentação.</summary>
public static class FormatosDoArquivoDoCandidato
{
    public static FormatoDoArquivo ParaContrato(FormatoPermitido formato) => formato switch
    {
        FormatoPermitido.Pdf => FormatoDoArquivo.Pdf,
        FormatoPermitido.Jpeg => FormatoDoArquivo.Jpeg,
        FormatoPermitido.Png => FormatoDoArquivo.Png,
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato que o verificador não conhece."),
    };

    /// <summary>O formato do domínio; nulo para "não definido" e para o que o sistema não recebe.</summary>
    public static FormatoPermitido? DoContrato(FormatoDoArquivo? formato) => formato switch
    {
        FormatoDoArquivo.Pdf => FormatoPermitido.Pdf,
        FormatoDoArquivo.Jpeg => FormatoPermitido.Jpeg,
        FormatoDoArquivo.Png => FormatoPermitido.Png,
        _ => null,
    };

    public static string Extensao(FormatoPermitido formato) => formato switch
    {
        FormatoPermitido.Pdf => "pdf",
        FormatoPermitido.Jpeg => "jpg",
        FormatoPermitido.Png => "png",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, "Formato sem extensão."),
    };
}
