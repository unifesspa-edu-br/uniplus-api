namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que falta nas bancas declaradas: a banca que julga em 1ª instância cada item recorrido, o recurso que admite instância
/// superior sobre item que se encerra na 1ª, o mínimo de bancas do tipo que exige duas ou mais, a regra de deliberação de cada
/// banca e o mínimo de integrantes que o quórum e os corretores das etapas julgadas pedem. As causas saem na posição da fase
/// ou da etapa que declara a banca, as das fases antes das das etapas.
/// </summary>
public static class ConferenciaDasBancas
{
    private static readonly ItemDaBanca[] ItensDaBanca =
    [
        new("cronograma_banca_de_recursos_sem_candidatos_atendidos",
            "Cronograma: a banca que julga recurso não define candidatos atendidos",
            static (cronograma, _, _) => cronograma.RecusasDasDeclaracoes(ExigenciaDeBancaDeRecurso.CodigosDosCandidatosAtendidos),
            static (_, _) => false),
        new("cronograma_recurso_com_banca_que_julga",
            "Cronograma: todo recurso tem a banca que o julga em 1ª instância, na fase ou na etapa",
            static (cronograma, etapas, _) => QuemJulgaORecurso.SemBanca(cronograma, etapas),
            static (cronograma, etapas) => QuemJulgaIndeterminado(cronograma, etapas)),
        new("cronograma_instancia_superior_admitida_pelo_tipo_de_banca",
            "Cronograma: o recurso que admite instância superior não recai sobre item avaliado por tipo de banca que o encerra na 1ª instância",
            static (cronograma, etapas, _) => QuemJulgaORecurso.InstanciaSuperiorNaoAdmitida(cronograma, etapas),
            static (cronograma, etapas) => QuemJulgaIndeterminado(cronograma, etapas)),
        new("cronograma_tipo_de_banca_com_o_minimo_de_bancas",
            "Cronograma: o tipo de banca que exige duas ou mais bancas tem ao menos duas, na fase ou na etapa e em cada grupo de candidatos atendidos",
            static (cronograma, etapas, _) => ConferirMinimoDeBancasPorTipo(cronograma, etapas),
            static (cronograma, etapas) => MinimoDeBancasIndeterminado(cronograma, etapas)),
        new("cronograma_banca_com_regra_de_deliberacao",
            "Cronograma: toda banca declara a regra de deliberação e, se julga recurso por ato individual, a votação do recurso",
            static (cronograma, etapas, _) => ConferirRegrasDeDeliberacao(cronograma, etapas),
            static (cronograma, etapas) => DeliberacaoIndeterminada(cronograma, etapas)),
        new("cronograma_banca_com_o_minimo_de_integrantes",
            "Cronograma: toda banca tem ao menos o mínimo de integrantes que o quórum e os corretores das etapas julgadas exigem",
            ConferirMinimoDeIntegrantes,
            static (cronograma, etapas) => EtapaJulgadaIndeterminada(cronograma, etapas)),
    ];

    /// <summary>
    /// O tipo que exige duas ou mais bancas e tem só uma no lugar, e, na fase, só uma no grupo de candidatos atendidos. As das
    /// fases saem antes das das etapas, cada uma na posição da banca que ficou sozinha.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirMinimoDeBancasPorTipo(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return [.. cronograma.Fases.SelectMany(FaltasDaFase), .. etapas.Etapas.SelectMany(FaltasDaEtapa)];
    }

    public static IReadOnlyList<FieldError> ConferirRegrasDeDeliberacao(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        IReadOnlySet<BancaDeclarada> avaliadorasQueJulgam = QuemJulgaORecurso.AvaliadorasQueJulgam(QuemJulgaORecurso.Itens(cronograma, etapas));

        return [.. DeliberacaoDasBancasDasFases(cronograma, avaliadorasQueJulgam), .. DeliberacaoDasBancasDasEtapas(etapas, avaliadorasQueJulgam)];
    }

    /// <summary>
    /// Primeiro as bancas comprovadamente abaixo do mínimo, de todas as bancas, e depois as etapas cuja regra de consolidação o
    /// catálogo não confirmou: o mínimo se confere sobre as etapas cuja regra foi lida, e a recusa por regra não resolvida não
    /// esconde uma banca que já se sabe insuficiente. A banca sem regra de deliberação não entra: a recusa dela é outra.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirMinimoDeIntegrantes(
        CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, CorretoresDasEtapas corretores)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(corretores);

        IReadOnlyList<QuemJulgaORecurso.ItemJulgado> itens = QuemJulgaORecurso.Itens(cronograma, etapas);
        BancaJulgadora[] bancas = [.. BancasDasFases(cronograma, itens), .. BancasDasEtapas(etapas, itens)];

        return [.. bancas.Select(banca => AbaixoDoMinimo(banca, corretores)).OfType<FieldError>(), .. RegrasNaoResolvidas(bancas, corretores)];
    }

    public static IReadOnlyList<ItemConformidade> Itens(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, CorretoresDasEtapas corretores) =>
        [.. ItensDaBanca.Select(item => new ItemConformidade(
            item.Codigo, DimensaoConformidade.Cronograma, item.Mensagem, item.Causas(cronograma, etapas, corretores).Count == 0 && !item.SemJulgar(cronograma, etapas)))];

    internal static DomainError RegraDeDeliberacaoAusente(string citacaoDaBanca, string onde) =>
        new(BancaErrorCodes.RegraDeDeliberacaoObrigatoria, $"A banca '{citacaoDaBanca}' da {onde} não declara a regra de deliberação.");

    internal static DomainError VotacaoDoRecursoAusente(BancaDeclarada banca, string onde, string comoJulga) =>
        new(
            BancaErrorCodes.RegraDeDeliberacaoObrigatoria,
            $"A banca '{banca.Citacao}' da {onde} decide por ato individual e julga recurso {comoJulga}, mas não declara a votação do recurso. Declare o quórum, o critério e o encerramento usados no julgamento do recurso.");

    internal static string PeloPapel(BancaDeclarada banca) => $"com o papel '{banca.Tipo.Papel.Rotulo()}'";

    internal const string PorTerAvaliado = "por ter avaliado o item recorrido, sem banca dedicada no lugar";

    // Quem julga o recurso sai da lista das bancas da fase e da etapa, da lista dos recursos e dos procedimentos e dos tipos de
    // decisão: com parte recusada em alguma delas, a banca que julga pode ser outra, e o item fica pendente em vez de aprovado
    // por omissão.
    private static bool QuemJulgaIndeterminado(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        FaseOuEtapaRecusadaNaLeitura(cronograma, etapas)
        || ListaDasBancasDasFasesComParteRecusada(cronograma)
        || EtapaJulgadaIndeterminada(cronograma, etapas)
        || QuemJulgaORecurso.Itens(cronograma, etapas).Any(static item => item.Indeterminado);

    // A banca que a leitura recusou não chega às listas declaradas, e a fase ou a etapa recusada inteira leva as bancas dela: a
    // regra de deliberação delas não se sabe, e o item fica pendente em vez de aprovado por omissão. As bancas aceitas seguem
    // conferidas.
    private static bool BancasIndeterminadas(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        FaseOuEtapaRecusadaNaLeitura(cronograma, etapas)
        || ListaDasBancasDasFasesComParteRecusada(cronograma)
        || etapas.Etapas.Any(static etapa => etapa.ParteRecusadaEm(EtapaDeclarada.ListaDasBancas));

    // A avaliadora cujo recurso se julga sem saber quem julga não é cobrada da votação do recurso: o item fica pendente.
    private static bool DeliberacaoIndeterminada(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        BancasIndeterminadas(cronograma, etapas)
        || QuemJulgaORecurso.AvaliadoraSemSaberSeJulga(QuemJulgaORecurso.Itens(cronograma, etapas));

    // Com a lista das bancas parcialmente recusada, ou com os candidatos atendidos de algum tipo recusados, não se sabe quantas
    // bancas o lugar ou o grupo tem de verdade: o item fica pendente em vez de aprovado por omissão.
    private static bool MinimoDeBancasIndeterminado(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        BancasIndeterminadas(cronograma, etapas)
        || cronograma.RecusasDasDeclaracoes(CandidatosAtendidosPelasBancas.Codigos).Count > 0;

    private static IEnumerable<FieldError> FaltasDaFase(FaseDeclarada fase, int posicao)
    {
        if (fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas))
        {
            return [];
        }

        return MinimoDeBancasPorTipo.Faltas(fase.Bancas.Declaradas, fase.Bancas.Grupos).Select(falta => CronogramaDoProcesso.NaFase(
            posicao, $"{FaseDeclarada.ListaDasBancasRequeridas}[{falta.Banca.Posicao}]", MinimoDeBancasPorTipo.Recusa(falta, $"fase '{fase.Codigo}'")));
    }

    private static IEnumerable<FieldError> FaltasDaEtapa(EtapaDeclarada etapa, int posicao)
    {
        if (etapa.ParteRecusadaEm(EtapaDeclarada.ListaDasBancas))
        {
            return [];
        }

        return MinimoDeBancasPorTipo.Faltas(etapa.Bancas.Declaradas, []).Select(falta => EtapasDoProcesso.NaEtapa(
            posicao, $"{EtapaDeclarada.ListaDasBancas}[{falta.Banca.Posicao}]", MinimoDeBancasPorTipo.Recusa(falta, EtapaNaPosicao(posicao))));
    }

    private static bool FaseOuEtapaRecusadaNaLeitura(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        cronograma.TemParteRecusadaNaListaDeFases || etapas.LeituraRecusouEtapa;

    private static bool ListaDasBancasDasFasesComParteRecusada(CronogramaDoProcesso cronograma) =>
        cronograma.Fases.Any(static fase => fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas));

    // O mínimo só fica indeterminado onde há etapa cuja julgadora não se sabe: parte recusada em fase sem etapa não o afeta.
    private static bool EtapaJulgadaIndeterminada(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        etapas.Etapas.Any(etapa => JulgadorasIndeterminadas.DoRecursoDaEtapa(cronograma, etapa));

    private static IEnumerable<FieldError> DeliberacaoDasBancasDasFases(CronogramaDoProcesso cronograma, IReadOnlySet<BancaDeclarada> avaliadorasQueJulgam) =>
        cronograma.Fases.SelectMany((fase, posicao) => fase.Bancas.Declaradas
            .Select(banca => (Banca: banca, Causa: CausaDaDeliberacao(banca, $"fase '{fase.Codigo}'", avaliadorasQueJulgam)))
            .Where(static par => par.Causa is not null)
            .Select(par => CronogramaDoProcesso.NaFase(posicao, $"{FaseDeclarada.ListaDasBancasRequeridas}[{par.Banca.Posicao}]", par.Causa!)));

    private static IEnumerable<FieldError> DeliberacaoDasBancasDasEtapas(EtapasDoProcesso etapas, IReadOnlySet<BancaDeclarada> avaliadorasQueJulgam) =>
        etapas.Etapas.SelectMany((etapa, posicao) => etapa.Bancas.Declaradas
            .Select(banca => (Banca: banca, Causa: CausaDaDeliberacao(banca, EtapaNaPosicao(posicao), avaliadorasQueJulgam)))
            .Where(static par => par.Causa is not null)
            .Select(par => EtapasDoProcesso.NaEtapa(posicao, $"{EtapaDeclarada.ListaDasBancas}[{par.Banca.Posicao}]", par.Causa!)));

    // A regra ausente vem antes: sem regra não há modo de deliberação a conferir. A votação do recurso é exigida da banca com papel
    // que julga recurso e da que o julga por ter avaliado o item, sem banca dedicada no lugar.
    private static DomainError? CausaDaDeliberacao(BancaDeclarada banca, string onde, IReadOnlySet<BancaDeclarada> avaliadorasQueJulgam)
    {
        if (banca.Regra is null)
        {
            return RegraDeDeliberacaoAusente(banca.Citacao, onde);
        }

        if (!banca.SemVotacaoDoRecurso)
        {
            return null;
        }

        return banca.Tipo.JulgaRecurso ? VotacaoDoRecursoAusente(banca, onde, PeloPapel(banca))
            : avaliadorasQueJulgam.Contains(banca) ? VotacaoDoRecursoAusente(banca, onde, PorTerAvaliado)
            : null;
    }

    private static string EtapaNaPosicao(int posicao) => $"etapa na posição {posicao + 1}";

    private static IEnumerable<BancaJulgadora> BancasDasFases(CronogramaDoProcesso cronograma, IReadOnlyList<QuemJulgaORecurso.ItemJulgado> itens) =>
        cronograma.Fases.SelectMany((fase, posicao) => fase.Bancas.Declaradas
            .Where(static banca => banca.Regra is not null)
            .Select(banca => new BancaJulgadora(
                erro => CronogramaDoProcesso.NaFase(posicao, $"{FaseDeclarada.ListaDasBancasRequeridas}[{banca.Posicao}]", erro),
                banca.Citacao,
                $"fase '{fase.Codigo}'",
                banca.Regra!,
                banca.Tipo.JulgaRecurso ? QuemJulgaORecurso.EtapasJulgadasPelaFase(itens, fase) : [])));

    // A banca da etapa com papel que julga recurso julga a etapa sempre; a que avaliou o item a julga quando se sabe que o recurso
    // da própria etapa recai sobre ela, sem banca dedicada no lugar. O recurso da fase não acrescenta etapa julgada: ela segue
    // conferida pelo quórum.
    private static IEnumerable<BancaJulgadora> BancasDasEtapas(EtapasDoProcesso etapas, IReadOnlyList<QuemJulgaORecurso.ItemJulgado> itens)
    {
        IReadOnlySet<BancaDeclarada> avaliadorasDaEtapa = QuemJulgaORecurso.AvaliadorasQueJulgam(itens.Where(static item => item.Recurso.Etapa is not null));

        return etapas.Etapas.SelectMany((etapa, posicao) => etapa.Bancas.Declaradas
            .Where(static banca => banca.Regra is not null)
            .Select(banca => new BancaJulgadora(
                erro => EtapasDoProcesso.NaEtapa(posicao, $"{EtapaDeclarada.ListaDasBancas}[{banca.Posicao}]", erro),
                banca.Citacao,
                EtapaNaPosicao(posicao),
                banca.Regra!,
                banca.Tipo.JulgaRecurso || avaliadorasDaEtapa.Contains(banca) ? [new EtapaJulgada(etapa, posicao)] : [])));
    }

    private static IEnumerable<FieldError> RegrasNaoResolvidas(BancaJulgadora[] bancas, CorretoresDasEtapas corretores) =>
        bancas.SelectMany(banca => banca.Julgadas
            .Where(julgada => !corretores.RegraResolvida(julgada.Etapa.RegraConsolidacao))
            .Select(julgada => banca.Localiza(MinimoDeIntegrantes.NaoResolvida(banca.Citacao, banca.Onde, julgada.Posicao))));

    private static FieldError? AbaixoDoMinimo(BancaJulgadora banca, CorretoresDasEtapas corretores)
    {
        int minimo = MinimoDeIntegrantes.Exigido(
            banca.Regra,
            banca.Julgadas.Where(julgada => corretores.RegraResolvida(julgada.Etapa.RegraConsolidacao)).Select(julgada => corretores.EmCampoDaRegra(julgada.Etapa.RegraConsolidacao)));

        return banca.Regra.Integrantes >= minimo
            ? null
            : banca.Localiza(MinimoDeIntegrantes.AbaixoDoMinimo(banca.Citacao, banca.Onde, banca.Regra.Integrantes, minimo));
    }

    /// <summary>Um item do painel das bancas e as causas que ele projeta: vermelho quando houver alguma.</summary>
    private sealed record ItemDaBanca(
        string Codigo,
        string Mensagem,
        Func<CronogramaDoProcesso, EtapasDoProcesso, CorretoresDasEtapas, IReadOnlyList<FieldError>> Causas,
        Func<CronogramaDoProcesso, EtapasDoProcesso, bool> SemJulgar);

    /// <summary>Uma banca declarada com regra de deliberação, a citação dela, onde ela está e as etapas que julga.</summary>
    private sealed record BancaJulgadora(
        Func<DomainError, FieldError> Localiza, string Citacao, string Onde, RegraDeDeliberacao Regra, IReadOnlyList<EtapaJulgada> Julgadas);
}
