namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence.Repositories;

using System.Globalization;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>
/// A ordem da fila das providências: a de abertura, pelo instante gravado na abertura, com o <c>Id</c> como desempate
/// final, que o motor acrescenta (UNI-REQ-0375). O <c>Id</c> sozinho não basta: depois do milissegundo os bits dele são
/// aleatórios, e ele vem do relógio da máquina, enquanto o instante da abertura vem do relógio da aplicação.
/// </summary>
internal static class OrdenacaoDasProvidencias
{
    private const string CampoAbertaEm = "aberta_em";

    /// <summary>
    /// A ordenação sobre o recorte do filtro: a situação e a concessão entram na assinatura da âncora, para que o cursor de
    /// um filtro não continue outro.
    /// </summary>
    public static KeysetSort<Providencia> Montar(FiltroDeProvidencias filtro) =>
        new(
            [KeysetSortColumn<Providencia>.For(CampoAbertaEm, p => p.AbertaEm, p => ChaveDoInstante(p.AbertaEm))],
            Ancora,
            [filtro.Situacao?.ToCodigo() ?? "todas", filtro.ConcessaoId?.ToString() ?? string.Empty]);

    /// <summary>
    /// Instante em ISO-8601 com deslocamento zero: a ordem lexicográfica desse formato coincide com a cronológica, que é o
    /// que a âncora precisa para comparar como o banco compara.
    /// </summary>
    private static string ChaveDoInstante(DateTimeOffset instante) =>
        instante.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    // A providência não tem construtor público: a âncora é um objeto com as propriedades que o motor compara.
    private static object Ancora(IReadOnlyList<string> partes, Guid id) =>
        new
        {
            AbertaEm = partes.Count > 0
                && DateTimeOffset.TryParse(partes[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset instante)
                    ? instante
                    : default,
            Id = id,
        };
}
