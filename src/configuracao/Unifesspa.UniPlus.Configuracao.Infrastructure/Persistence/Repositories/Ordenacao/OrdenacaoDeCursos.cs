namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories.Ordenacao;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>
/// Linha de leitura de Curso ordenada: a entidade acompanhada das colunas que
/// podem decidir a posição antes do <c>Id</c>.
/// </summary>
/// <remarks>
/// O motor de keyset ordena por propriedades do tipo que ele pagina, e a chave de
/// ordenação alfabética do curso é propriedade sombra — invisível na entidade
/// materializada. Projetar para esta linha de leitura resolve isso sem levar a coluna de
/// ordenação para dentro do domínio, e ainda serve de ponto de continuação do cursor: a
/// referência que o motor consome é uma instância dele com as colunas preenchidas.
/// </remarks>
internal sealed class CursoOrdenado : IIdentificavel
{
    public Guid Id { get; init; }

    public string NomeOrdenacao { get; init; } = string.Empty;

    public string Codigo { get; init; } = string.Empty;

    public string Grau { get; init; } = string.Empty;

    public string NivelEnsino { get; init; } = string.Empty;

    public DateTimeOffset CriadoEm { get; init; }

    public Curso Entidade { get; init; } = null!;
}

/// <summary>
/// Linha de leitura de Oferta de Curso ordenada pelo curso associado: a oferta
/// acompanhada das colunas de ordenação, inclusive as que vêm do curso.
/// </summary>
/// <remarks>
/// A oferta não navega para o curso — a chave estrangeira é declarada sem
/// propriedade de navegação. As colunas do curso vêm então de uma junção explícita
/// projetada nesta linha de leitura, em vez de acrescentar navegação à entidade só para
/// servir a listagem.
/// </remarks>
internal sealed record OfertaCursoOrdenada : IIdentificavel
{
    public Guid Id { get; init; }

    public string NomeOrdenacao { get; init; } = string.Empty;

    public string Codigo { get; init; } = string.Empty;

    public string OfertaCodigo { get; init; } = string.Empty;

    public string UnidadeSigla { get; init; } = string.Empty;

    public ProgramaDeOferta ProgramaDeOferta { get; init; }

    public FormatoPedagogico FormatoPedagogico { get; init; }

    public RegimeDeFuncionamento RegimeDeFuncionamento { get; init; }

    public RegimeDeTurno RegimeDeTurno { get; init; }

    public DateTimeOffset CriadoEm { get; init; }

    public OfertaCurso Entidade { get; init; } = null!;
}

/// <summary>
/// Liga cada campo que a API aceita em <c>sort</c> à coluna correspondente da
/// linha de leitura, e monta a ordenação pedida.
/// </summary>
/// <remarks>
/// <para>Os nomes dos campos são contrato público e vivem em
/// <see cref="CamposOrdenacaoCurso"/> e <see cref="CamposOrdenacaoOfertaCurso"/>,
/// visíveis à borda que valida o parâmetro e documenta a rota. O que mora aqui é o
/// outro lado da ponte: a coluna que cada nome designa e como o valor dela vira
/// texto no cursor. Um teste confere que os dois conjuntos coincidem, de
/// modo que declarar um campo sem mapeá-lo — ou o contrário — quebra a suíte em vez
/// de virar um 422 inexplicável em produção.</para>
/// <para>Toda ordenação termina implicitamente pelo <c>Id</c>, que o motor
/// acrescenta. Por isso nenhum campo aqui precisa garantir unicidade sozinho.</para>
/// </remarks>
internal static class OrdenacaoDeCursos
{
    /// <summary>Ordem alfabética de nome com o código como desempate, usada quando a consulta não pede outra.</summary>
    public static IReadOnlyList<SortField> PadraoDeCurso { get; } =
    [
        new(CamposOrdenacaoCurso.Nome, SortDirection.Ascending),
        new(CamposOrdenacaoCurso.Codigo, SortDirection.Ascending),
    ];

    /// <summary>Ordem alfabética pelo curso ofertado, usada quando a consulta não pede outra.</summary>
    public static IReadOnlyList<SortField> PadraoDeOferta { get; } =
    [
        new(CamposOrdenacaoOfertaCurso.CursoNome, SortDirection.Ascending),
        new(CamposOrdenacaoOfertaCurso.CursoCodigo, SortDirection.Ascending),
    ];

    /// <summary>Monta a ordenação de Cursos para os campos pedidos, sobre os filtros da listagem informados.</summary>
    public static KeysetSort<CursoOrdenado> DeCursos(
        IReadOnlyList<SortField> campos,
        IReadOnlyList<string> filtrosDaListagem) =>
        new(
            [.. campos.Select(ColunaDeCurso)],
            (partes, id) => CursoLidoDoCursor(ValoresPorCampo(campos, partes), id),
            filtrosDaListagem);

    /// <summary>Monta a ordenação de Ofertas de Curso para os campos pedidos, sobre os filtros da listagem informados.</summary>
    public static KeysetSort<OfertaCursoOrdenada> DeOfertas(
        IReadOnlyList<SortField> campos,
        IReadOnlyList<string> filtrosDaListagem) =>
        new(
            [.. campos.Select(ColunaDeOferta)],
            (partes, id) => OfertaLidaDoCursor(campos, partes, id),
            filtrosDaListagem);

    private static readonly Dictionary<string, Func<SortDirection, KeysetSortColumn<CursoOrdenado>>> CamposDeCurso =
        new(StringComparer.Ordinal)
        {
            [CamposOrdenacaoCurso.Nome] = d => KeysetSortColumn<CursoOrdenado>.For(
                CamposOrdenacaoCurso.Nome, c => c.NomeOrdenacao, c => c.NomeOrdenacao, d),
            [CamposOrdenacaoCurso.Codigo] = d => KeysetSortColumn<CursoOrdenado>.For(
                CamposOrdenacaoCurso.Codigo, c => c.Codigo, c => c.Codigo, d),
            [CamposOrdenacaoCurso.Grau] = d => KeysetSortColumn<CursoOrdenado>.For(
                CamposOrdenacaoCurso.Grau, c => c.Grau, c => c.Grau, d),
            [CamposOrdenacaoCurso.NivelEnsino] = d => KeysetSortColumn<CursoOrdenado>.For(
                CamposOrdenacaoCurso.NivelEnsino, c => c.NivelEnsino, c => c.NivelEnsino, d),
            [CamposOrdenacaoCurso.CriadoEm] = d => KeysetSortColumn<CursoOrdenado>.For(
                CamposOrdenacaoCurso.CriadoEm, c => c.CriadoEm, c => ChaveDeInstante(c.CriadoEm), d),
        };

    private sealed record CampoOrdenavelDaOferta(
        Func<SortDirection, KeysetSortColumn<OfertaCursoOrdenada>> Coluna,
        Func<OfertaCursoOrdenada, string, OfertaCursoOrdenada> Reconstituir);

    private static readonly Dictionary<string, CampoOrdenavelDaOferta> CamposDeOferta =
        new(StringComparer.Ordinal)
        {
            [CamposOrdenacaoOfertaCurso.CursoNome] = new(
                d => KeysetSortColumn<OfertaCursoOrdenada>.For(
                    CamposOrdenacaoOfertaCurso.CursoNome, o => o.NomeOrdenacao, o => o.NomeOrdenacao, d),
                (oferta, parte) => oferta with { NomeOrdenacao = parte }),
            [CamposOrdenacaoOfertaCurso.CursoCodigo] = new(
                d => KeysetSortColumn<OfertaCursoOrdenada>.For(
                    CamposOrdenacaoOfertaCurso.CursoCodigo, o => o.Codigo, o => o.Codigo, d),
                (oferta, parte) => oferta with { Codigo = parte }),
            [CamposOrdenacaoOfertaCurso.UnidadeOfertanteSigla] = new(
                d => KeysetSortColumn<OfertaCursoOrdenada>.For(
                    CamposOrdenacaoOfertaCurso.UnidadeOfertanteSigla, o => o.UnidadeSigla, o => o.UnidadeSigla, d),
                (oferta, parte) => oferta with { UnidadeSigla = parte }),
            [CamposOrdenacaoOfertaCurso.ProgramaDeOferta] = new(
                d => KeysetSortColumn<OfertaCursoOrdenada>.For(
                    CamposOrdenacaoOfertaCurso.ProgramaDeOferta, o => o.ProgramaDeOferta, o => o.ProgramaDeOferta.Codigo(), d),
                (oferta, parte) => oferta with { ProgramaDeOferta = CatalogoDe<ProgramaDeOferta>.Obter(parte) }),
            [CamposOrdenacaoOfertaCurso.FormatoPedagogico] = new(
                d => KeysetSortColumn<OfertaCursoOrdenada>.For(
                    CamposOrdenacaoOfertaCurso.FormatoPedagogico, o => o.FormatoPedagogico, o => o.FormatoPedagogico.Codigo(), d),
                (oferta, parte) => oferta with { FormatoPedagogico = CatalogoDe<FormatoPedagogico>.Obter(parte) }),
            [CamposOrdenacaoOfertaCurso.RegimeDeFuncionamento] = new(
                d => KeysetSortColumn<OfertaCursoOrdenada>.For(
                    CamposOrdenacaoOfertaCurso.RegimeDeFuncionamento,
                    o => o.RegimeDeFuncionamento,
                    o => RegimesDeFuncionamento.ParaTokenCanonico(o.RegimeDeFuncionamento),
                    d),
                (oferta, parte) => oferta with { RegimeDeFuncionamento = RegimeDeFuncionamentoDoCursor(parte) }),
            [CamposOrdenacaoOfertaCurso.RegimeDeTurno] = new(
                d => KeysetSortColumn<OfertaCursoOrdenada>.For(
                    CamposOrdenacaoOfertaCurso.RegimeDeTurno,
                    o => o.RegimeDeTurno,
                    o => RegimesDeTurno.ParaTokenCanonico(o.RegimeDeTurno),
                    d),
                (oferta, parte) => oferta with { RegimeDeTurno = RegimeDeTurnoDoCursor(parte) }),
            [CamposOrdenacaoOfertaCurso.CriadoEm] = new(
                d => KeysetSortColumn<OfertaCursoOrdenada>.For(
                    CamposOrdenacaoOfertaCurso.CriadoEm, o => o.CriadoEm, o => ChaveDeInstante(o.CriadoEm), d),
                (oferta, parte) => oferta with { CriadoEm = InstanteDaChave(parte) }),
        };

    /// <summary>Nomes de campo que a listagem de Cursos sabe ordenar.</summary>
    public static IReadOnlyList<string> CamposDeCursoMapeados { get; } =
        [.. CamposDeCurso.Keys];

    /// <summary>Nomes de campo que a listagem de Ofertas de Curso sabe ordenar.</summary>
    public static IReadOnlyList<string> CamposDeOfertaMapeados { get; } =
        [.. CamposDeOferta.Keys];

    private static KeysetSortColumn<CursoOrdenado> ColunaDeCurso(SortField campo) =>
        CamposDeCurso[campo.Campo](campo.Direcao);

    private static KeysetSortColumn<OfertaCursoOrdenada> ColunaDeOferta(SortField campo) =>
        CamposDeOferta[campo.Campo].Coluna(campo.Direcao);

    /// <summary>
    /// Instante em ISO-8601 com precisão de milissegundo e deslocamento zero. A
    /// ordem lexicográfica desse formato coincide com a cronológica, que é o que o
    /// cursor precisa para comparar como o banco compara.
    /// </summary>
    private static string ChaveDeInstante(DateTimeOffset instante) =>
        instante.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static CursoOrdenado CursoLidoDoCursor(Dictionary<string, string> valores, Guid id) =>
        new()
        {
            Id = id,
            NomeOrdenacao = valores.GetValueOrDefault(CamposOrdenacaoCurso.Nome, string.Empty),
            Codigo = valores.GetValueOrDefault(CamposOrdenacaoCurso.Codigo, string.Empty),
            Grau = valores.GetValueOrDefault(CamposOrdenacaoCurso.Grau, string.Empty),
            NivelEnsino = valores.GetValueOrDefault(CamposOrdenacaoCurso.NivelEnsino, string.Empty),
            CriadoEm = InstanteDaChave(valores.GetValueOrDefault(CamposOrdenacaoCurso.CriadoEm)),
        };

    // A linha só leva as colunas da ordenação pedida e o Id: a paginação por chave não lê as outras.
    private static OfertaCursoOrdenada OfertaLidaDoCursor(IReadOnlyList<SortField> campos, IReadOnlyList<string> partes, Guid id)
    {
        OfertaCursoOrdenada oferta = new() { Id = id };
        for (int i = 0; i < campos.Count && i < partes.Count; i++)
        {
            oferta = CamposDeOferta[campos[i].Campo].Reconstituir(oferta, partes[i]);
        }

        return oferta;
    }

    private static RegimeDeFuncionamento RegimeDeFuncionamentoDoCursor(string parte) =>
        RegimesDeFuncionamento.TryAnalisar(parte, out RegimeDeFuncionamento regime)
            ? regime
            : default;

    private static RegimeDeTurno RegimeDeTurnoDoCursor(string parte) =>
        RegimesDeTurno.TryAnalisar(parte, out RegimeDeTurno turno)
            ? turno
            : default;

    private static Dictionary<string, string> ValoresPorCampo(
        IReadOnlyList<SortField> campos,
        IReadOnlyList<string> partes)
    {
        Dictionary<string, string> valores = new(StringComparer.Ordinal);
        for (int i = 0; i < campos.Count && i < partes.Count; i++)
        {
            valores[campos[i].Campo] = partes[i];
        }

        return valores;
    }

    private static DateTimeOffset InstanteDaChave(string? bruto) =>
        DateTimeOffset.TryParse(
            bruto,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out DateTimeOffset instante)
            ? instante
            : default;
}
