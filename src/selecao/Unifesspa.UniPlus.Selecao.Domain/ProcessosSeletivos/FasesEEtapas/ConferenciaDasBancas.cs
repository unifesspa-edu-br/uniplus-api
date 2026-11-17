namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que falta nas bancas declaradas: a banca de análise de recursos que todo recurso exige, a regra de deliberação de cada
/// banca e o mínimo de integrantes que o quórum e os corretores das etapas julgadas pedem. As causas saem na posição da fase
/// ou da etapa que declara a banca, as das fases antes das das etapas.
/// </summary>
public static class ConferenciaDasBancas
{
    private static readonly ItemDaBanca[] ItensDaBanca =
    [
        new("cronograma_recurso_com_banca_de_analise",
            "Cronograma: todo recurso tem a banca de análise de recursos que o julga, na fase ou na etapa",
            static (cronograma, etapas, _) => ConferirRecursosSemBanca(cronograma, etapas),
            static (cronograma, etapas) => ConjuntoDeEtapasJulgadasIndeterminado(cronograma, etapas)),
        new("cronograma_banca_de_recursos_sem_recorte",
            "Cronograma: a banca de análise de recursos não declara recorte de competência",
            static (cronograma, _, _) => cronograma.RecusasDasDeclaracoes(ExigenciaDeBancaDeRecurso.CodigosDoRecorte),
            static (_, _) => false),
        new("cronograma_banca_com_regra_de_deliberacao",
            "Cronograma: toda banca declarada tem a regra de deliberação",
            static (cronograma, etapas, _) => ConferirRegrasDeDeliberacao(cronograma, etapas),
            static (_, _) => false),
        new("cronograma_banca_com_o_minimo_de_integrantes",
            "Cronograma: toda banca tem ao menos o mínimo de integrantes que o quórum e os corretores das etapas julgadas exigem",
            ConferirMinimoDeIntegrantes,
            static (cronograma, etapas) => EtapaJulgadaIndeterminada(cronograma, etapas)),
    ];

    public static IReadOnlyList<FieldError> ConferirRecursosSemBanca(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return [.. FasesSemBancaDeRecursos(cronograma), .. EtapasSemBancaDeRecursos(cronograma, etapas)];
    }

    public static IReadOnlyList<FieldError> ConferirRegrasDeDeliberacao(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return [.. BancasSemRegraDasFases(cronograma), .. BancasSemRegraDasEtapas(etapas)];
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

    internal static DomainError RegraDeDeliberacaoAusente(string nomeDaBanca, string onde) =>
        new(BancaErrorCodes.RegraDeDeliberacaoObrigatoria, $"A banca '{nomeDaBanca}' {onde} não declara a regra de deliberação.");

    // Quem julga o recurso de cada etapa sai da lista das bancas da fase e da etapa e da lista dos recursos da etapa: com parte
    // recusada em alguma delas, a banca que julga pode ser outra, e o item fica pendente em vez de aprovado por omissão.
    private static bool ConjuntoDeEtapasJulgadasIndeterminado(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        cronograma.Fases.Any(static fase => fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas))
        || EtapaJulgadaIndeterminada(cronograma, etapas);

    // O mínimo só fica indeterminado onde há etapa cuja julgadora não se sabe: parte recusada em fase sem etapa não o afeta.
    private static bool EtapaJulgadaIndeterminada(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        etapas.Etapas.Any(etapa => JulgadoraDaEtapaIndeterminada(cronograma, etapa));

    private static IEnumerable<FieldError> FasesSemBancaDeRecursos(CronogramaDoProcesso cronograma) =>
        cronograma.Fases
            .Select(static (fase, posicao) => (fase, posicao))
            .Where(static par => par.fase.Atos.AdmiteRecurso && !par.fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas) && !par.fase.Bancas.TemBancaDeRecursos)
            .Select(static par => CronogramaDoProcesso.NaFase(par.posicao, "bancasRequeridas", ExigenciaDeBancaDeRecurso.FaseSemBancaDeRecursos(par.fase.Codigo)));

    private static IEnumerable<FieldError> EtapasSemBancaDeRecursos(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        etapas.Etapas
            .Select(static (etapa, posicao) => (etapa, posicao))
            .Where(par => !JulgadoraDaEtapaIndeterminada(cronograma, par.etapa)
                && ExigenciaDeBancaDeRecurso.EtapaSemBancaQueJulgue(par.etapa.Atos.TemRecursos, par.etapa.Bancas.TemBancaDeRecursos, FaseDaEtapaTemBancaDeRecursos(cronograma, par.etapa)))
            .Select(static par => EtapasDoProcesso.NaEtapa(par.posicao, "bancas", ExigenciaDeBancaDeRecurso.EtapaSemBancaDeRecursos(par.etapa.Nome)));

    private static bool JulgadoraDaEtapaIndeterminada(CronogramaDoProcesso cronograma, EtapaDeclarada etapa) =>
        etapa.ParteRecusadaEm(EtapaDeclarada.ListaDasBancas)
        || etapa.ParteRecusadaEm(EtapaDeclarada.ListaDosRecursos)
        || cronograma.Fases.Any(fase => string.Equals(fase.Codigo, etapa.FaseCodigo, StringComparison.Ordinal) && fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas));

    private static bool FaseDaEtapaTemBancaDeRecursos(CronogramaDoProcesso cronograma, EtapaDeclarada etapa) =>
        cronograma.Fases.Any(fase => string.Equals(fase.Codigo, etapa.FaseCodigo, StringComparison.Ordinal) && fase.Bancas.TemBancaDeRecursos);

    private static IEnumerable<FieldError> BancasSemRegraDasFases(CronogramaDoProcesso cronograma) =>
        cronograma.Fases.SelectMany(static (fase, posicao) => fase.Bancas.Declaradas
            .Where(static banca => banca.Regra is null)
            .Select(banca => CronogramaDoProcesso.NaFase(
                posicao, $"bancasRequeridas[{banca.Posicao}]", RegraDeDeliberacaoAusente(banca.Nome, $"da fase '{fase.Codigo}'"))));

    private static IEnumerable<FieldError> BancasSemRegraDasEtapas(EtapasDoProcesso etapas) =>
        etapas.Etapas.SelectMany(static (etapa, posicao) => etapa.Bancas.Declaradas
            .Where(static banca => banca.Regra is null)
            .Select(banca => EtapasDoProcesso.NaEtapa(
                posicao, $"bancas[{banca.Posicao}]", RegraDeDeliberacaoAusente(banca.Nome, $"da etapa '{etapa.Nome}'"))));

    private static IEnumerable<BancaJulgadora> BancasDasFases(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        cronograma.Fases.SelectMany((fase, posicao) => fase.Bancas.Declaradas
            .Where(static banca => banca.Regra is not null)
            .Select(banca => new BancaJulgadora(
                erro => CronogramaDoProcesso.NaFase(posicao, $"bancasRequeridas[{banca.Posicao}]", erro),
                banca.Nome,
                $"da fase '{fase.Codigo}'",
                banca.Regra!,
                banca.DeRecursos ? EtapasRecorridasSemBancaPropria(cronograma, etapas, fase) : [])));

    private static IEnumerable<BancaJulgadora> BancasDasEtapas(EtapasDoProcesso etapas) =>
        etapas.Etapas.SelectMany(static (etapa, posicao) => etapa.Bancas.Declaradas
            .Where(static banca => banca.Regra is not null)
            .Select(banca => new BancaJulgadora(
                erro => EtapasDoProcesso.NaEtapa(posicao, $"bancas[{banca.Posicao}]", erro),
                banca.Nome,
                $"da etapa '{Eco(etapa.Nome)}'",
                banca.Regra!,
                banca.DeRecursos ? [etapa] : [])));

    // A etapa da fase com recurso e sem banca de recursos própria é julgada pela banca de recursos da fase; a etapa de julgadora
    // indeterminada não se atribui a banca nenhuma, e o mínimo se confere sobre as que a banca julga com certeza.
    private static EtapaDeclarada[] EtapasRecorridasSemBancaPropria(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, FaseDeclarada fase) =>
        [.. etapas.Etapas.Where(etapa => string.Equals(etapa.FaseCodigo, fase.Codigo, StringComparison.Ordinal)
            && !JulgadoraDaEtapaIndeterminada(cronograma, etapa)
            && etapa.Atos.TemRecursos
            && !etapa.Bancas.TemBancaDeRecursos)];

    private static IEnumerable<FieldError> RegrasNaoResolvidas(BancaJulgadora[] bancas, CorretoresDasEtapas corretores) =>
        bancas.SelectMany(banca => banca.Julgadas
            .Where(etapa => !corretores.RegraResolvida(etapa.RegraConsolidacao))
            .Select(etapa => banca.Localiza(MinimoDeIntegrantes.NaoResolvida(banca.Nome, banca.Onde, etapa.Nome))));

    private static FieldError? AbaixoDoMinimo(BancaJulgadora banca, CorretoresDasEtapas corretores)
    {
        int minimo = MinimoDeIntegrantes.Exigido(
            banca.Regra,
            banca.Julgadas.Where(etapa => corretores.RegraResolvida(etapa.RegraConsolidacao)).Select(etapa => corretores.EmCampoDaRegra(etapa.RegraConsolidacao)));

        return banca.Regra.Integrantes >= minimo
            ? null
            : banca.Localiza(MinimoDeIntegrantes.AbaixoDoMinimo(banca.Nome, banca.Onde, banca.Regra.Integrantes, minimo));
    }

    private static string Eco(string texto) => CaracteresInvisiveis.ParaEco(texto, TextoCongelado.TamanhoMaximoEcoado);

    /// <summary>Um item do painel das bancas e as causas que ele projeta: vermelho quando houver alguma.</summary>
    private sealed record ItemDaBanca(
        string Codigo,
        string Mensagem,
        Func<CronogramaDoProcesso, EtapasDoProcesso, CorretoresDasEtapas, IReadOnlyList<FieldError>> Causas,
        Func<CronogramaDoProcesso, EtapasDoProcesso, bool> SemJulgar);

    /// <summary>Uma banca declarada com regra de deliberação, onde ela está e as etapas que julga.</summary>
    private sealed record BancaJulgadora(
        Func<DomainError, FieldError> Localiza, string Nome, string Onde, RegraDeDeliberacao Regra, IReadOnlyList<EtapaDeclarada> Julgadas);
}
