namespace Unifesspa.UniPlus.Autorizacao.Application.Commands;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

/// <summary>
/// Emite a URL de PUT assinada de um PDF da Autorização (ADR-0146). O PDF de até
/// <see cref="Domain.ValueObjects.PdfDeclarado.TamanhoMaximoEmMegabytes"/> MB sobe dentro da validade da URL, e a das chaves
/// de idempotência dos endpoints que a devolvem acompanha essa validade.
/// </summary>
public sealed class EnvioDoPdf
{
    public const string TipoDeMidia = "application/pdf";

    /// <summary>A validade da URL, em segundos; é também a das chaves de idempotência dos endpoints que a devolvem.</summary>
    public const int PrazoEmSegundos = 15 * 60;

    public static readonly TimeSpan Prazo = TimeSpan.FromSeconds(PrazoEmSegundos);

    private readonly IArmazenamentoDosDocumentosDaAutorizacao _armazenamento;

    public EnvioDoPdf(IArmazenamentoDosDocumentosDaAutorizacao armazenamento)
    {
        ArgumentNullException.ThrowIfNull(armazenamento);

        _armazenamento = armazenamento;
    }

    /// <summary>Emite a URL de envio da chave; a validade conta do instante informado, lido antes da assinatura.</summary>
    internal async Task<UrlDeEnvioDoPdf> EmitirAsync(string chaveDeEnvio, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        string url = await _armazenamento
            .GerarUrlDeEnvioAsync(chaveDeEnvio, TipoDeMidia, Prazo, cancellationToken)
            .ConfigureAwait(false);

        return new UrlDeEnvioDoPdf(new Uri(url, UriKind.Absolute), agora + Prazo);
    }
}
