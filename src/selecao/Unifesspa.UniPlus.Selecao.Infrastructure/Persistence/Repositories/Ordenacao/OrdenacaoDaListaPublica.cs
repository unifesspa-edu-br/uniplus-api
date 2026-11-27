namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories.Ordenacao;

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
/// sem levá-las ao domínio, e a instância ainda serve de ponto de continuação à paginação.
/// </para>
/// </remarks>
internal sealed class CertameNaVitrine : IIdentificavel
{
    public Guid Id { get; init; }

    /// <summary>
    /// Se o período já encerrou no instante congelado do percurso. É a primeira coluna da ordem
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
/// Os nomes dos campos são contrato público e vivem em <see cref="CamposDeOrdenacaoDaListaPublica"/>,
/// visíveis à borda que valida o parâmetro e documenta a rota. O que mora aqui é o outro lado da
/// ponte: a coluna que cada nome designa e como o valor dela vira texto no ponto de continuação do cursor. Um
/// teste confere que os dois conjuntos coincidem, de modo que declarar um campo sem mapeá-lo — ou o
/// contrário — quebra a suíte em vez de virar uma recusa inexplicável em produção.
/// </para>
/// <para>
/// Toda ordenação termina implicitamente pelo identificador, que a paginação acrescenta. Por isso
/// nenhum campo aqui precisa garantir unicidade sozinho.
/// </para>
/// </remarks>
internal static class OrdenacaoDaListaPublica
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

    /// <summary>Monta a ordenação da vitrine sobre o filtro informado.</summary>
    /// <param name="campos">
    /// Campos pedidos pela consulta. Vazio é a ordem canônica por urgência.
    /// </param>
    /// <param name="condicoes">O que reduziu a coleção antes do keyset, com o instante congelado do percurso.</param>
    public static KeysetSort<CertameNaVitrine> Montar(
        IReadOnlyList<SortField> campos,
        CondicoesDoFiltro condicoes) =>
        campos.Count == 0
            ? new KeysetSort<CertameNaVitrine>(
                [ColunaDoSegmento(), Coluna(new SortField(CamposDeOrdenacaoDaListaPublica.InscricoesAte, SortDirection.Ascending))],
                PontoDeContinuacaoDaOrdemCanonica,
                condicoes)
            : new KeysetSort<CertameNaVitrine>(
                [.. campos.Select(Coluna)],
                (partes, id) => PontoDeContinuacao(campos, partes, id),
                condicoes);

    private static readonly Dictionary<string, Func<SortDirection, KeysetSortColumn<CertameNaVitrine>>> Campos =
        new(StringComparer.Ordinal)
        {
            [CamposDeOrdenacaoDaListaPublica.InscricoesAte] = d => KeysetSortColumn<CertameNaVitrine>.For(
                CamposDeOrdenacaoDaListaPublica.InscricoesAte, c => c.InscricoesAte, c => InstanteDoCursor.Escrever(c.InscricoesAte), d),
            [CamposDeOrdenacaoDaListaPublica.InscricoesDe] = d => KeysetSortColumn<CertameNaVitrine>.For(
                CamposDeOrdenacaoDaListaPublica.InscricoesDe, c => c.InscricoesDe, c => InstanteDoCursor.Escrever(c.InscricoesDe), d),
            [CamposDeOrdenacaoDaListaPublica.Nome] = d => KeysetSortColumn<CertameNaVitrine>.For(
                CamposDeOrdenacaoDaListaPublica.Nome, c => c.NomeOrdenacao, c => c.NomeOrdenacao, d),
            [CamposDeOrdenacaoDaListaPublica.DivulgadoEm] = d => KeysetSortColumn<CertameNaVitrine>.For(
                CamposDeOrdenacaoDaListaPublica.DivulgadoEm, c => c.DivulgadoEm, c => InstanteDoCursor.Escrever(c.DivulgadoEm), d),
        };

    /// <summary>Nomes de campo que a vitrine sabe ordenar.</summary>
    public static IReadOnlyList<string> CamposMapeados { get; } = [.. Campos.Keys];

    private static KeysetSortColumn<CertameNaVitrine> Coluna(SortField campo) =>
        Campos[campo.Campo](campo.Direcao);

    private static KeysetSortColumn<CertameNaVitrine> ColunaDoSegmento() =>
        KeysetSortColumn<CertameNaVitrine>.For(
            CampoDoSegmento, c => c.Encerrado, c => c.Encerrado ? "1" : "0", SortDirection.Ascending);

    private static CertameNaVitrine PontoDeContinuacaoDaOrdemCanonica(IReadOnlyList<string> partes, Guid id) =>
        new()
        {
            Id = id,
            Encerrado = partes.Count > 0 && partes[0] == "1",
            InscricoesAte = partes.Count > 1 ? InstanteDoCursor.Ler(partes[1]) : default,
        };

    private static CertameNaVitrine PontoDeContinuacao(IReadOnlyList<SortField> campos, IReadOnlyList<string> partes, Guid id)
    {
        Dictionary<string, string> valores = new(StringComparer.Ordinal);
        for (int i = 0; i < campos.Count && i < partes.Count; i++)
        {
            valores[campos[i].Campo] = partes[i];
        }

        return new CertameNaVitrine
        {
            Id = id,
            NomeOrdenacao = valores.GetValueOrDefault(CamposDeOrdenacaoDaListaPublica.Nome, string.Empty),
            InscricoesDe = InstanteLido(valores, CamposDeOrdenacaoDaListaPublica.InscricoesDe),
            InscricoesAte = InstanteLido(valores, CamposDeOrdenacaoDaListaPublica.InscricoesAte),
            DivulgadoEm = InstanteLido(valores, CamposDeOrdenacaoDaListaPublica.DivulgadoEm),
        };
    }

    private static DateTimeOffset InstanteLido(Dictionary<string, string> valores, string campo) =>
        valores.TryGetValue(campo, out string? texto) ? InstanteDoCursor.Ler(texto) : default;
}
