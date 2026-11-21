namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que falta nas bancas declaradas: a banca de análise de recursos que todo recurso exige, o mínimo de bancas do tipo que exige
/// duas ou mais, a regra de deliberação de cada banca e o mínimo de integrantes que o quórum e os corretores das etapas julgadas pedem. As causas saem na posição da fase
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
            static (cronograma, etapas, _) => ConferirRecursosSemBanca(cronograma, etapas),
            static (cronograma, etapas) => ConjuntoDeEtapasJulgadasIndeterminado(cronograma, etapas)),
        new("cronograma_tipo_de_banca_com_o_minimo_de_bancas",
            "Cronograma: o tipo de banca que exige duas ou mais bancas tem ao menos duas, na fase ou na etapa e em cada grupo de candidatos atendidos",
            static (cronograma, etapas, _) => ConferirMinimoDeBancasPorTipo(cronograma, etapas),
            static (cronograma, etapas) => MinimoDeBancasIndeterminado(cronograma, etapas)),
        new("cronograma_banca_com_regra_de_deliberacao",
            "Cronograma: toda banca declara a regra de deliberação e, se julga recurso por ato individual, a votação do recurso",
            static (cronograma, etapas, _) => ConferirRegrasDeDeliberacao(cronograma, etapas),
            static (cronograma, etapas) => BancasIndeterminadas(cronograma, etapas)),
        new("cronograma_banca_com_o_minimo_de_integrantes",
            "Cronograma: toda banca tem ao menos o mínimo de integrantes que o quórum e os corretores das etapas julgadas exigem",
            ConferirMinimoDeIntegrantes,
            static (cronograma, etapas) => EtapaJulgadaIndeterminada(cronograma, etapas)),
    ];

    public static IReadOnlyList<FieldError> ConferirRecursosSemBanca(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return QuemJulgaORecurso.SemBanca(cronograma, etapas);
    }

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

        return [.. DeliberacaoDasBancasDasFases(cronograma), .. DeliberacaoDasBancasDasEtapas(etapas)];
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

        BancaJulgadora[] bancas = [.. BancasDasFases(cronograma, etapas), .. BancasDasEtapas(etapas)];

        return [.. bancas.Select(banca => AbaixoDoMinimo(banca, corretores)).OfType<FieldError>(), .. RegrasNaoResolvidas(bancas, corretores)];
    }

    public static IReadOnlyList<ItemConformidade> Itens(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, CorretoresDasEtapas corretores) =>
        [.. ItensDaBanca.Select(item => new ItemConformidade(
            item.Codigo, DimensaoConformidade.Cronograma, item.Mensagem, item.Causas(cronograma, etapas, corretores).Count == 0 && !item.SemJulgar(cronograma, etapas)))];

    internal static DomainError RegraDeDeliberacaoAusente(string citacaoDaBanca, string onde) =>
        new(BancaErrorCodes.RegraDeDeliberacaoObrigatoria, $"A banca '{citacaoDaBanca}' da {onde} não declara a regra de deliberação.");

    internal static DomainError VotacaoDoRecursoAusente(BancaDeclarada banca, string onde) =>
        new(
            BancaErrorCodes.RegraDeDeliberacaoObrigatoria,
            $"A banca '{banca.Citacao}' da {onde} decide por ato individual e julga recurso com o papel '{banca.Tipo.Papel.Rotulo()}', mas não declara a votação do recurso. Declare o quórum, o critério e o encerramento usados no julgamento do recurso.");

    // Quem julga o recurso de cada etapa sai da lista das bancas da fase e da etapa e da lista dos recursos da etapa: com parte
    // recusada em alguma delas, a banca que julga pode ser outra, e o item fica pendente em vez de aprovado por omissão.
    private static bool ConjuntoDeEtapasJulgadasIndeterminado(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        FaseOuEtapaRecusadaNaLeitura(cronograma, etapas)
        || ListaDasBancasDasFasesComParteRecusada(cronograma)
        || EtapaJulgadaIndeterminada(cronograma, etapas);

    // A banca que a leitura recusou não chega às listas declaradas, e a fase ou a etapa recusada inteira leva as bancas dela: a
    // regra de deliberação delas não se sabe, e o item fica pendente em vez de aprovado por omissão. As bancas aceitas seguem
    // conferidas.
    private static bool BancasIndeterminadas(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        FaseOuEtapaRecusadaNaLeitura(cronograma, etapas)
        || ListaDasBancasDasFasesComParteRecusada(cronograma)
        || etapas.Etapas.Any(static etapa => etapa.ParteRecusadaEm(EtapaDeclarada.ListaDasBancas));

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
        etapas.Etapas.Any(etapa => QuemJulgaORecurso.JulgadoraIndeterminada(cronograma, etapa));

    private static IEnumerable<FieldError> DeliberacaoDasBancasDasFases(CronogramaDoProcesso cronograma) =>
        cronograma.Fases.SelectMany(static (fase, posicao) => fase.Bancas.Declaradas
            .Select(banca => (Banca: banca, Causa: CausaDaDeliberacao(banca, $"fase '{fase.Codigo}'")))
            .Where(static par => par.Causa is not null)
            .Select(par => CronogramaDoProcesso.NaFase(posicao, $"{FaseDeclarada.ListaDasBancasRequeridas}[{par.Banca.Posicao}]", par.Causa!)));

    private static IEnumerable<FieldError> DeliberacaoDasBancasDasEtapas(EtapasDoProcesso etapas) =>
        etapas.Etapas.SelectMany(static (etapa, posicao) => etapa.Bancas.Declaradas
            .Select(banca => (Banca: banca, Causa: CausaDaDeliberacao(banca, EtapaNaPosicao(posicao))))
            .Where(static par => par.Causa is not null)
            .Select(par => EtapasDoProcesso.NaEtapa(posicao, $"{EtapaDeclarada.ListaDasBancas}[{par.Banca.Posicao}]", par.Causa!)));

    // A regra ausente vem antes: sem regra não há modo de deliberação a conferir.
    private static DomainError? CausaDaDeliberacao(BancaDeclarada banca, string onde)
    {
        if (banca.Regra is null)
        {
            return RegraDeDeliberacaoAusente(banca.Citacao, onde);
        }

        return banca.JulgaRecursoSemVotacao ? VotacaoDoRecursoAusente(banca, onde) : null;
    }

    private static string EtapaNaPosicao(int posicao) => $"etapa na posição {posicao + 1}";

    private static IEnumerable<BancaJulgadora> BancasDasFases(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        cronograma.Fases.SelectMany((fase, posicao) => fase.Bancas.Declaradas
            .Where(static banca => banca.Regra is not null)
            .Select(banca => new BancaJulgadora(
                erro => CronogramaDoProcesso.NaFase(posicao, $"{FaseDeclarada.ListaDasBancasRequeridas}[{banca.Posicao}]", erro),
                banca.Citacao,
                $"fase '{fase.Codigo}'",
                banca.Regra!,
                banca.Tipo.JulgaRecurso ? EtapasJulgadasPelaFase(cronograma, etapas, fase) : [])));

    private static IEnumerable<BancaJulgadora> BancasDasEtapas(EtapasDoProcesso etapas) =>
        etapas.Etapas.SelectMany(static (etapa, posicao) => etapa.Bancas.Declaradas
            .Where(static banca => banca.Regra is not null)
            .Select(banca => new BancaJulgadora(
                erro => EtapasDoProcesso.NaEtapa(posicao, $"{EtapaDeclarada.ListaDasBancas}[{banca.Posicao}]", erro),
                banca.Citacao,
                EtapaNaPosicao(posicao),
                banca.Regra!,
                banca.Tipo.JulgaRecurso ? [new EtapaJulgada(etapa, posicao)] : [])));

    // A etapa da fase com recurso e sem banca de recursos própria é julgada pela banca de recursos da fase; a etapa de julgadora
    // indeterminada não se atribui a banca nenhuma, e o mínimo se confere sobre as que a banca julga com certeza.
    private static EtapaJulgada[] EtapasJulgadasPelaFase(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, FaseDeclarada fase) =>
        [.. QuemJulgaORecurso.DasEtapas(cronograma, etapas)
            .Where(recurso => recurso.Nivel == QuemJulgaORecurso.Nivel.BancaDeRecursosDaFase
                && string.Equals(recurso.Etapa.FaseCodigo, fase.Codigo, StringComparison.Ordinal))
            .Select(static recurso => new EtapaJulgada(recurso.Etapa, recurso.Posicao))];

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

    /// <summary>Uma etapa que a banca julga e a posição dela na lista das etapas, que as mensagens citam.</summary>
    private sealed record EtapaJulgada(EtapaDeclarada Etapa, int Posicao);

    /// <summary>Uma banca declarada com regra de deliberação, a citação dela, onde ela está e as etapas que julga.</summary>
    private sealed record BancaJulgadora(
        Func<DomainError, FieldError> Localiza, string Citacao, string Onde, RegraDeDeliberacao Regra, IReadOnlyList<EtapaJulgada> Julgadas);
}
