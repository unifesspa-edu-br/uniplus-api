namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories.Ordenacao;

using System.Globalization;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Linha de leitura das decisões: o ato acompanhado do instante que decide a posição antes do <c>Id</c>. A
/// entidade não tem construtor público, então a âncora de continuação do cursor é uma instância deste carregador.
/// </summary>
internal sealed class DecisaoOrdenada : IIdentificavel
{
    public Guid Id { get; init; }

    public DateTimeOffset Instante { get; init; }

    public AtoDaProtecaoDoCatalogo Entidade { get; init; } = null!;
}

/// <summary>
/// A ordem das decisões: das mais recentes para as mais antigas, com o <c>Id</c> como desempate final, que o motor
/// acrescenta. Não é escolhida por quem consulta.
/// </summary>
internal static class OrdenacaoDasDecisoesDaProtecao
{
    private const string CampoInstante = "instante";

    public static KeysetSort<DecisaoOrdenada> Montar(IReadOnlyList<string> recorte) =>
        new(
            [KeysetSortColumn<DecisaoOrdenada>.For(CampoInstante, d => d.Instante, d => ChaveDeInstante(d.Instante), SortDirection.Descending)],
            Ancora,
            recorte);

    /// <summary>
    /// Instante em ISO-8601 com deslocamento zero: a ordem lexicográfica desse formato coincide com a
    /// cronológica, que é o que a âncora precisa para comparar como o banco compara.
    /// </summary>
    private static string ChaveDeInstante(DateTimeOffset instante) =>
        instante.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DecisaoOrdenada Ancora(IReadOnlyList<string> partes, Guid id) =>
        new()
        {
            Id = id,
            Instante = partes.Count > 0
                && DateTimeOffset.TryParse(partes[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset instante)
                    ? instante
                    : default,
        };
}
