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
    private readonly Dictionary<string, HashSet<EixosDoItemCongelado>> _eixosDosItens;
    private readonly Dictionary<string, HashSet<EixosDoMetadadoCongelado>> _eixosDosMetadados;
    private readonly Dictionary<string, HashSet<string>> _operacoesDosAgregados;

    private FatosDasVersoesPublicadas(
        SortedDictionary<FinalidadeFormulario, SortedSet<string>> garantidos,
        SortedSet<string> itensDaInscricaoNaBase,
        SortedDictionary<string, SortedSet<string>> gruposDaInscricaoNaBase,
        Dictionary<string, HashSet<EixosDoItemCongelado>> eixosDosItens,
        Dictionary<string, HashSet<EixosDoMetadadoCongelado>> eixosDosMetadados,
        Dictionary<string, HashSet<string>> operacoesDosAgregados)
    {
        _garantidos = garantidos;
        ItensDaInscricaoNaBase = itensDaInscricaoNaBase;
        _gruposDaInscricaoNaBase = gruposDaInscricaoNaBase;
        _eixosDosItens = eixosDosItens;
        _eixosDosMetadados = eixosDosMetadados;
        _operacoesDosAgregados = operacoesDosAgregados;
    }

    /// <summary>
    /// Por fato coletado em alguma versão publicada, os eixos que cada versão congelou no item,
    /// sem repetição: o item do fato num grupo repetível entra como o de qualquer outro.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<EixosDoItemCongelado>> EixosDosItens =>
        _eixosDosItens.ToDictionary(static par => par.Key, static par => (IReadOnlyCollection<EixosDoItemCongelado>)par.Value, StringComparer.Ordinal);

    /// <summary>Por fato citado por condição de documento exigido, os eixos que as versões congelaram dele.</summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<EixosDoMetadadoCongelado>> EixosDosMetadados =>
        _eixosDosMetadados.ToDictionary(static par => par.Key, static par => (IReadOnlyCollection<EixosDoMetadadoCongelado>)par.Value, StringComparer.Ordinal);

    /// <summary>Por agregado, as operações que as versões congelaram.</summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> OperacoesDosAgregados =>
        _operacoesDosAgregados.ToDictionary(static par => par.Key, static par => (IReadOnlyCollection<string>)par.Value, StringComparer.Ordinal);

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

    /// <summary>
    /// Lê as versões publicadas, da primeira à base, que é a última. <paramref name="congeladoPorVersao"/>
    /// traz, na mesma ordem, o que cada uma congelou além dos itens: é obrigatório para que a guarda
    /// de eixo do metadado e da operação do agregado nunca fique sem ser alimentada em silêncio.
    /// </summary>
    public static FatosDasVersoesPublicadas De(
        IReadOnlyList<GrafoConfiguracao> versoes, IReadOnlyList<EixosDeVersaoPublicada> congeladoPorVersao)
    {
        ArgumentNullException.ThrowIfNull(versoes);
        ArgumentNullException.ThrowIfNull(congeladoPorVersao);
        if (congeladoPorVersao.Count != versoes.Count)
        {
            throw new ArgumentException("O congelado de cada versão publicada é informado, na mesma ordem das versões.", nameof(congeladoPorVersao));
        }

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

        Dictionary<string, HashSet<EixosDoItemCongelado>> eixosDosItens = new(StringComparer.Ordinal);
        foreach (GrafoConfiguracao versao in versoes)
        {
            foreach (FatoColetado item in versao.FatosColetados.Concat(versao.GruposColetados.SelectMany(static g => g.Subitens)))
            {
                Acrescentar(eixosDosItens, item.FatoCodigo, EixosDoItemCongelado.De(item));
            }
        }

        Dictionary<string, HashSet<EixosDoMetadadoCongelado>> eixosDosMetadados = new(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> operacoes = new(StringComparer.Ordinal);
        foreach (EixosDeVersaoPublicada congelado in congeladoPorVersao)
        {
            foreach ((string fato, EixosDoMetadadoCongelado eixos) in congelado.Metadados)
            {
                Acrescentar(eixosDosMetadados, fato, eixos);
            }

            foreach ((string agregado, string operacao) in congelado.OperacaoDosAgregados)
            {
                Acrescentar(operacoes, agregado, operacao);
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
                StringComparer.Ordinal),
            eixosDosItens,
            eixosDosMetadados,
            operacoes);
    }

    /// <summary>Repõe o que foi lido na abertura, como a persistência o guardou.</summary>
    public static FatosDasVersoesPublicadas Reconstituir(
        IReadOnlyDictionary<FinalidadeFormulario, IReadOnlyCollection<string>> garantidos,
        IReadOnlyCollection<string> itensDaInscricaoNaBase,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> gruposDaInscricaoNaBase,
        IReadOnlyDictionary<string, IReadOnlyCollection<EixosDoItemCongelado>>? eixosDosItens = null,
        IReadOnlyDictionary<string, IReadOnlyCollection<EixosDoMetadadoCongelado>>? eixosDosMetadados = null,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? operacoesDosAgregados = null)
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
                StringComparer.Ordinal),
            (eixosDosItens ?? new Dictionary<string, IReadOnlyCollection<EixosDoItemCongelado>>()).ToDictionary(static par => par.Key, static par => par.Value.ToHashSet(), StringComparer.Ordinal),
            (eixosDosMetadados ?? new Dictionary<string, IReadOnlyCollection<EixosDoMetadadoCongelado>>()).ToDictionary(static par => par.Key, static par => par.Value.ToHashSet(), StringComparer.Ordinal),
            (operacoesDosAgregados ?? new Dictionary<string, IReadOnlyCollection<string>>()).ToDictionary(static par => par.Key, static par => par.Value.ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal));
    }

    private static void Acrescentar<T>(Dictionary<string, HashSet<T>> destino, string chave, T valor)
    {
        if (!destino.TryGetValue(chave, out HashSet<T>? existentes))
        {
            destino[chave] = existentes = [];
        }

        existentes.Add(valor);
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

    /// <summary>
    /// O primeiro item ou campo de grupo dado que um candidato pode já ter respondido e cujo eixo
    /// difere do que alguma versão publicada congelou. O que o item guarda é a cópia tirada do
    /// catálogo quando ele foi definido, então só o item regravado pode trocar de eixo; a
    /// descrição, a orientação e a ordem de um valor não são eixo.
    /// </summary>
    public TrocaDeEixo? PrimeiroItemComEixoTrocado(IEnumerable<FatoColetado> itens, IEnumerable<GrupoColetado> grupos)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(grupos);

        foreach (FatoColetado item in itens.Concat(grupos.SelectMany(static g => g.Subitens)))
        {
            if (!_eixosDosItens.TryGetValue(item.FatoCodigo, out HashSet<EixosDoItemCongelado>? publicados))
            {
                continue;
            }

            EixosDoItemCongelado novo = EixosDoItemCongelado.De(item);
            foreach (EixosDoItemCongelado publicado in publicados)
            {
                if (TrocaDeEixo.Primeira(item.FatoCodigo, publicado.Eixos(), novo.Eixos()) is { } troca)
                {
                    return troca;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// O primeiro fato já coletado cujo metadado, ou o agregado cuja operação, a sessão congelaria
    /// diferente do que alguma versão publicada congelou. Recebe o que a versão nova vai congelar,
    /// nunca o catálogo vivo: o fato que a versão nova não cita numa condição não é comparado.
    /// </summary>
    public TrocaDeEixo? PrimeiroEixoCongeladoTrocado(
        IReadOnlyDictionary<string, EixosDoMetadadoCongelado> metadados, IReadOnlyDictionary<string, string> operacaoDosAgregados)
    {
        ArgumentNullException.ThrowIfNull(metadados);
        ArgumentNullException.ThrowIfNull(operacaoDosAgregados);

        foreach ((string fato, EixosDoMetadadoCongelado novo) in metadados.OrderBy(static par => par.Key, StringComparer.Ordinal))
        {
            if (!_eixosDosItens.ContainsKey(fato) || !_eixosDosMetadados.TryGetValue(fato, out HashSet<EixosDoMetadadoCongelado>? publicados))
            {
                continue;
            }

            foreach (EixosDoMetadadoCongelado publicado in publicados)
            {
                if (TrocaDeEixo.Primeira(fato, publicado.Eixos(), novo.Eixos()) is { } troca)
                {
                    return troca;
                }
            }
        }

        foreach ((string agregado, string novo) in operacaoDosAgregados.OrderBy(static par => par.Key, StringComparer.Ordinal))
        {
            if (_operacoesDosAgregados.TryGetValue(agregado, out HashSet<string>? publicadas)
                && publicadas.FirstOrDefault(p => !string.Equals(p, novo, StringComparison.Ordinal)) is { } diferente)
            {
                return new TrocaDeEixo(agregado, "operação do agregado", diferente, novo, DeAgregado: true);
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
