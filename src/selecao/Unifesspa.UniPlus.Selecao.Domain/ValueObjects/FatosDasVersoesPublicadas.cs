namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O que os formulários do processo coletaram nas versões publicadas do edital, lido na abertura
/// da retificação (UNI-REQ-0144). Cada finalidade fixa a versão no nascimento do rascunho do
/// candidato (UNI-REQ-0084), então quem já preencheu um formulário pode tê-lo feito em qualquer
/// versão em que ele existia: só o fato coletado em todas elas é garantido para quem vem depois.
/// </summary>
/// <remarks>
/// <para>
/// Os fatos produzidos por uma finalidade numa versão são os itens, os grupos e os campos de grupo
/// do formulário dela, quando a versão o tem. Os campos de grupo entram porque o agregado sobre
/// grupo repetível depende do fato de membro.
/// </para>
/// <para>
/// A versão base é a última, a vigente quando a sessão abre. A inscrição guarda dela também o papel
/// de cada fato, item ou campo de qual grupo, que a retificação não muda.
/// </para>
/// <para>
/// As regras de derivação das versões não entram: o derivado usado por uma finalidade é calculado
/// pela versão dela, a da sessão; o que varia entre as versões é só quais fatos a finalidade
/// anterior coletou.
/// </para>
/// </remarks>
public sealed class FatosDasVersoesPublicadas
{
    private readonly SortedDictionary<FinalidadeFormulario, SortedSet<string>> _garantidos;
    private readonly SortedDictionary<string, SortedSet<string>> _gruposDaInscricaoNaBase;

    private FatosDasVersoesPublicadas(
        SortedDictionary<FinalidadeFormulario, SortedSet<string>> garantidos,
        SortedSet<string> itensDaInscricaoNaBase,
        SortedDictionary<string, SortedSet<string>> gruposDaInscricaoNaBase)
    {
        _garantidos = garantidos;
        ItensDaInscricaoNaBase = itensDaInscricaoNaBase;
        _gruposDaInscricaoNaBase = gruposDaInscricaoNaBase;
    }

    /// <summary>
    /// Por finalidade que teve formulário em alguma versão, os fatos que ele coletou em todas as
    /// versões em que existia.
    /// </summary>
    public IReadOnlyDictionary<FinalidadeFormulario, IReadOnlySet<string>> Garantidos =>
        _garantidos.ToDictionary(static par => par.Key, static par => (IReadOnlySet<string>)par.Value);

    /// <summary>Os itens que o formulário de inscrição coleta na versão base.</summary>
    public IReadOnlySet<string> ItensDaInscricaoNaBase { get; }

    /// <summary>Os grupos que o formulário de inscrição coleta na versão base, cada um com os códigos dos seus campos.</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> GruposDaInscricaoNaBase =>
        _gruposDaInscricaoNaBase.ToDictionary(static par => par.Key, static par => (IReadOnlySet<string>)par.Value, StringComparer.Ordinal);

    /// <summary>Lê as versões publicadas, da primeira à base, que é a última.</summary>
    public static FatosDasVersoesPublicadas De(IReadOnlyList<GrafoConfiguracao> versoes)
    {
        ArgumentNullException.ThrowIfNull(versoes);
        if (versoes.Count == 0)
        {
            throw new ArgumentException("A retificação é aberta sobre ao menos uma versão publicada.", nameof(versoes));
        }

        SortedDictionary<FinalidadeFormulario, SortedSet<string>> garantidos = [];
        foreach (GrafoConfiguracao versao in versoes)
        {
            foreach (FinalidadeFormulario finalidade in versao.Formularios.Select(static f => f.Finalidade))
            {
                SortedSet<string> produzidos = Produzidos(versao, finalidade);
                if (garantidos.TryGetValue(finalidade, out SortedSet<string>? acumulado))
                {
                    acumulado.IntersectWith(produzidos);
                }
                else
                {
                    garantidos[finalidade] = produzidos;
                }
            }
        }

        GrafoConfiguracao versaoBase = versoes[^1];
        return new FatosDasVersoesPublicadas(
            garantidos,
            new SortedSet<string>(
                versaoBase.FatosColetados.Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao).Select(static f => f.FatoCodigo),
                StringComparer.Ordinal),
            new SortedDictionary<string, SortedSet<string>>(
                versaoBase.GruposColetados
                    .Where(static g => g.Finalidade == FinalidadeFormulario.Inscricao)
                    .ToDictionary(static g => g.Codigo, static g => new SortedSet<string>(g.Subitens.Select(static s => s.FatoCodigo), StringComparer.Ordinal)),
                StringComparer.Ordinal));
    }

    /// <summary>Repõe o que foi lido na abertura, como a persistência o guardou.</summary>
    public static FatosDasVersoesPublicadas Reconstituir(
        IReadOnlyDictionary<FinalidadeFormulario, IReadOnlyCollection<string>> garantidos,
        IReadOnlyCollection<string> itensDaInscricaoNaBase,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> gruposDaInscricaoNaBase)
    {
        ArgumentNullException.ThrowIfNull(garantidos);
        ArgumentNullException.ThrowIfNull(itensDaInscricaoNaBase);
        ArgumentNullException.ThrowIfNull(gruposDaInscricaoNaBase);

        return new FatosDasVersoesPublicadas(
            new SortedDictionary<FinalidadeFormulario, SortedSet<string>>(
                garantidos.ToDictionary(static par => par.Key, static par => new SortedSet<string>(par.Value, StringComparer.Ordinal))),
            new SortedSet<string>(itensDaInscricaoNaBase, StringComparer.Ordinal),
            new SortedDictionary<string, SortedSet<string>>(
                gruposDaInscricaoNaBase.ToDictionary(static par => par.Key, static par => new SortedSet<string>(par.Value, StringComparer.Ordinal), StringComparer.Ordinal),
                StringComparer.Ordinal));
    }

    /// <summary>
    /// Se o fato produzido pela finalidade foi coletado por ela em todas as versões publicadas em que
    /// ela tinha formulário. A finalidade sem formulário em nenhuma versão garante o que produz: o
    /// formulário dela nasce nesta retificação, e ninguém o preencheu antes.
    /// </summary>
    public bool Garante(FinalidadeFormulario finalidade, string fato) =>
        !_garantidos.TryGetValue(finalidade, out SortedSet<string>? garantidos) || garantidos.Contains(fato);

    /// <summary>
    /// O primeiro fato que a inscrição coletava na versão base e que os itens e os grupos dados
    /// deixam de coletar no mesmo papel: o item continua item, e o campo de grupo continua no grupo
    /// de mesmo código. Nulo quando todos continuam.
    /// </summary>
    public string? PrimeiroDaInscricaoQueDeixaDeSerColetado(IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(grupos);

        HashSet<string> itensColetados = new(itens.Select(static f => f.FatoCodigo), StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> camposPorGrupo = grupos.ToDictionary(
            static g => g.Codigo, static g => new HashSet<string>(g.Subitens.Select(static s => s.FatoCodigo), StringComparer.Ordinal), StringComparer.Ordinal);

        if (ItensDaInscricaoNaBase.FirstOrDefault(item => !itensColetados.Contains(item)) is { } item)
        {
            return item;
        }

        foreach ((string grupo, SortedSet<string> campos) in _gruposDaInscricaoNaBase)
        {
            if (!camposPorGrupo.TryGetValue(grupo, out HashSet<string>? enviados))
            {
                return grupo;
            }

            if (campos.FirstOrDefault(campo => !enviados.Contains(campo)) is { } campo)
            {
                return campo;
            }
        }

        return null;
    }

    /// <summary>Os itens, os grupos e os campos de grupo que o formulário da finalidade coleta na versão.</summary>
    private static SortedSet<string> Produzidos(GrafoConfiguracao versao, FinalidadeFormulario finalidade)
    {
        IEnumerable<GrupoColetado> grupos = versao.GruposColetados.Where(g => g.Finalidade == finalidade);
        return new SortedSet<string>(
            versao.FatosColetados.Where(f => f.Finalidade == finalidade).Select(static f => f.FatoCodigo)
                .Concat(grupos.Select(static g => g.Codigo))
                .Concat(grupos.SelectMany(static g => g.Subitens).Select(static s => s.FatoCodigo)),
            StringComparer.Ordinal);
    }
}
