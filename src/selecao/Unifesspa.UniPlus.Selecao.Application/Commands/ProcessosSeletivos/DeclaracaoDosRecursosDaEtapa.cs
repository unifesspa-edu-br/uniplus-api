namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Declara as janelas recursais de uma etapa. A âncora em ato é declarada pelo código e resolvida DENTRO
/// dos produtos desta etapa: ancorar na publicação de outra etapa deixa de ser exprimível.
/// </summary>
internal sealed class DeclaracaoDosRecursosDaEtapa(
    ConferenciaDaAncoraDoRecursoDaEtapa conferenciaDaAncora,
    LeituraMemoizada<(string Codigo, string Versao), RegraCatalogo> regras)
{
    public async Task<Result> DeclararAsync(
        EtapaProcesso etapa,
        EtapaProcessoInput input,
        IReadOnlySet<string> atosJaAncorados,
        CancellationToken cancellationToken)
    {
        List<RecursoDaEtapa> recursos = [];
        foreach (RecursoDaEtapaInput declarado in input.Recursos)
        {
            Result<RecursoDaEtapa> recurso = await RecursoAsync(etapa, input, declarado, atosJaAncorados, cancellationToken)
                .ConfigureAwait(false);
            if (recurso.IsFailure)
            {
                return Result.ValidationFailure(recurso.Errors);
            }

            recursos.Add(recurso.Value!);
        }

        return etapa.DefinirRecursos(recursos);
    }

    private async Task<Result<RecursoDaEtapa>> RecursoAsync(
        EtapaProcesso etapa,
        EtapaProcessoInput input,
        RecursoDaEtapaInput declarado,
        IReadOnlySet<string> atosJaAncorados,
        CancellationToken cancellationToken)
    {
        ProdutoDaEtapa? produtoAncora = ProdutoAncora(etapa, declarado);
        if (produtoAncora is not null)
        {
            // Conferido sobre o produto âncora JÁ RESOLVIDO, e não sobre o código cru do payload: um código
            // que não corresponde a produto preliminar algum da etapa é âncora inexistente, e quem sabe
            // dizer isso é o domínio, nomeando os preliminares disponíveis.
            DomainError? recusaDaAncora = await conferenciaDaAncora
                .ConferirAsync(etapa, input.Nome, produtoAncora, atosJaAncorados, cancellationToken).ConfigureAwait(false);
            if (recusaDaAncora is not null)
            {
                return Result<RecursoDaEtapa>.Failure(recusaDaAncora);
            }
        }

        Result<ReferenciaRegra> regra = await ReferenciaDaRegraAsync(declarado, cancellationToken).ConfigureAwait(false);
        return regra.IsFailure
            ? Result<RecursoDaEtapa>.ValidationFailure(regra.Errors)
            : RecursoDaEtapa.Criar(declarado.Ancora, regra.Value!, ArgumentosDoPrazo(declarado), produtoAncora?.Id ?? Guid.Empty);
    }

    // Entre os PRELIMINARES, e não entre todos os produtos: a etapa publica o mesmo ato duas vezes, e é do
    // preliminar que a janela corre. Resolver só pelo código devolveria o definitivo quando ele viesse
    // primeiro, e a etapa seria recusada por ancorar onde não se pode.
    private static ProdutoDaEtapa? ProdutoAncora(EtapaProcesso etapa, RecursoDaEtapaInput declarado) =>
        declarado.Ancora == AncoraDoRecurso.AtoPublicado
            ? etapa.Produtos.FirstOrDefault(p => p.Papel == PapelProdutoFase.Preliminar
                && string.Equals(p.AtoCodigo, declarado.AtoAncoraCodigo, StringComparison.Ordinal))
            : null;

    // A referência é montada a partir dos valores RESOLVIDOS do catálogo, nunca ecoados do payload, e a
    // recusa é de validação, nunca exceção: catálogo que não respondeu chega aqui com código em branco, e o
    // cliente precisa da mensagem, não de um 500.
    private async Task<Result<ReferenciaRegra>> ReferenciaDaRegraAsync(RecursoDaEtapaInput declarado, CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regras.ObterAsync((declarado.RegraCodigo, declarado.RegraVersao), cancellationToken)
            .ConfigureAwait(false);
        return regra is null || regra.Tipo != TipoRegra.RegraPrazoRecurso
            ? Result<ReferenciaRegra>.Failure(new DomainError(
                "RecursoDaEtapa.RegraCatalogoInvalida",
                $"A regra {declarado.RegraCodigo}/{declarado.RegraVersao} não é uma regra de prazo de recurso do catálogo."))
            : ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash);
    }

    private static ArgsRegraPrazoRecurso ArgumentosDoPrazo(RecursoDaEtapaInput declarado) =>
        new(
            declarado.PrazoValor,
            declarado.PrazoUnidade,
            declarado.SuspensividadePrimeiraInstanciaValor,
            declarado.SuspensividadePrimeiraInstanciaUnidade,
            declarado.AdmiteInstanciaSuperior,
            declarado.SuspensividadeSegundaInstanciaValor,
            declarado.SuspensividadeSegundaInstanciaUnidade);
}
