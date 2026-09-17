namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories.Ordenacao;

using System.Globalization;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Linha de leitura da vitrine: a divulgação acompanhada das colunas que podem decidir a posição
/// antes do identificador.
/// </summary>
/// <remarks>
/// <para>
/// Duas dessas colunas não existem na entidade materializada. A chave alfabética é coluna gerada,
/// mapeada como propriedade sombra; e o segmento aberto/encerrado é uma comparação com o instante
/// da consulta, que não é atributo de linha nenhuma. Projetar para este carregador resolve as duas
/// sem levá-las ao domínio, e a instância ainda serve de âncora ao motor de paginação.
/// </para>
/// </remarks>
internal sealed class CertameNaVitrine : IIdentificavel
{
    public Guid Id { get; init; }

    /// <summary>
    /// Se a janela já encerrou no instante congelado da travessia. É a primeira coluna da ordem
    /// canônica: ela joga os encerrados para o fim sem inverter o prazo dentro de cada grupo.
    /// </summary>
    public bool Encerrado { get; init; }

    public string NomeOrdenacao { get; init; } = string.Empty;

    public DateTimeOffset InscricoesDe { get; init; }

    public DateTimeOffset InscricoesAte { get; init; }

    public DateTimeOffset DivulgadoEm { get; init; }

    public CertameDivulgado Entidade { get; init; } = null!;
}

/// <summary>
/// Liga cada campo que a vitrine aceita em <c>sort</c> à coluna correspondente da linha de leitura,
/// e monta a ordenação pedida.
/// </summary>
/// <remarks>
/// <para>
/// Os nomes dos campos são contrato público e vivem em <see cref="CamposOrdenacaoDaVitrine"/>,
/// visíveis à borda que valida o parâmetro e documenta a rota. O que mora aqui é o outro lado da
/// ponte: a coluna que cada nome designa e como o valor dela vira texto na âncora do cursor. Um
/// teste confere que os dois conjuntos coincidem, de modo que declarar um campo sem mapeá-lo — ou o
/// contrário — quebra a suíte em vez de virar uma recusa inexplicável em produção.
/// </para>
/// <para>
/// Toda ordenação termina implicitamente pelo identificador, que o motor acrescenta. Por isso
/// nenhum campo aqui precisa garantir unicidade sozinho.
/// </para>
/// </remarks>
internal static class OrdenacaoDaVitrine
{
    /// <summary>
    /// Ordem canônica por urgência, usada quando a consulta não pede outra: quem ainda não encerrou
    /// primeiro, do prazo mais próximo ao mais distante, e os encerrados depois.
    /// </summary>
    /// <remarks>
    /// O segmento não é campo do catálogo — ninguém pede <c>sort=encerrado</c>. Ele existe só para
    /// produzir esta rotação, que é a ordem que a vitrine tem quando o cliente não escolhe nenhuma.
    /// </remarks>
    private const string CampoDoSegmento = "encerrado";

    /// <summary>Monta a ordenação da vitrine sobre o recorte informado.</summary>
    /// <param name="campos">
    /// Campos pedidos pela consulta. Vazio é a ordem canônica por urgência.
    /// </param>
    /// <param name="recorte">O que reduziu a coleção antes do keyset.</param>
    public static KeysetSort<CertameNaVitrine> Montar(
        IReadOnlyList<SortField> campos,
        IReadOnlyList<string> recorte) =>
        campos.Count == 0
            ? new KeysetSort<CertameNaVitrine>(
                [ColunaDoSegmento(), Coluna(new SortField(CamposOrdenacaoDaVitrine.InscricoesAte, SortDirection.Ascending))],
                AncoraDaOrdemCanonica,
                recorte)
            : new KeysetSort<CertameNaVitrine>(
                [.. campos.Select(Coluna)],
                (partes, id) => Ancora(campos, partes, id),
                recorte);

    private static readonly Dictionary<string, Func<SortDirection, KeysetSortColumn<CertameNaVitrine>>> Campos =
        new(StringComparer.Ordinal)
        {
            [CamposOrdenacaoDaVitrine.InscricoesAte] = d => KeysetSortColumn<CertameNaVitrine>.For(
                CamposOrdenacaoDaVitrine.InscricoesAte, c => c.InscricoesAte, c => ChaveDeInstante(c.InscricoesAte), d),
            [CamposOrdenacaoDaVitrine.InscricoesDe] = d => KeysetSortColumn<CertameNaVitrine>.For(
                CamposOrdenacaoDaVitrine.InscricoesDe, c => c.InscricoesDe, c => ChaveDeInstante(c.InscricoesDe), d),
            [CamposOrdenacaoDaVitrine.Nome] = d => KeysetSortColumn<CertameNaVitrine>.For(
                CamposOrdenacaoDaVitrine.Nome, c => c.NomeOrdenacao, c => c.NomeOrdenacao, d),
            [CamposOrdenacaoDaVitrine.DivulgadoEm] = d => KeysetSortColumn<CertameNaVitrine>.For(
                CamposOrdenacaoDaVitrine.DivulgadoEm, c => c.DivulgadoEm, c => ChaveDeInstante(c.DivulgadoEm), d),
        };

    /// <summary>Nomes de campo que a vitrine sabe ordenar.</summary>
    public static IReadOnlyList<string> CamposMapeados { get; } = [.. Campos.Keys];

    private static KeysetSortColumn<CertameNaVitrine> Coluna(SortField campo) =>
        Campos[campo.Campo](campo.Direcao);

    private static KeysetSortColumn<CertameNaVitrine> ColunaDoSegmento() =>
        KeysetSortColumn<CertameNaVitrine>.For(
            CampoDoSegmento, c => c.Encerrado, c => c.Encerrado ? "1" : "0", SortDirection.Ascending);

    /// <summary>
    /// Instante em ISO-8601 com deslocamento zero. A ordem lexicográfica desse formato coincide com
    /// a cronológica, que é o que a âncora precisa para comparar como o banco compara.
    /// </summary>
    private static string ChaveDeInstante(DateTimeOffset instante) =>
        instante.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static CertameNaVitrine AncoraDaOrdemCanonica(IReadOnlyList<string> partes, Guid id) =>
        new()
        {
            Id = id,
            Encerrado = partes.Count > 0 && partes[0] == "1",
            InscricoesAte = partes.Count > 1 ? InstanteDaChave(partes[1]) : default,
        };

    private static CertameNaVitrine Ancora(IReadOnlyList<SortField> campos, IReadOnlyList<string> partes, Guid id)
    {
        Dictionary<string, string> valores = new(StringComparer.Ordinal);
        for (int i = 0; i < campos.Count && i < partes.Count; i++)
        {
            valores[campos[i].Campo] = partes[i];
        }

        return new CertameNaVitrine
        {
            Id = id,
            NomeOrdenacao = valores.GetValueOrDefault(CamposOrdenacaoDaVitrine.Nome, string.Empty),
            InscricoesDe = InstanteDaChave(valores.GetValueOrDefault(CamposOrdenacaoDaVitrine.InscricoesDe)),
            InscricoesAte = InstanteDaChave(valores.GetValueOrDefault(CamposOrdenacaoDaVitrine.InscricoesAte)),
            DivulgadoEm = InstanteDaChave(valores.GetValueOrDefault(CamposOrdenacaoDaVitrine.DivulgadoEm)),
        };
    }

    private static DateTimeOffset InstanteDaChave(string? bruto) =>
        bruto is not null
        && DateTimeOffset.TryParse(bruto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset instante)
            ? instante
            : default;
}
