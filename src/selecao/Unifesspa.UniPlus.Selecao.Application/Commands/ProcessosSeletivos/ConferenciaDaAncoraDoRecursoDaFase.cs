namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Confere o ato do produto em que a janela recursal de uma fase se ancora: ele não congela a configuração
/// e não tem efeito irreversível. Se o ato congela, vale a classificação congelada quando o tipo já foi
/// declarado, e o catálogo só para o tipo novo. A irreversibilidade nunca é congelada e é sempre lida do
/// catálogo (UNI-REQ-0080): um ato que consome vaga escassa concede um direito que não se desfaz, e
/// recurso contra ele não teria o que reverter.
/// </summary>
internal sealed class ConferenciaDaAncoraDoRecursoDaFase(
    AtosConsultadosNaEdicao atos,
    IReadOnlySet<(string Fase, string Ato)> ancorasJaDeclaradas)
{
    /// <summary>A recusa da âncora, ou <c>null</c> quando o ato pode ancorar o recurso.</summary>
    public async Task<DomainError?> ConferirAsync(string faseCodigo, ProdutoDaFase produtoAncora, CancellationToken cancellationToken)
    {
        Result<ClassificacaoDoTipoDeAto?> congelada = await atos.Congelados
            .DoTipoJaDeclaradoAsync(produtoAncora.AtoCodigo, cancellationToken).ConfigureAwait(false);
        if (congelada.IsFailure)
        {
            return congelada.Error;
        }

        TipoAtoPublicadoView? tipo = await atos.TipoVigenteAsync(produtoAncora.AtoCodigo, cancellationToken).ConfigureAwait(false);
        DomainError? ancoraNova = tipo is null && !ancorasJaDeclaradas.Contains((faseCodigo, produtoAncora.AtoCodigo))
            ? await RecusaDaAncoraNovaSemCatalogoAsync(faseCodigo, produtoAncora, cancellationToken).ConfigureAwait(false)
            : null;
        return ancoraNova ?? RecusaDoAto(produtoAncora, congelada.Value, tipo);
    }

    // Sem versão vigente no catálogo a irreversibilidade é desconhecida, e uma âncora nova não pode ser
    // aceita sem conferi-la. A âncora que a versão publicada ou o agregado já tem segue valendo.
    private async Task<DomainError?> RecusaDaAncoraNovaSemCatalogoAsync(
        string faseCodigo, ProdutoDaFase produtoAncora, CancellationToken cancellationToken)
    {
        Result<bool> jaPublicada = await atos.Congelados
            .AncoraDaFaseJaPublicadaAsync(faseCodigo, produtoAncora.AtoCodigo, cancellationToken).ConfigureAwait(false);
        if (jaPublicada.IsFailure)
        {
            return jaPublicada.Error;
        }

        return jaPublicada.Value
            ? null
            : new DomainError(
                "ProdutoDaFase.AtoNaoEncontradoNoCatalogo",
                $"O tipo de ato '{produtoAncora.AtoCodigo}' não tem versão vigente no catálogo de Publicações na data de hoje, e a âncora nova precisa da irreversibilidade dele.");
    }

    private static DomainError? RecusaDoAto(ProdutoDaFase produtoAncora, ClassificacaoDoTipoDeAto? congelada, TipoAtoPublicadoView? tipo)
    {
        if (congelada?.CongelaConfiguracao ?? tipo?.CongelaConfiguracao ?? false)
        {
            return new DomainError(
                "RegraRecursoFase.AncoraEmAtoCongelante",
                $"O tipo de ato âncora '{produtoAncora.AtoCodigo}' congela configuração — a âncora do recurso nunca é o ato que congela a configuração.");
        }

        return tipo?.EfeitoIrreversivel == true
            ? new DomainError(
                "RegraRecursoFase.AncoraEmAtoIrreversivel",
                $"O tipo de ato âncora '{tipo.Codigo}' tem efeito irreversível — não cabe recurso contra ele, e sim contra o resultado que o fundamenta.")
            : null;
    }
}
