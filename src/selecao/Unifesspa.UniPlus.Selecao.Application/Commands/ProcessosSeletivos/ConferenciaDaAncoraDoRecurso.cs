namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Confere o ato em que o prazo do recurso de uma etapa ou de uma fase começa: ele não congela a
/// configuração e não tem efeito irreversível. O ato que congela fixa as regras do certame, e contar prazo
/// de recurso a partir dele é contar contra o documento que o candidato não está contestando; se o tipo já
/// foi declarado, vale a classificação congelada, e o catálogo só vale para o tipo novo. A
/// irreversibilidade nunca é congelada e é sempre lida do catálogo (UNI-REQ-0080): o ato que consome vaga
/// escassa concede direito que não se desfaz, e recurso contra ele não teria o que reverter — o cabível é
/// contra o resultado que o fundamentou.
/// </summary>
/// <remarks>
/// O que difere entre os donos é o que lhes pertence: se o evento que inicia o prazo do recurso já estava declarado no agregado e se a
/// versão publicada já o tem, ambos recebidos como parâmetro; e o rótulo das recusas.
/// </remarks>
internal sealed class ConferenciaDaAncoraDoRecurso(AtosConsultadosNaEdicao atos)
{
    /// <summary>A recusa do evento que inicia o prazo do recurso, ou <c>null</c> quando o ato pode iniciar o prazo do recurso.</summary>
    public async Task<DomainError?> ConferirAsync(
        string atoCodigo,
        RotuloDoDonoDaAncora rotulo,
        Func<string, bool> jaDeclarada,
        Func<string, CancellationToken, Task<Result<bool>>> jaPublicadaAsync,
        CancellationToken cancellationToken)
    {
        Result<TipoDeAtoCongelado?> congelada = await atos.Congelados
            .DoTipoJaDeclaradoAsync(atoCodigo, cancellationToken).ConfigureAwait(false);
        if (congelada.IsFailure)
        {
            return congelada.Error;
        }

        TipoAtoPublicadoView? tipo = await atos.TipoVigenteAsync(atoCodigo, cancellationToken).ConfigureAwait(false);
        DomainError? ancoraNova = tipo is null && !jaDeclarada(atoCodigo)
            ? await RecusaDaAncoraNovaSemCatalogoAsync(atoCodigo, rotulo, jaPublicadaAsync, cancellationToken).ConfigureAwait(false)
            : null;
        return ancoraNova ?? RecusaDoAto(rotulo, congelada.Value, tipo);
    }

    // Sem versão vigente no catálogo a irreversibilidade é desconhecida, e um evento novo que inicia o prazo do recurso não pode ser
    // aceito sem conferi-la. O evento que a versão publicada ou o agregado já tem segue valendo.
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
                $"O tipo de ato declarado{rotulo.Qualificador} não tem versão vigente no catálogo de Publicações na data de hoje, e o novo evento que inicia o prazo do recurso precisa da irreversibilidade dele.");
    }

    private static DomainError? RecusaDoAto(
        RotuloDoDonoDaAncora rotulo, TipoDeAtoCongelado? congelada, TipoAtoPublicadoView? tipo) =>
        RecusaDoAtoQueCongela(rotulo, congelada, tipo) ?? RecusaDoAtoIrreversivel(rotulo, congelada, tipo);

    private static DomainError? RecusaDoAtoQueCongela(
        RotuloDoDonoDaAncora rotulo, TipoDeAtoCongelado? congelada, TipoAtoPublicadoView? tipo) =>
        congelada?.Classificacao.CongelaConfiguracao ?? tipo?.CongelaConfiguracao ?? false
            ? new DomainError(
                rotulo.CodigoDaAncoraCongelante,
                $"O tipo de ato '{NomeDoAto(congelada, tipo)}'{rotulo.Qualificador} congela a configuração, e o evento que inicia o prazo do recurso nunca é o ato que congela a configuração.")
            : null;

    private static DomainError? RecusaDoAtoIrreversivel(
        RotuloDoDonoDaAncora rotulo, TipoDeAtoCongelado? congelada, TipoAtoPublicadoView? tipo) =>
        tipo?.EfeitoIrreversivel == true
            ? new DomainError(
                rotulo.CodigoDaAncoraIrreversivel,
                $"O tipo de ato '{NomeDoAto(congelada, tipo)}'{rotulo.Qualificador} tem efeito irreversível: não cabe recurso contra ele, e sim contra o resultado que o fundamenta.")
            : null;

    // A cópia da versão publicada vence o catálogo: o nome é o que a unidade viu ao publicar.
    private static string? NomeDoAto(TipoDeAtoCongelado? congelada, TipoAtoPublicadoView? tipo) => congelada?.Nome ?? tipo?.Nome;
}
