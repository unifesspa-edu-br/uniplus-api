namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>Os envios de arquivo recém-iniciados, ainda pendentes, com o prazo de envio escolhido pelo teste.</summary>
internal static class EnviosPendentesDeTeste
{
    public static readonly TimeSpan PrazoDeEnvio = TimeSpan.FromMinutes(15);

    public static DocumentoAPublicar Documento(
        Guid processoId,
        TimeSpan? prazo = null,
        FinalidadeDoDocumento finalidade = FinalidadeDoDocumento.DocumentoDoEdital,
        string nomeArquivo = "edital.pdf",
        string tipoDeMidia = TiposDeMidiaDeDocumento.Pdf,
        TimeProvider? relogio = null) =>
        DocumentoAPublicar.IniciarPendente(processoId, finalidade, nomeArquivo, tipoDeMidia, relogio ?? TimeProvider.System, prazo ?? PrazoDeEnvio).Value!;

    public static ModeloDeDocumento Modelo(
        Guid processoId, TimeSpan? prazo = null, string nomeArquivo = "Autodeclaração", TimeProvider? relogio = null) =>
        ModeloDeDocumento.IniciarPendente(processoId, nomeArquivo, "ODT", relogio ?? TimeProvider.System, prazo ?? PrazoDeEnvio).Value!;
}
