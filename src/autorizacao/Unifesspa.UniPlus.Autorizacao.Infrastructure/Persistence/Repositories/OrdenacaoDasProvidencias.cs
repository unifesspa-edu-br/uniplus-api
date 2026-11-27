namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// A ordem da fila das providências: a de abertura, pelo instante gravado na abertura, com o <c>Id</c> como desempate
/// final, que a paginação acrescenta (UNI-REQ-0375). O <c>Id</c> sozinho não basta: depois do milissegundo os bits dele são
/// aleatórios, e ele vem do relógio da máquina, enquanto o instante da abertura vem do relógio da aplicação.
/// </summary>
internal static class OrdenacaoDasProvidencias
{
    private const string CampoAbertaEm = "aberta_em";

    /// <summary>
    /// A ordenação sobre o recorte do filtro: a situação e a concessão entram no resumo do ponto de continuação, para que o
    /// cursor de um filtro não continue outro.
    /// </summary>
    public static KeysetSort<Providencia> Montar(FiltroDeProvidencias filtro)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        return new(
            [KeysetSortColumn<Providencia>.For(CampoAbertaEm, p => p.AbertaEm, p => InstanteDoCursor.Escrever(p.AbertaEm))],
            PontoDeContinuacao,
            CondicoesDoFiltro.De(ParteDoFiltro.De(filtro.Situacao), ParteDoFiltro.De(filtro.ConcessaoId)));
    }

    // A providência não tem construtor público: o ponto de continuação é um objeto com as propriedades que a paginação compara.
    private static object PontoDeContinuacao(IReadOnlyList<string> partes, Guid id) =>
        new { AbertaEm = InstanteDoCursor.Ler(partes[0]), Id = id };
}
