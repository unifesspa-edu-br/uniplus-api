namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// As regras de um formulário na forma que atravessa o fio: o que o avaliador usa para decidir o que
/// aparece, o que é obrigatório, quais opções valem, o que a resposta viola e o que impede a inscrição
/// — etapas, itens, grupos repetíveis, termos, derivações e agregados —, mais a oferta de valores de
/// cada campo. É a forma que a API publica e que o interpretador do front lê (ADR-0139).
/// </summary>
/// <remarks>
/// <para>
/// A conversão vai e volta sem perda: <see cref="De"/> leva uma <see cref="DefinicaoFormulario"/> ao
/// portável, e <see cref="ParaDefinicao"/> traz o portável de volta. Para as mesmas respostas, as duas
/// definições dão a mesma avaliação; é isso que prova que nada que o avaliador usa fica fora do fio.
/// </para>
/// <para>
/// Os predicados seguem a forma da pré-condição: a lista externa é o OU de cláusulas, e cada cláusula é
/// o E de condições. Nulo é ausência de condição; a lista vazia é o predicado sem cláusula, que avalia
/// falso — e, na derivação, a regra-âncora, sempre ativa.
/// </para>
/// </remarks>
public sealed record FormularioPortavel(
    IReadOnlyList<EtapaPortavel> Etapas,
    IReadOnlyList<TermoPortavel> Termos,
    IReadOnlyList<DerivacaoPortavel> Derivacoes,
    IReadOnlyList<AgregadoPortavel> Agregados)
{
    /// <summary>
    /// O formulário portável de uma definição. A oferta de valores não está na definição: quem monta a
    /// definição a conhece e a passa aqui, por fato.
    /// </summary>
    public static FormularioPortavel De(
        DefinicaoFormulario definicao, IReadOnlyDictionary<string, IReadOnlySet<string>>? ofertas = null)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        IReadOnlyDictionary<string, IReadOnlySet<string>> ofertados = ofertas ?? new Dictionary<string, IReadOnlySet<string>>();

        return new(
            [.. definicao.Etapas.Select(e => new EtapaPortavel(
                e.Codigo,
                ParaFio(e.Exibicao),
                [.. e.Itens.Select(i => Item(i, ofertados))],
                [.. e.Grupos.Select(g => new GrupoPortavel(
                    g.Codigo,
                    ParaFio(g.Exibicao),
                    EntradaDeRegras.ParaEntrada(g.Obrigatoriedade),
                    ParaFio(g.Obrigatoriedade.Predicado),
                    g.Minimo,
                    g.Maximo,
                    g.IncluiCandidato,
                    [.. g.Subitens.Select(s => Item(s, ofertados))]))]))],
            [.. definicao.Termos.Select(static t => new TermoPortavel(
                t.Codigo,
                ParaFio(t.Exibicao),
                EntradaDeRegras.ParaEntrada(t.Obrigatoriedade),
                ParaFio(t.Obrigatoriedade.Predicado)))],
            [.. definicao.Derivacoes.Select(static d => new DerivacaoPortavel(
                d.CodigoFato,
                d.Booleano,
                [.. d.Regras.Select(static r => new RegraDerivacaoPortavel(ParaFio(r.Quando)!, r.Contribui))]))],
            [.. definicao.Agregados.Select(static a => new AgregadoPortavel(
                a.Codigo,
                a.GrupoCodigo,
                a.FatoDeMembro,
                AgregadoDeGrupo.ParaToken(a.Operacao)))]);
    }

    /// <summary>
    /// A definição avaliável do portável. A forma que não forma definição — obrigatoriedade sem o
    /// predicado que ela pede, derivação sem regra, código repetido — é recusada com a causa, para
    /// que o conteúdo vindo de fora (um arquivo importado) nunca chegue ao avaliador pela metade.
    /// </summary>
    public Result<DefinicaoFormulario> ParaDefinicao()
    {
        if (CaminhoDoPrimeiroNulo() is { } nulo)
        {
            return Result<DefinicaoFormulario>.Failure(new DomainError(
                FormularioPortavelErrorCodes.EstruturaInvalida, $"O elemento '{nulo}' das regras é nulo."));
        }

        // As invariantes da definição são protegidas por exceção, porque a definição montada pelo
        // módulo nunca as viola; o portável vem de fora e é recusado com a causa.
        try
        {
            return Converter();
        }
        catch (ArgumentException excecao)
        {
            return Result<DefinicaoFormulario>.Failure(new DomainError(FormularioPortavelErrorCodes.EstruturaInvalida, excecao.Message));
        }
    }

    private Result<DefinicaoFormulario> Converter()
    {
        List<DefinicaoEtapa> etapas = [];
        foreach (EtapaPortavel etapa in Etapas ?? [])
        {
            Result<DefinicaoEtapa> lida = Etapa(etapa);
            if (lida.IsFailure)
            {
                return Result<DefinicaoFormulario>.Failure(lida.Error!);
            }

            etapas.Add(lida.Value!);
        }

        List<DefinicaoTermo> termos = [];
        foreach (TermoPortavel termo in Termos ?? [])
        {
            Result<PredicadoDnf?> exibicao = DoFio(termo.Exibicao);
            Result<Obrigatoriedade> obrigatoriedade = Obrigatoriedade(termo.Obrigatoriedade, termo.PredicadoObrigatoriedade, $"termo '{termo.Codigo}'");
            if (exibicao.IsFailure || obrigatoriedade.IsFailure)
            {
                return Result<DefinicaoFormulario>.Failure((exibicao.Error ?? obrigatoriedade.Error)!);
            }

            termos.Add(new DefinicaoTermo(termo.Codigo, exibicao.Value, obrigatoriedade.Value!));
        }

        List<RegrasDerivacaoFato> derivacoes = [];
        foreach (DerivacaoPortavel derivacao in Derivacoes ?? [])
        {
            Result<RegrasDerivacaoFato> lida = Derivacao(derivacao);
            if (lida.IsFailure)
            {
                return Result<DefinicaoFormulario>.Failure(lida.Error!);
            }

            derivacoes.Add(lida.Value!);
        }

        List<DefinicaoAgregado> agregados = [];
        foreach (AgregadoPortavel agregado in Agregados ?? [])
        {
            OperacaoAgregado operacao = AgregadoDeGrupo.DoToken(agregado.Operacao);
            if (operacao == OperacaoAgregado.Nenhuma)
            {
                return Result<DefinicaoFormulario>.Failure(new DomainError(
                    FormularioPortavelErrorCodes.OperacaoDeAgregadoInvalida,
                    $"A operação do agregado '{agregado.Codigo}' é EXISTE ou VALORES_PRESENTES."));
            }

            agregados.Add(new DefinicaoAgregado(agregado.Codigo, agregado.GrupoCodigo, agregado.FatoDeMembro, operacao));
        }

        return Result<DefinicaoFormulario>.Success(new DefinicaoFormulario(etapas, termos, derivacoes, agregados));
    }

    /// <summary>
    /// O caminho do primeiro elemento nulo dentro das regras, como <c>etapas[0].itens[2]</c>; nulo
    /// quando não há. O arquivo importado chega pelo fio, e a lista com elemento nulo não forma
    /// definição.
    /// </summary>
    private string? CaminhoDoPrimeiroNulo() =>
        Lista(Etapas, "etapas", static (etapa, caminho) =>
            Predicado(etapa.Exibicao, $"{caminho}.exibicao")
            ?? Lista(etapa.Itens, $"{caminho}.itens", NuloNoItem)
            ?? Lista(etapa.Grupos, $"{caminho}.grupos", static (grupo, doGrupo) =>
                Predicado(grupo.Exibicao, $"{doGrupo}.exibicao")
                ?? Predicado(grupo.PredicadoObrigatoriedade, $"{doGrupo}.predicadoObrigatoriedade")
                ?? Lista(grupo.Subitens, $"{doGrupo}.subitens", NuloNoItem)))
        ?? Lista(Termos, "termos", static (termo, caminho) =>
            Predicado(termo.Exibicao, $"{caminho}.exibicao") ?? Predicado(termo.PredicadoObrigatoriedade, $"{caminho}.predicadoObrigatoriedade"))
        ?? Lista(Derivacoes, "derivacoes", static (derivacao, caminho) =>
            Lista(derivacao.Regras, $"{caminho}.regras", static (regra, daRegra) => Predicado(regra.Quando, $"{daRegra}.quando")))
        ?? Lista(Agregados, "agregados", static (_, _) => null);

    private static string? NuloNoItem(ItemPortavel item, string caminho) =>
        Predicado(item.Exibicao, $"{caminho}.exibicao")
        ?? Predicado(item.PredicadoObrigatoriedade, $"{caminho}.predicadoObrigatoriedade")
        ?? Lista(item.Restricoes, $"{caminho}.restricoes", static (restricao, daRestricao) =>
            Lista(restricao.Entradas, $"{daRestricao}.entradas", static (entrada, daEntrada) =>
                Predicado(entrada.Quando, $"{daEntrada}.quando") ?? Lista(entrada.Valores, $"{daEntrada}.valores", static (_, _) => null))
            ?? Lista(restricao.Fatos, $"{daRestricao}.fatos", static (_, _) => null))
        ?? (item.Impedimento is { } impedimento ? Predicado(impedimento.Quando, $"{caminho}.impedimento.quando") : null)
        ?? Lista(item.Oferta, $"{caminho}.oferta", static (_, _) => null);

    private static string? Predicado(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicado, string caminho) =>
        Lista(predicado, caminho, static (clausula, daClausula) => Lista(clausula, daClausula, static (_, _) => null));

    private static string? Lista<T>(IReadOnlyList<T>? lista, string caminho, Func<T, string, string?> dentro)
        where T : class
    {
        for (int i = 0; i < (lista?.Count ?? 0); i++)
        {
            string doElemento = $"{caminho}[{i}]";
            if (lista![i] is not { } elemento)
            {
                return doElemento;
            }

            if (dentro(elemento, doElemento) is { } nulo)
            {
                return nulo;
            }
        }

        return null;
    }

    /// <summary>
    /// A oferta de valores de cada campo que tem oferta, por fato — a dos itens e a dos subitens. Um fato
    /// repetido não forma definição (<see cref="ParaDefinicao"/> o recusa); aqui fica a primeira oferta.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Ofertas()
    {
        Dictionary<string, IReadOnlySet<string>> ofertas = new(StringComparer.Ordinal);
        foreach (ItemPortavel item in (Etapas ?? [])
            .SelectMany(static e => (e.Itens ?? []).Concat((e.Grupos ?? []).SelectMany(static g => g.Subitens ?? [])))
            .Where(static i => i.Oferta is not null))
        {
            ofertas.TryAdd(item.FatoCodigo, new HashSet<string>(item.Oferta!, StringComparer.Ordinal));
        }

        return ofertas;
    }

    /// <summary>
    /// A avaliação do portável: a resposta fora da oferta é descartada antes, como na pré-visualização e
    /// na execução, e o resto é o mesmo avaliador.
    /// </summary>
    public Result<AvaliacaoFormulario> Avaliar(EntradaAvaliacaoFormulario entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        Result<DefinicaoFormulario> definicao = ParaDefinicao();
        if (definicao.IsFailure)
        {
            return Result<AvaliacaoFormulario>.Failure(definicao.Error!);
        }

        IReadOnlyDictionary<string, IReadOnlySet<string>> ofertas = Ofertas();
        EntradaAvaliacaoFormulario dentroDaOferta = entrada with
        {
            Respostas = RespostaDeCampo.DentroDaOferta(entrada.Respostas, ofertas),
            RespostasDosGrupos = entrada.RespostasDosGrupos?.ToDictionary(
                static g => g.Key,
                g => (IReadOnlyList<OcorrenciaRespondida>)[.. g.Value.Select(o => o with { Respostas = RespostaDeCampo.DentroDaOferta(o.Respostas, ofertas) })],
                StringComparer.Ordinal),
        };

        return Result<AvaliacaoFormulario>.Success(AvaliadorFormulario.Avaliar(definicao.Value!, dentroDaOferta));
    }

    private static ItemPortavel Item(DefinicaoItem item, IReadOnlyDictionary<string, IReadOnlySet<string>> ofertados) =>
        new(
            item.FatoCodigo,
            ParaFio(item.Exibicao),
            EntradaDeRegras.ParaEntrada(item.Obrigatoriedade),
            ParaFio(item.Obrigatoriedade.Predicado),
            [.. item.Restricoes.Select(EntradaDeRegras.ParaEntrada)],
            EntradaDeRegras.ParaEntrada(item.Impedimento),
            ofertados.TryGetValue(item.FatoCodigo, out IReadOnlySet<string>? oferta) ? [.. oferta.Order(StringComparer.Ordinal)] : null,
            item.Formato);

    private static Result<DefinicaoEtapa> Etapa(EtapaPortavel etapa)
    {
        Result<PredicadoDnf?> exibicao = DoFio(etapa.Exibicao);
        if (exibicao.IsFailure)
        {
            return Result<DefinicaoEtapa>.Failure(exibicao.Error!);
        }

        List<DefinicaoItem> itens = [];
        foreach (ItemPortavel item in etapa.Itens ?? [])
        {
            Result<DefinicaoItem> lido = Item(item);
            if (lido.IsFailure)
            {
                return Result<DefinicaoEtapa>.Failure(lido.Error!);
            }

            itens.Add(lido.Value!);
        }

        List<DefinicaoGrupo> grupos = [];
        foreach (GrupoPortavel grupo in etapa.Grupos ?? [])
        {
            Result<DefinicaoGrupo> lido = Grupo(grupo);
            if (lido.IsFailure)
            {
                return Result<DefinicaoEtapa>.Failure(lido.Error!);
            }

            grupos.Add(lido.Value!);
        }

        return Construir(() => new DefinicaoEtapa(etapa.Codigo, exibicao.Value, itens, grupos));
    }

    private static Result<DefinicaoGrupo> Grupo(GrupoPortavel grupo)
    {
        Result<PredicadoDnf?> exibicao = DoFio(grupo.Exibicao);
        Result<Obrigatoriedade> obrigatoriedade = Obrigatoriedade(grupo.Obrigatoriedade, grupo.PredicadoObrigatoriedade, $"grupo '{grupo.Codigo}'");
        if (exibicao.IsFailure || obrigatoriedade.IsFailure)
        {
            return Result<DefinicaoGrupo>.Failure((exibicao.Error ?? obrigatoriedade.Error)!);
        }

        List<DefinicaoItem> subitens = [];
        foreach (ItemPortavel subitem in grupo.Subitens ?? [])
        {
            Result<DefinicaoItem> lido = Item(subitem);
            if (lido.IsFailure)
            {
                return Result<DefinicaoGrupo>.Failure(lido.Error!);
            }

            subitens.Add(lido.Value!);
        }

        return Construir(() => new DefinicaoGrupo(
            grupo.Codigo, exibicao.Value, obrigatoriedade.Value!, grupo.Minimo, grupo.Maximo, subitens, grupo.IncluiCandidato));
    }

    private static Result<DefinicaoItem> Item(ItemPortavel item)
    {
        Result<PredicadoDnf?> exibicao = DoFio(item.Exibicao);
        Result<Obrigatoriedade> obrigatoriedade = Obrigatoriedade(item.Obrigatoriedade, item.PredicadoObrigatoriedade, $"campo '{item.FatoCodigo}'");
        Result<Impedimento?> impedimento = EntradaDeRegras.Impedimento(item.Impedimento);
        if (exibicao.IsFailure || obrigatoriedade.IsFailure || impedimento.IsFailure)
        {
            return Result<DefinicaoItem>.Failure((exibicao.Error ?? obrigatoriedade.Error ?? impedimento.Error)!);
        }

        List<RestricaoValor> restricoes = [];
        foreach (RestricaoValorInput restricao in item.Restricoes ?? [])
        {
            Result<RestricaoValor> lida = EntradaDeRegras.Restricao(restricao);
            if (lida.IsFailure)
            {
                return Result<DefinicaoItem>.Failure(lida.Error!);
            }

            restricoes.Add(lida.Value!);
        }

        return Construir(() => new DefinicaoItem(item.FatoCodigo, exibicao.Value, obrigatoriedade.Value!, restricoes, impedimento.Value, item.Formato));
    }

    private static Result<RegrasDerivacaoFato> Derivacao(DerivacaoPortavel derivacao)
    {
        List<RegraDerivacao> regras = [];
        foreach (RegraDerivacaoPortavel regra in derivacao.Regras ?? [])
        {
            if (regra.Quando is null)
            {
                return Result<RegrasDerivacaoFato>.Failure(new DomainError(
                    FormularioPortavelErrorCodes.DerivacaoInvalida,
                    $"Uma regra de '{derivacao.FatoCodigo}' não diz quando ativa; a regra sempre ativa tem a lista vazia."));
            }

            Result<PredicadoDnf?> quando = DoFio(regra.Quando);
            if (quando.IsFailure)
            {
                return Result<RegrasDerivacaoFato>.Failure(quando.Error!);
            }

            if (derivacao.Booleano)
            {
                regras.Add(RegraDerivacao.CriarBooleana(quando.Value!));
                continue;
            }

            Result<RegraDerivacao> criada = RegraDerivacao.Criar(quando.Value!, regra.Contribui ?? string.Empty);
            if (criada.IsFailure)
            {
                return Result<RegrasDerivacaoFato>.Failure(criada.Error!);
            }

            regras.Add(criada.Value!);
        }

        // As dependências são as que as regras citam, e o domínio do categórico é o que elas
        // contribuem: o avaliador não usa mais que isso do derivado.
        string[] dependencias = [.. regras.SelectMany(static r => r.FatosCitados).Distinct(StringComparer.Ordinal)];
        Result<RegrasDerivacaoFato> lida = derivacao.Booleano
            ? RegrasDerivacaoFato.CriarBooleana(derivacao.FatoCodigo, regras, dependencias)
            : RegrasDerivacaoFato.Criar(
                derivacao.FatoCodigo, regras, dependencias, [.. regras.Select(static r => r.Contribui!).Distinct(StringComparer.Ordinal)]);
        return lida.IsSuccess
            ? lida
            : Result<RegrasDerivacaoFato>.Failure(new DomainError(FormularioPortavelErrorCodes.DerivacaoInvalida, lida.Error!.Message));
    }

    private static Result<Obrigatoriedade> Obrigatoriedade(
        string? token, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicado, string dono)
    {
        Result<PredicadoDnf?> quando = DoFio(predicado);
        if (quando.IsFailure)
        {
            return Result<Obrigatoriedade>.Failure(quando.Error!);
        }

        return EntradaDeRegras.Obrigatoriedade(token, quando.Value) is { } obrigatoriedade
            ? Result<Obrigatoriedade>.Success(obrigatoriedade)
            : Result<Obrigatoriedade>.Failure(new DomainError(
                FormularioPortavelErrorCodes.ObrigatoriedadeInvalida,
                $"A obrigatoriedade do {dono} é SEMPRE ou NUNCA sem predicado, ou QUANDO com ele."));
    }

    /// <summary>O predicado na forma do fio: nulo sem condição, a lista vazia sem cláusula.</summary>
    private static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? ParaFio(PredicadoDnf? predicado) =>
        EntradaDeRegras.ParaEntrada(predicado);

    /// <summary>O predicado do fio: nulo é sem condição, e a lista vazia é o predicado sem cláusula.</summary>
    private static Result<PredicadoDnf?> DoFio(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? clausulas)
    {
        if (clausulas is { Count: 0 })
        {
            Result<PredicadoDnf> semClausula = PredicadoDnf.CriarDeCondicoesAgrupadas([]);
            return Result<PredicadoDnf?>.Success(semClausula.Value);
        }

        return EntradaDeRegras.Predicado(clausulas);
    }

    private static Result<T> Construir<T>(Func<T> construir)
    {
        try
        {
            return Result<T>.Success(construir());
        }
        catch (ArgumentException excecao)
        {
            return Result<T>.Failure(new DomainError(FormularioPortavelErrorCodes.EstruturaInvalida, excecao.Message));
        }
    }
}

/// <summary>Uma etapa: o código com que o avaliador a identifica, a exibição da etapa inteira, os itens e os grupos.</summary>
public sealed record EtapaPortavel(
    string Codigo,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao,
    IReadOnlyList<ItemPortavel> Itens,
    IReadOnlyList<GrupoPortavel> Grupos);

/// <summary>
/// Um campo: o fato que ele produz, a exibição, a obrigatoriedade — <c>SEMPRE</c>, <c>NUNCA</c> ou
/// <c>QUANDO</c> com o predicado —, as restrições de valor, o impedimento, a oferta de valores, quando o
/// campo tem uma — a resposta fora dela não vale —, e o formato do campo de texto, como <c>CPF</c> ou
/// <c>EMAIL</c>, que a resposta tem de atender.
/// </summary>
public sealed record ItemPortavel(
    string FatoCodigo,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao,
    string Obrigatoriedade,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? PredicadoObrigatoriedade,
    IReadOnlyList<RestricaoValorInput> Restricoes,
    ImpedimentoInput? Impedimento,
    IReadOnlyList<string>? Oferta,
    string? Formato = null);

/// <summary>Um grupo repetível: exibição, obrigatoriedade, mínimo e máximo de ocorrências, se o candidato é um dos membros e os subitens.</summary>
public sealed record GrupoPortavel(
    string Codigo,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao,
    string Obrigatoriedade,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? PredicadoObrigatoriedade,
    int Minimo,
    int? Maximo,
    bool IncluiCandidato,
    IReadOnlyList<ItemPortavel> Subitens);

/// <summary>Um termo exigido: o código com que o avaliador o identifica, a exibição e a obrigatoriedade.</summary>
public sealed record TermoPortavel(
    string Codigo,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? Exibicao,
    string Obrigatoriedade,
    IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? PredicadoObrigatoriedade);

/// <summary>
/// As regras de um fato derivado: booleano (alguma regra ativa) ou categórico (a união do que as regras
/// ativas contribuem).
/// </summary>
public sealed record DerivacaoPortavel(string FatoCodigo, bool Booleano, IReadOnlyList<RegraDerivacaoPortavel> Regras);

/// <summary>Uma regra de derivação: quando ela ativa — a lista vazia é a âncora, sempre ativa — e o código que contribui no categórico.</summary>
public sealed record RegraDerivacaoPortavel(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> Quando, string? Contribui);

/// <summary>Um fato agregado sobre um grupo: o fato de membro que ele agrega e a operação, <c>EXISTE</c> ou <c>VALORES_PRESENTES</c>.</summary>
public sealed record AgregadoPortavel(string Codigo, string GrupoCodigo, string FatoDeMembro, string Operacao);
