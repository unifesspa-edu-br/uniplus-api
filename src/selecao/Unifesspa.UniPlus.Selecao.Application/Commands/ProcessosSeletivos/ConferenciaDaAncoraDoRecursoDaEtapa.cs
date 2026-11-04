namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Confere o ato em que a janela recursal de uma etapa se ancora, com as mesmas recusas que a fase dá à
/// âncora dela. O ato que CONGELA a configuração fixa as regras do certame, e contar prazo de recurso a
/// partir dele é contar contra o documento que o candidato não está contestando. O ato de EFEITO
/// IRREVERSÍVEL concede direito que não se desfaz, como uma vaga escassa já entregue a outro: o recurso
/// não teria o que reverter, e o cabível é contra o resultado que o fundamentou (UNI-REQ-0080).
/// </summary>
internal sealed class ConferenciaDaAncoraDoRecursoDaEtapa(AtosConsultadosNaEdicao atos)
{
    /// <summary>A recusa da âncora, ou <c>null</c> quando o ato pode ancorar o recurso.</summary>
    public async Task<DomainError?> ConferirAsync(
        EtapaProcesso etapa,
        string nomeDaEtapa,
        ProdutoDaEtapa produtoAncora,
        IReadOnlySet<string> atosJaAncorados,
        CancellationToken cancellationToken)
    {
        Result<ClassificacaoDoTipoDeAto?> congelada = await atos.Congelados
            .DoTipoJaDeclaradoAsync(produtoAncora.AtoCodigo, cancellationToken).ConfigureAwait(false);
        if (congelada.IsFailure)
        {
            return congelada.Error;
        }

        TipoAtoPublicadoView? tipo = await atos.TipoVigenteAsync(produtoAncora.AtoCodigo, cancellationToken).ConfigureAwait(false);
        DomainError? ancoraNova = tipo is null && !atosJaAncorados.Contains(produtoAncora.AtoCodigo)
            ? await RecusaDaAncoraNovaSemCatalogoAsync(etapa, nomeDaEtapa, produtoAncora, cancellationToken).ConfigureAwait(false)
            : null;
        return ancoraNova ?? RecusaDoAto(nomeDaEtapa, produtoAncora, congelada.Value, tipo);
    }

    // A irreversibilidade não é congelada: sem versão vigente no catálogo, ela é desconhecida, e uma âncora
    // nova não pode ser aceita sem conferi-la. A âncora que a versão publicada ou o agregado já tem segue
    // valendo.
    private async Task<DomainError?> RecusaDaAncoraNovaSemCatalogoAsync(
        EtapaProcesso etapa, string nomeDaEtapa, ProdutoDaEtapa produtoAncora, CancellationToken cancellationToken)
    {
        Result<bool> jaPublicada = await atos.Congelados
            .AncoraDaEtapaJaPublicadaAsync(etapa.Id, produtoAncora.AtoCodigo, cancellationToken).ConfigureAwait(false);
        if (jaPublicada.IsFailure)
        {
            return jaPublicada.Error;
        }

        return jaPublicada.Value
            ? null
            : new DomainError(
                "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo",
                $"O tipo de ato '{produtoAncora.AtoCodigo}', declarado pela etapa '{nomeDaEtapa}', não tem versão vigente no catálogo de Publicações na data de hoje, e a âncora nova precisa da irreversibilidade dele.");
    }

    private static DomainError? RecusaDoAto(
        string nomeDaEtapa, ProdutoDaEtapa produtoAncora, ClassificacaoDoTipoDeAto? congelada, TipoAtoPublicadoView? tipo)
    {
        if (congelada?.CongelaConfiguracao ?? tipo?.CongelaConfiguracao ?? false)
        {
            return new DomainError(
                "RecursoDaEtapa.AncoraEmAtoCongelante",
                $"O tipo de ato âncora '{produtoAncora.AtoCodigo}', declarado pela etapa '{nomeDaEtapa}', congela configuração — a âncora do recurso nunca é o ato que congela a configuração.");
        }

        return tipo?.EfeitoIrreversivel == true
            ? new DomainError(
                "RecursoDaEtapa.AncoraEmAtoIrreversivel",
                $"O tipo de ato âncora '{tipo.Codigo}', declarado pela etapa '{nomeDaEtapa}', tem efeito irreversível — não cabe recurso contra ele, e sim contra o resultado que o fundamenta.")
            : null;
    }
}
