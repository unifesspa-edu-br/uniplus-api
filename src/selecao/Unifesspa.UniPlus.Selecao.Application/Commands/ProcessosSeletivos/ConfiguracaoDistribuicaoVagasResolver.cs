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
internal static class ConfiguracaoDistribuicaoVagasResolver
{
    /// <summary>
    /// A distribuição reporta as violações com os nomes do domínio — o conjunto de modalidades sob
    /// <c>modalidades</c>, as vagas ofertadas base sob <c>vagasOfertadasBase</c> —, e o cliente envia os
    /// campos de <see cref="ConfiguracaoDistribuicaoVagasInput"/>. Sem esta tradução, a resposta de validação
    /// apontaria para uma propriedade inexistente no request.
    /// </summary>
    internal static string TraduzirFieldDoDominio(string fieldDominio) =>
        fieldDominio switch
        {
            "modalidades" => "modalidadeIds",
            "vagasOfertadasBase" => "voBase",
            "percentualDaReservaLegal" => "pr",
            "regraDeAjuste" => "regraAjusteCodigo",
            "ajusteDoQuadro" => "argsAjuste",
            _ => fieldDominio,
        };

    /// <summary>
    /// A forma de todas as distribuições do payload, acumulada (ADR-0125) antes de qualquer I/O: a recusa de
    /// forma vence a de cadastro dentro do bucket 422.
    /// </summary>
    internal static IReadOnlyList<FieldError> ConferirForma(IReadOnlyList<ConfiguracaoDistribuicaoVagasInput> pedidos) =>
        [.. pedidos.SelectMany((pedido, indice) => ConferenciaDaDistribuicaoDeVagas
            .ConferirForma(pedido.VoBase, pedido.Pr, pedido.ModalidadeIds.Count)
            .Select(erro => ComIndice(erro, indice)))];

    internal static async Task<Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>> ResolverTodasAsync(
        IReadOnlyList<ConfiguracaoDistribuicaoVagasInput> pedidos, IRegraCatalogoReader regraCatalogoReader, IOfertaCursoReader ofertaCursoReader,
        IModalidadeReader modalidadeReader, IReferenciaReservaDemograficaReader referenciaReservaDemograficaReader, CancellationToken cancellationToken)
    {
        List<ConfiguracaoDistribuicaoVagas> distribuicoes = [];
        for (int indice = 0; indice < pedidos.Count; indice++)
        {
            Result<ConfiguracaoDistribuicaoVagas> resultado = await new ResolucaoDaDistribuicao(pedidos[indice], regraCatalogoReader, ofertaCursoReader, modalidadeReader, referenciaReservaDemograficaReader)
                .ResolverAsync(cancellationToken).ConfigureAwait(false);
            if (resultado.IsFailure)
            {
                return FalhaDoPedido(resultado, indice);
            }

            distribuicoes.Add(resultado.Value!);
        }

        return Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>.Success(distribuicoes);
    }

    private static Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>> FalhaDoPedido(
        Result<ConfiguracaoDistribuicaoVagas> resultado, int indice) =>
        Result<IReadOnlyList<ConfiguracaoDistribuicaoVagas>>.ValidationFailure(
            [.. resultado.Errors.Select(erro => ComIndice(erro, indice))]);

    private static FieldError ComIndice(FieldError erro, int indice) =>
        erro.Field is null ? erro : erro with { Field = $"distribuicaoVagas[{indice}].{TraduzirFieldDoDominio(erro.Field)}" };
}
