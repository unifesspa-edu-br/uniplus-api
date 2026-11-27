namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories.Ordenacao;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Linha de leitura das decisões: o ato acompanhado do instante que decide a posição antes do <c>Id</c>. A
/// entidade não tem construtor público, então o ponto de continuação do cursor é uma instância deste carregador.
/// </summary>
internal sealed class DecisaoOrdenada : IIdentificavel
{
    public Guid Id { get; init; }

    public DateTimeOffset Instante { get; init; }

    public AtoDaProtecaoDoCatalogo Entidade { get; init; } = null!;
}

/// <summary>
/// A ordem das decisões: das mais recentes para as mais antigas, com o <c>Id</c> como desempate final, que a paginação
/// acrescenta. Não é escolhida por quem consulta.
/// </summary>
internal static class OrdenacaoDasDecisoesDaProtecao
{
    private const string CampoInstante = "instante";

    public static KeysetSort<DecisaoOrdenada> Montar(CondicoesDoFiltro condicoes) =>
        new(
            [KeysetSortColumn<DecisaoOrdenada>.For(CampoInstante, d => d.Instante, d => InstanteDoCursor.Escrever(d.Instante), SortDirection.Descending)],
            (partes, id) => new DecisaoOrdenada { Id = id, Instante = InstanteDoCursor.Ler(partes[0]) },
            condicoes);
}
