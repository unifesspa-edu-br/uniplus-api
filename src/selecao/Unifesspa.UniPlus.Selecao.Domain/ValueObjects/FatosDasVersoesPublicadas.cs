namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

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
/// A versão base é a última, a vigente quando a retificação parte. A inscrição guarda dela também o papel
/// de cada fato, item ou campo de qual grupo, que a retificação não muda.
/// </para>
/// <para>
/// As regras de derivação das versões não entram: o derivado usado por uma finalidade é calculado
/// pela versão dela, a da retificação; o que varia entre as versões é só quais fatos a finalidade
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
        FatosJaColetados = eixosDosItens.Keys.ToHashSet(StringComparer.Ordinal);
        _eixosDosMetadados = eixosDosMetadados;
        _operacoesDosAgregados = operacoesDosAgregados;
    }

    /// <summary>
    /// Por fato coletado em alguma versão publicada, os eixos que cada versão congelou no item,
    /// sem repetição: o item do fato num grupo repetível entra como o de qualquer outro.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<EixosDoItemCongelado>> EixosDosItens =>
        _eixosDosItens.ToDictionary(static par => par.Key, static par => (IReadOnlyCollection<EixosDoItemCongelado>)par.Value, StringComparer.Ordinal);

    /// <summary>
    /// Os eixos que as versões publicadas congelaram no item do fato. A retificação só aceita o item igual a
    /// todos eles, então, sem divergência entre as versões, qualquer um serve de fonte do que o item
    /// regravado copia.
    /// </summary>
    public bool TentarObterEixosDoItem(string fatoCodigo, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out EixosDoItemCongelado? eixos)
    {
        eixos = _eixosDosItens.TryGetValue(fatoCodigo, out HashSet<EixosDoItemCongelado>? congelados) ? congelados.FirstOrDefault() : null;
        return eixos is not null;
    }

    /// <summary>Os fatos que alguma versão publicada coletou, em item ou em campo de grupo.</summary>
    public IReadOnlySet<string> FatosJaColetados { get; }

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
        IReadOnlyList<ConfiguracaoPublicada> versoes, IReadOnlyList<EixosDeVersaoPublicada> congeladoPorVersao)
    {
        ConferirVersoes(versoes, congeladoPorVersao);
        ConfiguracaoPublicada versaoBase = versoes[^1];
        return new FatosDasVersoesPublicadas(GarantidosDasVersoes(versoes),
            new SortedSet<string>(versaoBase.Coleta.Itens.Where(static f => f.Finalidade == FinalidadeFormulario.Inscricao)
                .Select(static f => f.FatoCodigo), StringComparer.Ordinal), GruposDaInscricao(versaoBase),
            EixosDosItensDasVersoes(versoes),
            PorChave(congeladoPorVersao.SelectMany(static v => v.Metadados)),
            PorChave(congeladoPorVersao.SelectMany(static v => v.OperacaoDosAgregados)));
    }

    private static void ConferirVersoes(IReadOnlyList<ConfiguracaoPublicada> versoes, IReadOnlyList<EixosDeVersaoPublicada> congeladoPorVersao)
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
    }

    private static SortedDictionary<string, SortedSet<string>> GruposDaInscricao(ConfiguracaoPublicada versao) => new(
        versao.Coleta.GruposColetados.Where(static g => g.Finalidade == FinalidadeFormulario.Inscricao)
            .ToDictionary(static g => g.Codigo, static g => new SortedSet<string>(g.Subitens.Select(static s => s.FatoCodigo), StringComparer.Ordinal)),
        StringComparer.Ordinal);

    private static SortedDictionary<FinalidadeFormulario, SortedSet<string>> GarantidosDasVersoes(IReadOnlyList<ConfiguracaoPublicada> versoes)
    {
        SortedDictionary<FinalidadeFormulario, SortedSet<string>> garantidos = [];
        foreach (ConfiguracaoPublicada versao in versoes)
        {
            AcumularGarantidos(versao, garantidos);
        }
        return garantidos;
    }

    private static void AcumularGarantidos(ConfiguracaoPublicada versao, SortedDictionary<FinalidadeFormulario, SortedSet<string>> garantidos)
    {
        foreach (FinalidadeFormulario finalidade in versao.Coleta.Formularios.Select(static f => f.Finalidade))
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

    private static Dictionary<string, HashSet<EixosDoItemCongelado>> EixosDosItensDasVersoes(IReadOnlyList<ConfiguracaoPublicada> versoes)
    {
        Dictionary<string, HashSet<EixosDoItemCongelado>> eixos = new(StringComparer.Ordinal);
        foreach (FatoColetado item in versoes.SelectMany(static v => v.Coleta.Campos))
        {
            Acrescentar(eixos, item.FatoCodigo, EixosDoItemCongelado.De(item));
        }
        return eixos;
    }

    private static Dictionary<string, HashSet<T>> PorChave<T>(IEnumerable<KeyValuePair<string, T>> pares)
    {
        Dictionary<string, HashSet<T>> acumulados = new(StringComparer.Ordinal);
        foreach ((string chave, T valor) in pares)
        {
            Acrescentar(acumulados, chave, valor);
        }
        return acumulados;
    }

    /// <summary>Reconstitui o que foi lido das versões publicadas, como foi guardado.</summary>
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

    /// <summary>Se o fato produzido pelo formulário do código foi coletado por ele em todas as versões em que existia; o código sem formulário em nenhuma versão garante o que produz.</summary>
    public bool Garante(CodigoDoFormulario formulario, string fato)
    {
        ArgumentNullException.ThrowIfNull(formulario);

        // Provisório até a retirada da finalidade: as versões ainda guardam o que cada finalidade garante.
        return EstruturaFormulario.FinalidadeDoCodigo(formulario.Valor) is not { } finalidade || Garante(finalidade, fato);
    }

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

        return PrimeiroDoGrupoQueDeixaDeSerColetado(camposPorGrupo);
    }

    private string? PrimeiroDoGrupoQueDeixaDeSerColetado(Dictionary<string, HashSet<string>> camposPorGrupo)
    {
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
    /// Cada fato já coletado cujo metadado, e cada agregado cuja operação, a versão nova congelaria diferente do que alguma versão
    /// publicada congelou: primeiro os metadados e depois os agregados, cada grupo na ordem de entrada, uma troca por fato.
    /// Recebe o que a versão nova vai congelar, nunca o catálogo vivo: o fato que a versão nova não cita numa condição não é
    /// comparado.
    /// </summary>
    public IReadOnlyList<TrocaDeEixo> EixosCongeladosTrocados(
        IReadOnlyDictionary<string, EixosDoMetadadoCongelado> metadados, IReadOnlyDictionary<string, string> operacaoDosAgregados)
    {
        ArgumentNullException.ThrowIfNull(metadados);
        ArgumentNullException.ThrowIfNull(operacaoDosAgregados);

        return
        [
            .. metadados.Select(par => TrocaDoMetadado(par.Key, par.Value)).OfType<TrocaDeEixo>(),
            .. operacaoDosAgregados.Select(par => TrocaDaOperacao(par.Key, par.Value)).OfType<TrocaDeEixo>(),
        ];
    }

    private TrocaDeEixo? TrocaDoMetadado(string fato, EixosDoMetadadoCongelado novo) =>
        _eixosDosItens.ContainsKey(fato) && _eixosDosMetadados.TryGetValue(fato, out HashSet<EixosDoMetadadoCongelado>? publicados)
            ? publicados.Select(publicado => TrocaDeEixo.Primeira(fato, publicado.Eixos(), novo.Eixos())).OfType<TrocaDeEixo>().FirstOrDefault()
            : null;

    private TrocaDeEixo? TrocaDaOperacao(string agregado, string novo) =>
        _operacoesDosAgregados.TryGetValue(agregado, out HashSet<string>? publicadas)
        && publicadas.FirstOrDefault(publicada => !string.Equals(publicada, novo, StringComparison.Ordinal)) is { } diferente
            ? new TrocaDeEixo(agregado, "operação do agregado", diferente, novo, DeAgregado: true)
            : null;

    /// <summary>Os itens, os grupos e os campos de grupo que o formulário da finalidade coleta na versão.</summary>
    private static SortedSet<string> Produzidos(ConfiguracaoPublicada versao, FinalidadeFormulario finalidade)
    {
        IEnumerable<GrupoColetado> grupos = versao.Coleta.GruposColetados.Where(g => g.Finalidade == finalidade);
        return new SortedSet<string>(
            versao.Coleta.Itens.Where(f => f.Finalidade == finalidade).Select(static f => f.FatoCodigo)
                .Concat(grupos.Select(static g => g.Codigo))
                .Concat(grupos.SelectMany(static g => g.Subitens).Select(static s => s.FatoCodigo)),
            StringComparer.Ordinal);
    }
}
