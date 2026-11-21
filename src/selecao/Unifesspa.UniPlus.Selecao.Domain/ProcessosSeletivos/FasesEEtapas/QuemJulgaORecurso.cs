namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quem julga em 1ª instância cada item recorrido: cada procedimento de recurso que a fase ou a etapa declara, ou o recurso sem
/// procedimento. O primeiro nível que tem banca decide e os seguintes não entram: no recurso da etapa, a banca da etapa dedicada
/// à 1ª instância, a da fase em que ela acontece e as bancas que avaliaram o item; no da fase, a da fase dedicada à 1ª instância
/// e as bancas que avaliaram o item. A banca que avaliou é a que a cópia do tipo diz que avalia o mérito e julga o tipo de
/// decisão do item (<see cref="TipoDeBancaCopiado.AvaliaODecisao"/>). Com várias avaliadoras no mesmo grupo, cada uma julga o
/// recurso das outras e, com uma só, ela mesma: em qualquer caso todas as que avaliaram o item o julgam.
/// </summary>
internal static class QuemJulgaORecurso
{
    /// <summary>O campo, dentro do recurso, em que se declara se ele admite instância superior.</summary>
    internal const string CampoDaInstanciaSuperior = "admiteInstanciaSuperior";

    /// <summary>Em que nível se encontrou quem julga o item.</summary>
    internal enum Nivel
    {
        Ninguem,
        BancaDeRecursosDaEtapa,
        BancaDeRecursosDaFase,
        BancasQueAvaliaramOItem,
    }

    /// <summary>As bancas dedicadas à 1ª instância que um nível encontrou.</summary>
    internal sealed record BancasDoNivel(Nivel Nivel, IReadOnlyList<BancaDeclarada> Bancas);

    /// <summary>
    /// Um item recorrido e quem o julga. As avaliadoras do item vêm sempre, julguem ou não: a marca de encerrar o recurso na 1ª
    /// instância é delas. O item indeterminado não tem quem julga afirmado, porque a leitura recusou parte do que o decide.
    /// </summary>
    internal sealed record ItemJulgado(
        IRecursoNoLugar Recurso,
        ProcedimentoDeRecursoDeclarado? Procedimento,
        Nivel Nivel,
        IReadOnlyList<BancaDeclarada> Avaliadoras,
        bool Indeterminado)
    {
        /// <summary>Se o item é julgado pelas bancas que o avaliaram, e não por banca dedicada à 1ª instância.</summary>
        internal bool JulgadoPelasAvaliadoras => !Indeterminado && Nivel == Nivel.BancasQueAvaliaramOItem;

        /// <summary>A banca que avaliou o item e encerra o recurso dele na 1ª instância, se houver.</summary>
        internal BancaDeclarada? AvaliadoraQueEncerra =>
            Avaliadoras.FirstOrDefault(static banca => banca.Tipo.EncerraRecursoNaPrimeiraInstancia);
    }

    /// <summary>Os itens recorridos das fases e depois os das etapas, cada um na posição do lugar e na ordem canônica dos procedimentos.</summary>
    internal static IReadOnlyList<ItemJulgado> Itens(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(etapas);

        return [.. ItensDasFases(cronograma, etapas), .. ItensDasEtapas(cronograma, etapas)];
    }

    /// <summary>As recusas dos itens que nenhuma banca julga em 1ª instância; o item que a leitura deixou indeterminado não entra, e o mesmo texto no mesmo lugar sai uma vez.</summary>
    internal static IReadOnlyList<FieldError> SemBanca(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        .. Itens(cronograma, etapas)
            .Where(static item => item is { Indeterminado: false, Nivel: Nivel.Ninguem })
            .Select(static item => item.Recurso.NasBancas(item.Recurso.SemBanca(item.Procedimento?.TipoDeDecisao)))
            .Distinct(),
    ];

    /// <summary>
    /// As recusas dos recursos que admitem instância superior e têm item avaliado por banca cujo tipo encerra o recurso na 1ª
    /// instância: uma por recurso, citando a primeira banca e o item dela.
    /// </summary>
    internal static IReadOnlyList<FieldError> InstanciaSuperiorNaoAdmitida(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        .. Itens(cronograma, etapas)
            .Where(static item => item.Recurso.Recurso.AdmiteInstanciaSuperior && !item.Indeterminado && item.AvaliadoraQueEncerra is not null)
            .GroupBy(static item => item.Recurso)
            .Select(static recurso => RecusaDaInstanciaSuperior(recurso.First())),
    ];

    /// <summary>As bancas que julgam o recurso por terem avaliado o item, sem banca dedicada no lugar, onde se sabe quem julga.</summary>
    internal static IReadOnlySet<BancaDeclarada> AvaliadorasQueJulgam(IEnumerable<ItemJulgado> itens)
    {
        ArgumentNullException.ThrowIfNull(itens);

        // Por identidade: duas bancas de lugares diferentes podem ter os mesmos valores.
        return new HashSet<BancaDeclarada>(
            itens.Where(static item => item.JulgadoPelasAvaliadoras).SelectMany(static item => item.Avaliadoras),
            ReferenceEqualityComparer.Instance);
    }

    /// <summary>Se há item indeterminado com banca que o avaliou: não se sabe se ela julga o recurso, e o que se exige de quem julga fica sem conferir.</summary>
    internal static bool AvaliadoraSemSaberSeJulga(IEnumerable<ItemJulgado> itens)
    {
        ArgumentNullException.ThrowIfNull(itens);

        return itens.Any(static item => item.Indeterminado && item.Avaliadoras.Count > 0);
    }

    /// <summary>As etapas cujo recurso é julgado, com certeza, pela banca da fase dedicada à 1ª instância.</summary>
    internal static IReadOnlyList<EtapaJulgada> EtapasJulgadasPelaFase(IEnumerable<ItemJulgado> itens, FaseDeclarada fase)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(fase);

        return
        [
            .. itens
                .Where(static item => item is { Indeterminado: false, Nivel: Nivel.BancaDeRecursosDaFase, Recurso.Etapa: not null })
                .Select(static item => item.Recurso.Etapa!)
                .Where(etapa => string.Equals(etapa.Etapa.FaseCodigo, fase.Codigo, StringComparison.Ordinal))
                .DistinctBy(static etapa => etapa.Posicao),
        ];
    }

    /// <summary>As bancas dedicadas a julgar o recurso em 1ª instância entre as dadas.</summary>
    internal static IReadOnlyList<BancaDeclarada> DedicadasDaPrimeiraInstancia(IEnumerable<BancaDeclarada> bancas) =>
        [.. bancas.Where(static banca => banca.Tipo.JulgaAPrimeiraInstancia)];

    /// <summary>As bancas entre as dadas que avaliaram o tipo de decisão, na ordem em que vieram.</summary>
    internal static IReadOnlyList<BancaDeclarada> QueAvaliaram(IEnumerable<BancaDeclarada> bancas, TipoMotivoDeDecisao tipoDeDecisao) =>
        [.. bancas.Where(banca => banca.Tipo.AvaliaODecisao(tipoDeDecisao))];

    private static IEnumerable<ItemJulgado> ItensDasFases(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        cronograma.Fases.SelectMany((fase, posicao) => fase.Atos.Recurso is { } recurso
            ? Julgar(new RecursoNaFase(fase, posicao, recurso, etapas))
            : []);

    private static IEnumerable<ItemJulgado> ItensDasEtapas(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        etapas.Etapas.SelectMany((etapa, posicao) => etapa.Atos.Recursos.SelectMany((recurso, indice) =>
            Julgar(new RecursoNaEtapa(cronograma, new EtapaJulgada(etapa, posicao), indice, recurso))));

    // O recurso sem procedimento é um item só, sem tipo de decisão: só banca dedicada à 1ª instância pode julgá-lo.
    private static IEnumerable<ItemJulgado> Julgar(IRecursoNoLugar recurso)
    {
        IReadOnlyList<ProcedimentoDeRecursoDeclarado> procedimentos = recurso.Recurso.Procedimentos;
        ProcedimentoDeRecursoDeclarado?[] itens = procedimentos.Count == 0 ? [null] : [.. procedimentos];

        return itens.Select(procedimento => Julgar(recurso, procedimento));
    }

    private static ItemJulgado Julgar(IRecursoNoLugar recurso, ProcedimentoDeRecursoDeclarado? procedimento)
    {
        IReadOnlyList<BancaDeclarada> avaliadoras = recurso.Avaliadoras(procedimento);

        return new(recurso, procedimento, NivelDe(recurso, avaliadoras), avaliadoras, recurso.Indeterminado(procedimento));
    }

    private static Nivel NivelDe(IRecursoNoLugar recurso, IReadOnlyList<BancaDeclarada> avaliadoras)
    {
        BancasDoNivel? dedicadas = recurso.DedicadasDaPrimeiraInstancia.FirstOrDefault(static nivel => nivel.Bancas.Count > 0);

        return dedicadas?.Nivel ?? (avaliadoras.Count > 0 ? Nivel.BancasQueAvaliaramOItem : Nivel.Ninguem);
    }

    private static FieldError RecusaDaInstanciaSuperior(ItemJulgado item)
    {
        BancaDeclarada avaliadora = item.AvaliadoraQueEncerra!;

        return item.Recurso.NaInstanciaSuperior(RecusasDoRecurso.InstanciaSuperiorNaoAdmitida(item.Recurso.Onde, avaliadora, item.Procedimento!.TipoDeDecisao));
    }
}
