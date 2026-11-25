namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;

/// <summary>Um item do formulário, no que o grafo confere: o fato, a ordem, a seção e os fatos que as regras dele citam.</summary>
public sealed record ItemDoGrafo(string FatoCodigo, int Ordem, string? PaginaCodigo, IReadOnlyCollection<string> FatosCitados);

/// <summary>Uma página do formulário, no que o grafo confere: o código, a ordem e os fatos que a exibição cita.</summary>
public sealed record PaginaDoGrafo(string Codigo, int Ordem, IReadOnlyCollection<string> FatosCitados);

/// <summary>
/// Um grupo repetível, no que o grafo confere: o código, a posição na ordem do formulário — a mesma
/// sequência dos itens —, a seção, os fatos que a exibição e a obrigatoriedade dele citam e os
/// subitens, cada um com a ordem própria dentro do grupo (UNI-REQ-0146).
/// </summary>
public sealed record GrupoDoGrafo(
    string Codigo, int Ordem, string? PaginaCodigo, IReadOnlyCollection<string> FatosCitados, IReadOnlyList<ItemDoGrafo> Subitens);

/// <summary>Um termo exigido pelo formulário, no que o grafo confere: o código e os fatos que as condições citam.</summary>
public sealed record TermoDoGrafo(string Codigo, IReadOnlyCollection<string> FatosCitados);

/// <summary>
/// O grafo de coleta do formulário, o mesmo no processo e no modelo (UNI-REQ-0144, UNI-REQ-0145,
/// UNI-REQ-0146): cada fato uma vez, a ordem total, sem ciclo entre as regras, campo de grupo
/// repetível citado só dentro do próprio grupo, e cada regra — de item, de grupo, de campo do
/// grupo, de exibição de seção e de termo — citando só o que o formulário conhece antes dela.
/// </summary>
/// <remarks>
/// A primeira recusa orienta quem corrige, e por isso a ordem é fixa: fato duplicado, ordem
/// duplicada, ciclo, fato de membro citado fora do grupo, citação dos itens, dos grupos, das seções
/// e dos termos. O ciclo vem antes da citação porque nomeia o caminho inteiro; a citação apontaria
/// só o primeiro par fora de sequência do mesmo problema. O fato de membro vem antes da citação
/// porque, fora do grupo, ele não é "desconhecido": existe, mas só por ocorrência.
/// </remarks>
public static class GrafoDoFormulario
{
    /// <summary>
    /// O que as regras do formulário podem citar: os itens dele, na ordem; os fatos conhecidos antes
    /// do formulário; e os derivados por regra cujas dependências estão entre esses.
    /// </summary>
    public static DependenciasDoFormulario Dependencias(
        IEnumerable<ItemDoGrafo> itens,
        IReadOnlySet<string> conhecidosAntes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes)
    {
        ArgumentNullException.ThrowIfNull(itens);
        return DependenciasDoFormulario.Criar(
            itens.ToDictionary(static i => i.FatoCodigo, static i => i.Ordem, StringComparer.Ordinal), conhecidosAntes, derivacoes);
    }

    /// <summary>
    /// A coleta do formulário: fato uma vez, contando os subitens, e código de grupo que não é código
    /// de fato coletado, derivado ou pressuposto; ordem total entre itens e grupos, e dentro de cada grupo; sem ciclo; fato de membro só
    /// citado dentro do próprio grupo; e as regras dos itens, dos grupos e a exibição das seções
    /// citando só o que é conhecido antes delas. As dependências só são montadas depois de o fato
    /// aparecer uma vez só.
    /// </summary>
    public static DomainError? ValidarColeta(
        IReadOnlyList<ItemDoGrafo> itens,
        IReadOnlyCollection<PaginaDoGrafo> paginas,
        IReadOnlySet<string> conhecidosAntes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes,
        IReadOnlyList<GrupoDoGrafo>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(paginas);
        ArgumentNullException.ThrowIfNull(conhecidosAntes);
        ArgumentNullException.ThrowIfNull(derivacoes);
        grupos ??= [];

        DomainError? recusa = UnicidadeDaColeta.Conferir(itens, grupos, conhecidosAntes, derivacoes, out Dictionary<string, ItemDoGrafo> fatos);
        return recusa
            ?? CicloDasRegras(fatos)
            ?? CitacoesDaColeta(itens, paginas, conhecidosAntes, derivacoes, grupos);
    }

    private static DomainError? CicloDasRegras(IReadOnlyDictionary<string, ItemDoGrafo> fatos)
    {
        Dictionary<string, IReadOnlyCollection<string>> citacoes = fatos.ToDictionary(
            static par => par.Key, static par => par.Value.FatosCitados, StringComparer.Ordinal);
        return GrafoDeFatos.DetectarCiclo(citacoes) is { } caminho
            ? new DomainError(GrafoFormularioErrorCodes.GrafoComCiclo, $"As regras dos campos formam um ciclo: {string.Join(" → ", caminho)}.")
            : null;
    }

    private static DomainError? CitacoesDaColeta(
        IReadOnlyList<ItemDoGrafo> itens, IReadOnlyCollection<PaginaDoGrafo> paginas, IReadOnlySet<string> conhecidosAntes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes, IReadOnlyList<GrupoDoGrafo> grupos)
    {
        DependenciasDoFormulario dependencias = Dependencias(itens, conhecidosAntes, derivacoes);
        return CitacaoDeMembroForaDoGrupo(itens, paginas, [], grupos)
            ?? CitacaoInvalidaDosItens(itens, dependencias)
            ?? CitacaoInvalidaDosGrupos(grupos, dependencias)
            ?? CitacaoInvalidaDasPaginas(paginas, itens, dependencias, grupos);
    }

    /// <summary>
    /// A primeira regra do formulário que cita fato que ela não pode citar, nesta ordem: campo de
    /// grupo citado fora do próprio grupo, depois a citação dos itens, dos grupos e dos campos deles,
    /// da exibição das seções e dos termos.
    /// </summary>
    public static DomainError? CitacaoInvalida(
        IReadOnlyCollection<ItemDoGrafo> itens,
        IReadOnlyCollection<PaginaDoGrafo> paginas,
        IEnumerable<TermoDoGrafo> termos,
        DependenciasDoFormulario dependencias,
        IReadOnlyList<GrupoDoGrafo>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(termos);
        grupos ??= [];
        IReadOnlyList<TermoDoGrafo> listaDeTermos = [.. termos];
        return CitacaoDeMembroForaDoGrupo(itens, paginas, listaDeTermos, grupos)
            ?? CitacaoInvalidaDosItens(itens, dependencias)
            ?? CitacaoInvalidaDosGrupos(grupos, dependencias)
            ?? CitacaoInvalidaDasPaginas(paginas, itens, dependencias, grupos)
            ?? listaDeTermos.Select(t => CitacaoInvalidaDoTermo(t, dependencias)).FirstOrDefault(static e => e is not null);
    }

    /// <summary>
    /// A primeira regra que cita fato de membro fora do próprio grupo — de item, de grupo, de
    /// subitem de outro grupo, de exibição de seção ou de termo. Fora do grupo o fato de membro não
    /// tem valor único: existe uma vez por ocorrência.
    /// </summary>
    public static DomainError? CitacaoDeMembroForaDoGrupo(
        IEnumerable<ItemDoGrafo> itens, IEnumerable<PaginaDoGrafo> paginas, IEnumerable<TermoDoGrafo> termos, IReadOnlyList<GrupoDoGrafo> grupos)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(paginas);
        ArgumentNullException.ThrowIfNull(termos);
        ArgumentNullException.ThrowIfNull(grupos);

        Dictionary<string, string> grupoDoMembro = GrupoDeCadaMembro(grupos);
        if (grupoDoMembro.Count == 0)
        {
            return null;
        }

        return RegrasQueCitam(itens, paginas, termos, grupos)
            .Select(regra => regra.CitacaoForaDoGrupo(grupoDoMembro))
            .FirstOrDefault(static recusa => recusa is not null);
    }

    private static Dictionary<string, string> GrupoDeCadaMembro(IEnumerable<GrupoDoGrafo> grupos)
    {
        Dictionary<string, string> resultado = new(StringComparer.Ordinal);
        foreach (GrupoDoGrafo grupo in grupos)
        {
            foreach (ItemDoGrafo subitem in grupo.Subitens)
            {
                resultado.TryAdd(subitem.FatoCodigo, grupo.Codigo);
            }
        }

        return resultado;
    }

    private static IEnumerable<RegraQueCita> RegrasQueCitam(
        IEnumerable<ItemDoGrafo> itens, IEnumerable<PaginaDoGrafo> paginas, IEnumerable<TermoDoGrafo> termos, IEnumerable<GrupoDoGrafo> grupos) =>
        [.. itens.Select(static i => new RegraQueCita($"do fato '{i.FatoCodigo}'", i.FatosCitados, null)),
         .. grupos.Select(static g => new RegraQueCita($"do grupo '{g.Codigo}'", g.FatosCitados, null)),
         .. grupos.SelectMany(static g => g.Subitens.Select(s => new RegraQueCita($"do fato '{s.FatoCodigo}'", s.FatosCitados, g.Codigo))),
         .. paginas.Select(static e => new RegraQueCita($"de exibição da seção '{e.Codigo}'", e.FatosCitados, null)),
         .. termos.Select(static t => new RegraQueCita($"do termo '{t.Codigo}'", t.FatosCitados, null))];

    /// <summary>
    /// As regras dos grupos citam só o que o formulário conhece antes da posição do grupo; o subitem
    /// cita isso e os campos anteriores do mesmo grupo, que a ocorrência já respondeu.
    /// </summary>
    public static DomainError? CitacaoInvalidaDosGrupos(IEnumerable<GrupoDoGrafo> grupos, DependenciasDoFormulario dependencias)
    {
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(dependencias);

        return grupos.Select(grupo => new CitacoesDoGrupo(grupo, dependencias).PrimeiraRecusa())
            .FirstOrDefault(static recusa => recusa is not null);
    }

    /// <summary>A primeira regra de item que cita fato que o formulário não conhece antes do item.</summary>
    public static DomainError? CitacaoInvalidaDosItens(IEnumerable<ItemDoGrafo> itens, DependenciasDoFormulario dependencias)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(dependencias);

        return itens.Select(item => new RegraQueCita($"do fato '{item.FatoCodigo}'", item.FatosCitados, null)
                .CitacaoInvalidaDoItem(item.Ordem, dependencias))
            .FirstOrDefault(static recusa => recusa is not null);
    }

    /// <summary>
    /// A exibição da seção cita só o que o formulário conhece antes dela: antes do primeiro item ou
    /// grupo da própria seção ou, se ela está vazia, das seções seguintes.
    /// </summary>
    public static DomainError? CitacaoInvalidaDasPaginas(
        IReadOnlyCollection<PaginaDoGrafo> paginas,
        IEnumerable<ItemDoGrafo> itens,
        DependenciasDoFormulario dependencias,
        IReadOnlyList<GrupoDoGrafo>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(paginas);
        return paginas.OrderBy(static e => e.Ordem)
            .Select(pagina => CitacaoInvalidaDaPagina(pagina, PosicaoDaPagina(pagina, paginas, itens, grupos), dependencias))
            .FirstOrDefault(static e => e is not null);
    }

    /// <summary>A exibição da página, na posição dada, cita fato que o formulário não conhece antes dela.</summary>
    public static DomainError? CitacaoInvalidaDaPagina(PaginaDoGrafo pagina, long posicao, DependenciasDoFormulario dependencias)
    {
        ArgumentNullException.ThrowIfNull(pagina);
        ArgumentNullException.ThrowIfNull(dependencias);

        return pagina.FatosCitados.Select(citado => RecusaDaPagina(pagina, citado, dependencias.Conferir(citado, posicao)))
            .FirstOrDefault(static recusa => recusa is not null);
    }

    private static DomainError? RecusaDaPagina(PaginaDoGrafo pagina, string citado, RecusaDeCitacao? recusa) => recusa switch
    {
        RecusaDeCitacao.FatoNaoConhecido => new DomainError(GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
            $"A exibição da seção '{pagina.Codigo}' cita '{citado}', que o formulário não coleta nem deriva antes dela."),
        RecusaDeCitacao.FatoPosterior => new DomainError(GrafoFormularioErrorCodes.CitaFatoPosterior,
            $"A exibição da seção '{pagina.Codigo}' cita '{citado}', que só fica conhecido na própria seção ou depois dela."),
        _ => null,
    };

    /// <summary>A condição do termo cita fato que o formulário não conhece.</summary>
    public static DomainError? CitacaoInvalidaDoTermo(TermoDoGrafo termo, DependenciasDoFormulario dependencias)
    {
        ArgumentNullException.ThrowIfNull(termo);
        ArgumentNullException.ThrowIfNull(dependencias);

        return termo.FatosCitados.FirstOrDefault(f => dependencias.Conferir(f, DependenciasDoFormulario.PosicaoDosTermos) is not null) is { } citado
            ? new DomainError(
                GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
                $"A condição do termo '{termo.Codigo}' cita '{citado}', que o formulário não coleta nem deriva, nem é conhecido antes dele.")
            : null;
    }

    internal static DomainError FatoDuplicado(string codigo) =>
        new(GrafoFormularioErrorCodes.FatoDuplicado, $"O código '{codigo}' aparece mais de uma vez no formulário, entre fatos e grupos.");

    internal static DomainError OrdemDuplicada(int ordem) =>
        new(GrafoFormularioErrorCodes.OrdemDuplicada, $"A ordem {ordem} é usada por mais de um item ou grupo — a ordem de coleta precisa ser total.");

    /// <summary>A posição da página na ordem de coleta: a do primeiro item ou grupo dela ou das páginas seguintes.</summary>
    public static long PosicaoDaPagina(
        PaginaDoGrafo pagina, IEnumerable<PaginaDoGrafo> paginas, IEnumerable<ItemDoGrafo> itens, IEnumerable<GrupoDoGrafo>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(pagina);
        ArgumentNullException.ThrowIfNull(paginas);
        ArgumentNullException.ThrowIfNull(itens);

        HashSet<string> daquiEmDiante = new(paginas.Where(e => e.Ordem >= pagina.Ordem).Select(static e => e.Codigo), StringComparer.Ordinal);
        return itens.Select(static i => (i.PaginaCodigo, i.Ordem))
            .Concat((grupos ?? []).Select(static g => (g.PaginaCodigo, g.Ordem)))
            .Where(p => p.PaginaCodigo is { } codigo && daquiEmDiante.Contains(codigo))
            .Select(static p => (long)p.Ordem)
            .DefaultIfEmpty(DependenciasDoFormulario.PosicaoDosTermos)
            .Min();
    }
}
