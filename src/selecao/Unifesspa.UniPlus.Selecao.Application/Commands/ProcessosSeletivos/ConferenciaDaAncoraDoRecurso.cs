namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Confere o ato em que a janela recursal de uma etapa ou de uma fase se ancora: ele não congela a
/// configuração e não tem efeito irreversível. O ato que congela fixa as regras do certame, e contar prazo
/// de recurso a partir dele é contar contra o documento que o candidato não está contestando; se o tipo já
/// foi declarado, vale a classificação congelada, e o catálogo só vale para o tipo novo. A
/// irreversibilidade nunca é congelada e é sempre lida do catálogo (UNI-REQ-0080): o ato que consome vaga
/// escassa concede direito que não se desfaz, e recurso contra ele não teria o que reverter — o cabível é
/// contra o resultado que o fundamentou.
/// </summary>
/// <remarks>
/// O que difere entre os donos é o que lhes pertence: se a âncora já estava declarada no agregado e se a
/// versão publicada já a tem, ambos recebidos como parâmetro; e o rótulo das recusas.
/// </remarks>
internal sealed class ConferenciaDaAncoraDoRecurso(AtosConsultadosNaEdicao atos)
{
    /// <summary>A recusa da âncora, ou <c>null</c> quando o ato pode ancorar o recurso.</summary>
    public async Task<DomainError?> ConferirAsync(
        string atoCodigo,
        RotuloDoDonoDaAncora rotulo,
        Func<string, bool> jaDeclarada,
        Func<string, CancellationToken, Task<Result<bool>>> jaPublicadaAsync,
        CancellationToken cancellationToken)
    {
        Result<ClassificacaoDoTipoDeAto?> congelada = await atos.Congelados
            .DoTipoJaDeclaradoAsync(atoCodigo, cancellationToken).ConfigureAwait(false);
        if (congelada.IsFailure)
        {
            return congelada.Error;
        }

        TipoAtoPublicadoView? tipo = await atos.TipoVigenteAsync(atoCodigo, cancellationToken).ConfigureAwait(false);
        DomainError? ancoraNova = tipo is null && !jaDeclarada(atoCodigo)
            ? await RecusaDaAncoraNovaSemCatalogoAsync(atoCodigo, rotulo, jaPublicadaAsync, cancellationToken).ConfigureAwait(false)
            : null;
        return ancoraNova ?? RecusaDoAto(atoCodigo, rotulo, congelada.Value, tipo);
    }

    // Sem versão vigente no catálogo a irreversibilidade é desconhecida, e uma âncora nova não pode ser
    // aceita sem conferi-la. A âncora que a versão publicada ou o agregado já tem segue valendo.
    private static async Task<DomainError?> RecusaDaAncoraNovaSemCatalogoAsync(
        string atoCodigo,
        RotuloDoDonoDaAncora rotulo,
        Func<string, CancellationToken, Task<Result<bool>>> jaPublicadaAsync,
        CancellationToken cancellationToken)
    {
        Result<bool> jaPublicada = await jaPublicadaAsync(atoCodigo, cancellationToken).ConfigureAwait(false);
        if (jaPublicada.IsFailure)
        {
            return jaPublicada.Error;
        }

        return jaPublicada.Value
            ? null
            : new DomainError(
                rotulo.CodigoDoAtoNaoEncontrado,
                $"O tipo de ato '{atoCodigo}'{rotulo.Qualificador} não tem versão vigente no catálogo de Publicações na data de hoje, e a âncora nova precisa da irreversibilidade dele.");
    }

    private static DomainError? RecusaDoAto(
        string atoCodigo, RotuloDoDonoDaAncora rotulo, ClassificacaoDoTipoDeAto? congelada, TipoAtoPublicadoView? tipo)
    {
        if (congelada?.CongelaConfiguracao ?? tipo?.CongelaConfiguracao ?? false)
        {
            return new DomainError(
                rotulo.CodigoDaAncoraCongelante,
                $"O tipo de ato âncora '{atoCodigo}'{rotulo.Qualificador} congela configuração — a âncora do recurso nunca é o ato que congela a configuração.");
        }

        return tipo?.EfeitoIrreversivel == true
            ? new DomainError(
                rotulo.CodigoDaAncoraIrreversivel,
                $"O tipo de ato âncora '{tipo.Codigo}'{rotulo.Qualificador} tem efeito irreversível — não cabe recurso contra ele, e sim contra o resultado que o fundamenta.")
            : null;
    }
}
