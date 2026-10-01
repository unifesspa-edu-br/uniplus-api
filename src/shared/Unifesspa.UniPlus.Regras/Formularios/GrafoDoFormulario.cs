namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;

/// <summary>Um item do formulário, no que o grafo confere: o fato, a ordem, a seção e os fatos que as regras dele citam.</summary>
public sealed record ItemDoGrafo(string FatoCodigo, int Ordem, string? EtapaCodigo, IReadOnlyCollection<string> FatosCitados);

/// <summary>Uma etapa do formulário, no que o grafo confere: o código, a ordem e os fatos que a exibição cita.</summary>
public sealed record EtapaDoGrafo(string Codigo, int Ordem, IReadOnlyCollection<string> FatosCitados);

/// <summary>
/// Um grupo repetível, no que o grafo confere: o código, a posição na ordem do formulário — a mesma
/// sequência dos itens —, a seção, os fatos que a exibição e a obrigatoriedade dele citam e os
/// subitens, cada um com a ordem própria dentro do grupo (UNI-REQ-0146).
/// </summary>
public sealed record GrupoDoGrafo(
    string Codigo, int Ordem, string? EtapaCodigo, IReadOnlyCollection<string> FatosCitados, IReadOnlyList<ItemDoGrafo> Subitens);

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
        IReadOnlyCollection<EtapaDoGrafo> etapas,
        IReadOnlySet<string> conhecidosAntes,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes,
        IReadOnlyList<GrupoDoGrafo>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(conhecidosAntes);
        ArgumentNullException.ThrowIfNull(derivacoes);
        grupos ??= [];

        Dictionary<string, ItemDoGrafo> porCodigo = new(StringComparer.Ordinal);
        HashSet<int> ordens = [];
        foreach (ItemDoGrafo item in itens)
        {
            if (!porCodigo.TryAdd(item.FatoCodigo, item))
            {
                return FatoDuplicado(item.FatoCodigo);
            }

            if (!ordens.Add(item.Ordem))
            {
                return OrdemDuplicada(item.Ordem);
            }
        }

        HashSet<string> codigosDeGrupo = new(StringComparer.Ordinal);
        foreach (GrupoDoGrafo grupo in grupos)
        {
            if (porCodigo.ContainsKey(grupo.Codigo)
                || derivacoes.ContainsKey(grupo.Codigo)
                || conhecidosAntes.Contains(grupo.Codigo)
                || !codigosDeGrupo.Add(grupo.Codigo))
            {
                return FatoDuplicado(grupo.Codigo);
            }

            if (!ordens.Add(grupo.Ordem))
            {
                return OrdemDuplicada(grupo.Ordem);
            }

            HashSet<int> ordensNoGrupo = [];
            foreach (ItemDoGrafo subitem in grupo.Subitens)
            {
                if (codigosDeGrupo.Contains(subitem.FatoCodigo) || !porCodigo.TryAdd(subitem.FatoCodigo, subitem))
                {
                    return FatoDuplicado(subitem.FatoCodigo);
                }

                if (!ordensNoGrupo.Add(subitem.Ordem))
                {
                    return new DomainError(
                        GrafoFormularioErrorCodes.OrdemDuplicada,
                        $"A ordem {subitem.Ordem} é usada por mais de um campo do grupo '{grupo.Codigo}' — a ordem dentro do grupo precisa ser total.");
                }
            }
        }

        Dictionary<string, IReadOnlyCollection<string>> citacoes = porCodigo.ToDictionary(
            static par => par.Key, static par => par.Value.FatosCitados, StringComparer.Ordinal);
        if (GrafoDeFatos.DetectarCiclo(citacoes) is { } caminho)
        {
            return new DomainError(GrafoFormularioErrorCodes.GrafoComCiclo, $"As regras dos campos formam um ciclo: {string.Join(" → ", caminho)}.");
        }

        DependenciasDoFormulario dependencias = Dependencias(itens, conhecidosAntes, derivacoes);
        return CitacaoDeMembroForaDoGrupo(itens, etapas, [], grupos)
            ?? CitacaoInvalidaDosItens(itens, dependencias)
            ?? CitacaoInvalidaDosGrupos(grupos, dependencias)
            ?? CitacaoInvalidaDasEtapas(etapas, itens, dependencias, grupos);
    }

    /// <summary>
    /// A primeira regra do formulário que cita fato que ela não pode citar, nesta ordem: campo de
    /// grupo citado fora do próprio grupo, depois a citação dos itens, dos grupos e dos campos deles,
    /// da exibição das seções e dos termos.
    /// </summary>
    public static DomainError? CitacaoInvalida(
        IReadOnlyCollection<ItemDoGrafo> itens,
        IReadOnlyCollection<EtapaDoGrafo> etapas,
        IEnumerable<TermoDoGrafo> termos,
        DependenciasDoFormulario dependencias,
        IReadOnlyList<GrupoDoGrafo>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(termos);
        grupos ??= [];
        IReadOnlyList<TermoDoGrafo> listaDeTermos = [.. termos];
        return CitacaoDeMembroForaDoGrupo(itens, etapas, listaDeTermos, grupos)
            ?? CitacaoInvalidaDosItens(itens, dependencias)
            ?? CitacaoInvalidaDosGrupos(grupos, dependencias)
            ?? CitacaoInvalidaDasEtapas(etapas, itens, dependencias, grupos)
            ?? listaDeTermos.Select(t => CitacaoInvalidaDoTermo(t, dependencias)).FirstOrDefault(static e => e is not null);
    }

    /// <summary>
    /// A primeira regra que cita fato de membro fora do próprio grupo — de item, de grupo, de
    /// subitem de outro grupo, de exibição de seção ou de termo. Fora do grupo o fato de membro não
    /// tem valor único: existe uma vez por ocorrência.
    /// </summary>
    public static DomainError? CitacaoDeMembroForaDoGrupo(
        IEnumerable<ItemDoGrafo> itens, IEnumerable<EtapaDoGrafo> etapas, IEnumerable<TermoDoGrafo> termos, IReadOnlyList<GrupoDoGrafo> grupos)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(termos);
        ArgumentNullException.ThrowIfNull(grupos);

        Dictionary<string, string> grupoDoMembro = new(StringComparer.Ordinal);
        foreach (GrupoDoGrafo grupo in grupos)
        {
            foreach (ItemDoGrafo subitem in grupo.Subitens)
            {
                grupoDoMembro.TryAdd(subitem.FatoCodigo, grupo.Codigo);
            }
        }

        if (grupoDoMembro.Count == 0)
        {
            return null;
        }

        IEnumerable<(string Citante, IReadOnlyCollection<string> Citados, string? GrupoProprio)> regras =
        [
            .. itens.Select(static i => ($"do fato '{i.FatoCodigo}'", i.FatosCitados, (string?)null)),
            .. grupos.Select(static g => ($"do grupo '{g.Codigo}'", g.FatosCitados, (string?)null)),
            .. grupos.SelectMany(static g => g.Subitens.Select(s => ($"do fato '{s.FatoCodigo}'", s.FatosCitados, (string?)g.Codigo))),
            .. etapas.Select(static e => ($"de exibição da seção '{e.Codigo}'", e.FatosCitados, (string?)null)),
            .. termos.Select(static t => ($"do termo '{t.Codigo}'", t.FatosCitados, (string?)null)),
        ];
        foreach ((string citante, IReadOnlyCollection<string> citados, string? grupoProprio) in regras)
        {
            if (citados.FirstOrDefault(c => grupoDoMembro.TryGetValue(c, out string? doGrupo) && doGrupo != grupoProprio) is { } membro)
            {
                return new DomainError(
                    GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo,
                    $"Uma regra {citante} cita '{membro}', campo do grupo '{grupoDoMembro[membro]}' — fora do grupo ele existe uma vez por "
                    + "ocorrência e não tem valor único.");
            }
        }

        return null;
    }

    /// <summary>
    /// As regras dos grupos citam só o que o formulário conhece antes da posição do grupo; o subitem
    /// cita isso e os campos anteriores do mesmo grupo, que a ocorrência já respondeu.
    /// </summary>
    public static DomainError? CitacaoInvalidaDosGrupos(IEnumerable<GrupoDoGrafo> grupos, DependenciasDoFormulario dependencias)
    {
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(dependencias);

        foreach (GrupoDoGrafo grupo in grupos)
        {
            if (CitacaoInvalidaNaPosicao($"do grupo '{grupo.Codigo}'", grupo.FatosCitados, grupo.Ordem, dependencias) is { } doGrupo)
            {
                return doGrupo;
            }

            Dictionary<string, int> ordemNoGrupo = grupo.Subitens.ToDictionary(static s => s.FatoCodigo, static s => s.Ordem, StringComparer.Ordinal);
            foreach (ItemDoGrafo subitem in grupo.Subitens)
            {
                if (subitem.FatosCitados.FirstOrDefault(c => ordemNoGrupo.TryGetValue(c, out int ordem) && ordem >= subitem.Ordem) is { } posterior)
                {
                    return new DomainError(
                        GrafoFormularioErrorCodes.CitaFatoPosterior,
                        $"Uma regra do fato '{subitem.FatoCodigo}' cita '{posterior}', campo do grupo '{grupo.Codigo}' que vem nele ou depois dele — "
                        + "o campo dependeria de uma resposta ainda não dada na ocorrência.");
                }

                if (CitacaoInvalidaNaPosicao(
                        $"do fato '{subitem.FatoCodigo}'", [.. subitem.FatosCitados.Where(c => !ordemNoGrupo.ContainsKey(c))], grupo.Ordem, dependencias)
                    is { } doSubitem)
                {
                    return doSubitem;
                }
            }
        }

        return null;
    }

    /// <summary>A primeira regra de item que cita fato que o formulário não conhece antes do item.</summary>
    public static DomainError? CitacaoInvalidaDosItens(IEnumerable<ItemDoGrafo> itens, DependenciasDoFormulario dependencias)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(dependencias);

        foreach (ItemDoGrafo item in itens)
        {
            foreach (string citado in item.FatosCitados)
            {
                switch (dependencias.Conferir(citado, item.Ordem))
                {
                    case RecusaDeCitacao.FatoNaoConhecido:
                        return new DomainError(
                            GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
                            $"Uma regra do fato '{item.FatoCodigo}' cita '{citado}', que o formulário não coleta nem deriva, nem é conhecido antes dele.");
                    case RecusaDeCitacao.FatoPosterior:
                        return new DomainError(
                            GrafoFormularioErrorCodes.CitaFatoPosterior,
                            $"Uma regra do fato '{item.FatoCodigo}' cita '{citado}', que só fica conhecido nesse campo ou depois dele — "
                            + "o campo dependeria de uma resposta ainda não dada.");
                    case null:
                    default:
                        break;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// A exibição da seção cita só o que o formulário conhece antes dela: antes do primeiro item ou
    /// grupo da própria seção ou, se ela está vazia, das seções seguintes.
    /// </summary>
    public static DomainError? CitacaoInvalidaDasEtapas(
        IReadOnlyCollection<EtapaDoGrafo> etapas,
        IEnumerable<ItemDoGrafo> itens,
        DependenciasDoFormulario dependencias,
        IReadOnlyList<GrupoDoGrafo>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        return etapas.OrderBy(static e => e.Ordem)
            .Select(etapa => CitacaoInvalidaDaEtapa(etapa, PosicaoDaEtapa(etapa, etapas, itens, grupos), dependencias))
            .FirstOrDefault(static e => e is not null);
    }

    /// <summary>A exibição da etapa, na posição dada, cita fato que o formulário não conhece antes dela.</summary>
    public static DomainError? CitacaoInvalidaDaEtapa(EtapaDoGrafo etapa, long posicao, DependenciasDoFormulario dependencias)
    {
        ArgumentNullException.ThrowIfNull(etapa);
        ArgumentNullException.ThrowIfNull(dependencias);

        foreach (string citado in etapa.FatosCitados)
        {
            switch (dependencias.Conferir(citado, posicao))
            {
                case RecusaDeCitacao.FatoNaoConhecido:
                    return new DomainError(
                        GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
                        $"A exibição da seção '{etapa.Codigo}' cita '{citado}', que o formulário não coleta nem deriva antes dela.");
                case RecusaDeCitacao.FatoPosterior:
                    return new DomainError(
                        GrafoFormularioErrorCodes.CitaFatoPosterior,
                        $"A exibição da seção '{etapa.Codigo}' cita '{citado}', que só fica conhecido na própria seção ou depois dela.");
                case null:
                default:
                    break;
            }
        }

        return null;
    }

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

    /// <summary>A primeira citação, de uma regra na posição dada, a fato que o formulário não conhece antes dela.</summary>
    private static DomainError? CitacaoInvalidaNaPosicao(
        string citante, IEnumerable<string> citados, long posicao, DependenciasDoFormulario dependencias)
    {
        foreach (string citado in citados)
        {
            switch (dependencias.Conferir(citado, posicao))
            {
                case RecusaDeCitacao.FatoNaoConhecido:
                    return new DomainError(
                        GrafoFormularioErrorCodes.CitaFatoNaoConhecido,
                        $"Uma regra {citante} cita '{citado}', que o formulário não coleta nem deriva, nem é conhecido antes do grupo.");
                case RecusaDeCitacao.FatoPosterior:
                    return new DomainError(
                        GrafoFormularioErrorCodes.CitaFatoPosterior,
                        $"Uma regra {citante} cita '{citado}', que só fica conhecido na posição do grupo ou depois dela.");
                case null:
                default:
                    break;
            }
        }

        return null;
    }

    private static DomainError FatoDuplicado(string codigo) =>
        new(GrafoFormularioErrorCodes.FatoDuplicado, $"O código '{codigo}' aparece mais de uma vez no formulário, entre fatos e grupos.");

    private static DomainError OrdemDuplicada(int ordem) =>
        new(GrafoFormularioErrorCodes.OrdemDuplicada, $"A ordem {ordem} é usada por mais de um item ou grupo — a ordem de coleta precisa ser total.");

    /// <summary>A posição da etapa na ordem de coleta: a do primeiro item ou grupo dela ou das etapas seguintes.</summary>
    public static long PosicaoDaEtapa(
        EtapaDoGrafo etapa, IEnumerable<EtapaDoGrafo> etapas, IEnumerable<ItemDoGrafo> itens, IEnumerable<GrupoDoGrafo>? grupos = null)
    {
        ArgumentNullException.ThrowIfNull(etapa);
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(itens);

        HashSet<string> daquiEmDiante = new(etapas.Where(e => e.Ordem >= etapa.Ordem).Select(static e => e.Codigo), StringComparer.Ordinal);
        return itens.Select(static i => (i.EtapaCodigo, i.Ordem))
            .Concat((grupos ?? []).Select(static g => (g.EtapaCodigo, g.Ordem)))
            .Where(p => p.EtapaCodigo is { } codigo && daquiEmDiante.Contains(codigo))
            .Select(static p => (long)p.Ordem)
            .DefaultIfEmpty(DependenciasDoFormulario.PosicaoDosTermos)
            .Min();
    }
}

/// <summary>Códigos de recusa do grafo do formulário, no processo e no modelo.</summary>
public static class GrafoFormularioErrorCodes
{
    public const string FatoDuplicado = "GrafoFormulario.FatoDuplicado";
    public const string OrdemDuplicada = "GrafoFormulario.OrdemDuplicada";
    public const string GrafoComCiclo = "GrafoFormulario.GrafoComCiclo";
    public const string CitaFatoNaoConhecido = "GrafoFormulario.CitaFatoNaoConhecido";
    public const string CitaFatoPosterior = "GrafoFormulario.CitaFatoPosterior";
    public const string CitaAtributoDoCandidato = "GrafoFormulario.CitaAtributoDoCandidato";
    public const string CitaFatoDeMembroForaDoGrupo = "GrafoFormulario.CitaFatoDeMembroForaDoGrupo";
}
