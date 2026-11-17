namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Vagas;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Resolve um <see cref="ConfiguracaoDistribuicaoVagasInput"/> cru (Ids e
/// códigos do payload do cliente) contra os cadastros vivos do módulo
/// Configuração e o catálogo <c>regras_do_catalogo</c>, montando uma
/// <see cref="ConfiguracaoDistribuicaoVagas"/> congelada por valor
/// (snapshot-copy, ADR-0061).
/// </summary>
/// <remarks>
/// Extraído de <see cref="DefinirDistribuicaoVagasCommandHandler"/> (issue
/// #1282/#1283) para ser reaproveitado, sem duplicação, pelo comando que
/// persiste a distribuição e pela query que só simula o cálculo — as duas
/// superfícies precisam concordar byte a byte sobre o que é uma
/// configuração válida e sobre o quadro que ela produz.
/// </remarks>
public sealed class ConfiguracaoDistribuicaoVagasResolver(
    IRegraCatalogoReader regraCatalogoReader,
    IOfertaCursoReader ofertaCursoReader,
    IModalidadeReader modalidadeReader,
    IReferenciaReservaDemograficaReader referenciaReservaDemograficaReader)
{
    /// <summary>
    /// A distribuição reporta as violações com os nomes do domínio — o conjunto de modalidades sob
    /// <c>modalidades</c>, a regra de ajuste sob <c>regraDeAjuste</c> —, e o cliente envia os
    /// campos de <see cref="ConfiguracaoDistribuicaoVagasInput"/>. Sem esta tradução, a resposta de validação
    /// apontaria para uma propriedade inexistente no request.
    /// </summary>
    internal static string TraduzirFieldDoDominio(string fieldDominio) =>
        fieldDominio switch
        {
            "modalidades" => "modalidadeIds",
            "regraDeAjuste" => "regraAjusteCodigo",
            _ => fieldDominio,
        };

    /// <summary>
    /// Resolve todas as distribuições do payload, cada uma contra os cadastros, e acumula (ADR-0125) as recusas
    /// de todas: primeiro as de forma, depois as de cadastro, depois as da declaração e por fim as do quadro, e
    /// dentro de cada família na ordem das ofertas. Só as ofertas sem recusa alguma têm o quadro montado.
    /// </summary>
    internal async Task<Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>> ResolverTodasAsync(
        IReadOnlyList<ConfiguracaoDistribuicaoVagasInput> pedidos, CancellationToken cancellationToken)
    {
        List<ResolucaoDaDistribuicao> resolucoes = [];
        foreach (ConfiguracaoDistribuicaoVagasInput pedido in pedidos)
        {
            ResolucaoDaDistribuicao resolucao = new(pedido, regraCatalogoReader, ofertaCursoReader, modalidadeReader, referenciaReservaDemograficaReader);
            await resolucao.ResolverAsync(cancellationToken).ConfigureAwait(false);
            resolucoes.Add(resolucao);
        }

        return Concluir(resolucoes);
    }

    private static Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>> Concluir(List<ResolucaoDaDistribuicao> resolucoes)
    {
        List<FieldError> recusas = RecusasDeTodasAsOfertas(resolucoes);
        List<ConfiguracaoDistribuicaoVagas> distribuicoes = [];
        for (int indice = 0; indice < resolucoes.Count; indice++)
        {
            MontarDaOferta(resolucoes[indice], indice, distribuicoes, recusas);
        }

        return recusas.Count > 0
            ? Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>.ValidationFailure(recusas)
            : Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>.Success(distribuicoes);
    }

    private static List<FieldError> RecusasDeTodasAsOfertas(List<ResolucaoDaDistribuicao> resolucoes) =>
    [
        .. DaFamilia(resolucoes, static resolucao => resolucao.RecusasDaForma),
        .. DaFamilia(resolucoes, static resolucao => resolucao.RecusasDoCadastro),
        .. DaFamilia(resolucoes, static resolucao => resolucao.RecusasDaDeclaracao),
    ];

    private static IEnumerable<FieldError> DaFamilia(
        List<ResolucaoDaDistribuicao> resolucoes, Func<ResolucaoDaDistribuicao, IReadOnlyList<FieldError>> recusasDaFamilia) =>
        resolucoes.SelectMany((resolucao, indice) => recusasDaFamilia(resolucao).Select(erro => ComIndice(erro, indice)));

    // O quadro só se monta quando nenhuma das famílias anteriores recusou a oferta.
    private static void MontarDaOferta(
        ResolucaoDaDistribuicao resolucao, int indice, List<ConfiguracaoDistribuicaoVagas> distribuicoes, List<FieldError> recusas)
    {
        if (!resolucao.SemRecusas)
        {
            return;
        }

        Result<ConfiguracaoDistribuicaoVagas> montada = resolucao.Montar();
        if (montada.IsSuccess)
        {
            distribuicoes.Add(montada.Value!);
            return;
        }

        recusas.AddRange(montada.Errors.Select(erro => ComIndice(erro, indice)));
    }

    private static FieldError ComIndice(FieldError erro, int indice) =>
        erro.Field is null ? erro : erro with { Field = $"distribuicaoVagas[{indice}].{TraduzirFieldDoDominio(erro.Field)}" };
}
