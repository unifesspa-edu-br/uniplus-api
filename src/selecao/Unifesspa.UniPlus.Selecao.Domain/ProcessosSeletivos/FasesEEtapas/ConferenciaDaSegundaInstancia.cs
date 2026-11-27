namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Veredicto = Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas.ExigenciaDasBancasDaHomologacao.Veredicto;

/// <summary>
/// A banca de 2ª instância que o recurso exige e a que sobra, juntando fase e etapa. Falta a banca onde um recurso admite
/// instância superior e ninguém a prevê no lugar certo (na fase, a própria fase; na etapa, ela ou a fase em que acontece); sobra
/// a banca onde nenhum recurso a faria julgar. Cada pergunta é julgada só com o que se pode saber: a parte que a leitura do
/// documento recusou, o recurso que a recusa de instância superior já apontou e a fase ou etapa recusada inteira deixam a
/// conferência sem julgar, e o item do painel fica pendente em vez de aprovado por omissão. As causas saem na posição do lugar,
/// as das fases antes das das etapas.
/// </summary>
internal static class ConferenciaDaSegundaInstancia
{
    /// <summary>
    /// Os recursos além do da própria fase que a banca de 2ª instância da fase julga: os das etapas da fase que admitem instância
    /// superior e não têm banca de 2ª instância própria. O alcance é incerto quando a leitura recusou parte do que o decide (etapa
    /// recusada inteira, ou lista de bancas ou de recursos de etapa da fase sem banca própria), e diz se alguma etapa que admite
    /// já tem a sua banca, para a mensagem orientar a retirada certa.
    /// </summary>
    private sealed record AlcanceDaBancaDaFase(IReadOnlyList<EtapaDeclarada> Etapas, bool Incerto, bool EtapaQueAdmiteTemBancaPropria);

    /// <summary>Todas as causas da banca de 2ª instância: primeiro as faltas, depois as sobras e os excessos.</summary>
    internal static IReadOnlyList<FieldError> Conferir(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        [.. Faltas(cronograma, etapas).Causas, .. Sobras(cronograma, etapas).Causas, .. Excessos(cronograma, etapas).Causas];

    /// <summary>O recurso que admite instância superior sem banca de 2ª instância no lugar certo: primeiro as fases, depois as etapas.</summary>
    internal static Veredicto Faltas(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        IReadOnlyList<QuemJulgaORecurso.ItemJulgado> recusadosPelaInstanciaSuperior =
            [.. QuemJulgaORecurso.Itens(cronograma, etapas).Where(static item => item.InstanciaSuperiorRecusada)];

        return Juntar(
            cronograma,
            etapas,
            [
                .. cronograma.Fases.Select((fase, posicao) => FaltaNaFase(fase, posicao, recusadosPelaInstanciaSuperior)),
                .. etapas.Etapas.Select((etapa, posicao) => FaltaNaEtapa(cronograma, etapa, posicao, recusadosPelaInstanciaSuperior)),
            ]);
    }

    /// <summary>A banca de 2ª instância sem recurso que ela julgue: primeiro as das fases, depois as das etapas, uma recusa por banca.</summary>
    internal static Veredicto Sobras(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        Juntar(
            cronograma,
            etapas,
            [
                .. cronograma.Fases.Select((fase, posicao) => SobraNaFase(fase, posicao, EtapasNoAlcanceDaBancaDaFase(fase, etapas))),
                .. etapas.Etapas.Select(SobraNaEtapa),
            ]);

    /// <summary>O excesso de bancas aceitas em cada lugar, exceto onde a sobra já orienta a retirada.</summary>
    internal static Veredicto Excessos(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        Juntar(cronograma, etapas,
        [
            .. cronograma.Fases.Select((fase, posicao) => ExcessoNaFase(fase, posicao, etapas)),
            .. etapas.Etapas.Select(ExcessoNaEtapa),
        ]);

    private static Veredicto ExcessoNaFase(FaseDeclarada fase, int posicao, EtapasDoProcesso etapas) =>
        ExcessoDasBancas(
            fase.Bancas.BancasDaSegundaInstancia,
            fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas),
            SobraNaFase(fase, posicao, EtapasNoAlcanceDaBancaDaFase(fase, etapas)),
            banca => CronogramaDoProcesso.NaFase(posicao, $"{FaseDeclarada.ListaDasBancasRequeridas}[{banca.Posicao}]",
                ExigenciaDaBancaDaSegundaInstancia.FaseComMaisDeUmaBanca(fase.Citacao, fase.Bancas.BancasDaSegundaInstancia.Count, banca)));

    private static Veredicto ExcessoNaEtapa(EtapaDeclarada etapa, int posicao) =>
        ExcessoDasBancas(
            etapa.Bancas.BancasDaSegundaInstancia,
            etapa.ParteRecusadaEm(EtapaDeclarada.ListaDasBancas),
            SobraNaEtapa(etapa, posicao),
            banca => EtapasDoProcesso.NaEtapa(posicao, $"{EtapaDeclarada.ListaDasBancas}[{banca.Posicao}]",
                ExigenciaDaBancaDaSegundaInstancia.EtapaComMaisDeUmaBanca(posicao, etapa.Bancas.BancasDaSegundaInstancia.Count, banca)));

    private static Veredicto ExcessoDasBancas(
        IReadOnlyList<BancaDeclarada> bancas, bool listaRecusada, Veredicto sobra, Func<BancaDeclarada, FieldError> recusa) =>
        sobra.Causas.Count > 0
            ? Veredicto.SemJulgar
            : new(bancas.Count > 1 ? [recusa(bancas[1])] : [], listaRecusada);

    private static Veredicto Juntar(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, Veredicto[] lugares) =>
        new(
            [.. lugares.SelectMany(static lugar => lugar.Causas)],
            lugares.Any(static lugar => lugar.Indeterminado) || ConferenciaDasBancas.FaseOuEtapaRecusadaNaLeitura(cronograma, etapas));

    private static Veredicto FaltaNaFase(FaseDeclarada fase, int posicao, IReadOnlyList<QuemJulgaORecurso.ItemJulgado> recusadosPelaInstanciaSuperior)
    {
        bool regraRecusada = fase.ParteRecusadaEm(FaseDeclarada.CampoDaRegraDeRecurso);

        if (fase.Bancas.BancasDaSegundaInstancia.Count > 0 || !(fase.Atos.AdmiteInstanciaSuperior || regraRecusada))
        {
            return Veredicto.Atendido;
        }

        bool cortada = regraRecusada
            || fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas)
            || recusadosPelaInstanciaSuperior.Any(item => item.Recurso is RecursoNaFase noLugar && noLugar.Posicao == posicao);

        return cortada ? Veredicto.SemJulgar : new([CausaDaFaltaNaFase(fase, posicao)], Indeterminado: false);
    }

    private static Veredicto FaltaNaEtapa(
        CronogramaDoProcesso cronograma, EtapaDeclarada etapa, int posicao, IReadOnlyList<QuemJulgaORecurso.ItemJulgado> recusadosPelaInstanciaSuperior)
    {
        if (!SemBancaQueJulgue(cronograma, etapa))
        {
            return Veredicto.Atendido;
        }

        bool cortada = etapa.ParteRecusadaEm(EtapaDeclarada.ListaDosRecursos)
            || etapa.ParteRecusadaEm(EtapaDeclarada.ListaDasBancas)
            || JulgadorasIndeterminadas.BancasDaFaseDaEtapaIncertas(cronograma, etapa)
            || recusadosPelaInstanciaSuperior.Any(item => item.Recurso.Etapa?.Posicao == posicao);

        return cortada ? Veredicto.SemJulgar : new([CausaDaFaltaNaEtapa(cronograma, etapa, posicao)], Indeterminado: false);
    }

    // A etapa cujos recursos têm parte recusada pode admitir instância superior: a falta fica para ser julgada, e não afirmada.
    private static bool SemBancaQueJulgue(CronogramaDoProcesso cronograma, EtapaDeclarada etapa) =>
        ExigenciaDaBancaDaSegundaInstancia.EtapaSemBancaQueJulgueASegundaInstancia(
            etapa.Atos.TemRecursoComInstanciaSuperior || etapa.ParteRecusadaEm(EtapaDeclarada.ListaDosRecursos),
            etapa.Bancas.BancasDaSegundaInstancia.Count > 0,
            FaseDaEtapa(cronograma, etapa)?.Bancas.BancasDaSegundaInstancia.Count > 0);

    private static FieldError CausaDaFaltaNaFase(FaseDeclarada fase, int posicao) => CronogramaDoProcesso.NaFase(
        posicao, FaseDeclarada.ListaDasBancasRequeridas, ExigenciaDaBancaDaSegundaInstancia.FaseSemBancaDaSegundaInstancia(fase.Citacao));

    private static FieldError CausaDaFaltaNaEtapa(CronogramaDoProcesso cronograma, EtapaDeclarada etapa, int posicao) => EtapasDoProcesso.NaEtapa(
        posicao, EtapaDeclarada.ListaDasBancas, ExigenciaDaBancaDaSegundaInstancia.EtapaSemBancaDaSegundaInstancia(posicao, FaseDaEtapa(cronograma, etapa)?.Citacao));

    private static Veredicto SobraNaFase(FaseDeclarada fase, int posicao, AlcanceDaBancaDaFase alcance)
    {
        bool regraRecusada = fase.ParteRecusadaEm(FaseDeclarada.CampoDaRegraDeRecurso);
        bool admiteComCerteza = (fase.Atos.AdmiteInstanciaSuperior && !regraRecusada) || (alcance.Etapas.Count > 0 && !alcance.Incerto);

        return admiteComCerteza
            ? Veredicto.Atendido
            : SobraDasBancas(
                fase.Bancas.BancasDaSegundaInstancia,
                fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas),
                regraRecusada || alcance.Incerto,
                banca => CausaDaSobraNaFase(fase, posicao, banca, alcance));
    }

    private static Veredicto SobraNaEtapa(EtapaDeclarada etapa, int posicao) =>
        etapa.Atos.TemRecursoComInstanciaSuperior
            ? Veredicto.Atendido
            : SobraDasBancas(
                etapa.Bancas.BancasDaSegundaInstancia,
                etapa.ParteRecusadaEm(EtapaDeclarada.ListaDasBancas),
                etapa.ParteRecusadaEm(EtapaDeclarada.ListaDosRecursos),
                banca => EtapasDoProcesso.NaEtapa(
                    posicao,
                    $"{EtapaDeclarada.ListaDasBancas}[{banca.Posicao}]",
                    ExigenciaDaBancaDaSegundaInstancia.EtapaComBancaSemRecursoParaJulgar(posicao, banca)));

    // Onde a admissão não se sabe, a banca aceita não é recusada e o item fica pendente; onde se sabe que nenhum recurso admite, a
    // banca aceita sai recusada e a parte recusada da lista, que pode esconder outra banca, deixa o item pendente.
    private static Veredicto SobraDasBancas(
        IReadOnlyList<BancaDeclarada> bancas, bool listaDasBancasRecusada, bool admissaoIncerta, Func<BancaDeclarada, FieldError> recusa) =>
        admissaoIncerta
            ? new([], bancas.Count > 0 || listaDasBancasRecusada)
            : new([.. bancas.Select(recusa)], listaDasBancasRecusada);

    private static FieldError CausaDaSobraNaFase(FaseDeclarada fase, int posicao, BancaDeclarada banca, AlcanceDaBancaDaFase alcance) =>
        CronogramaDoProcesso.NaFase(
            posicao,
            $"{FaseDeclarada.ListaDasBancasRequeridas}[{banca.Posicao}]",
            alcance.EtapaQueAdmiteTemBancaPropria
                ? ExigenciaDaBancaDaSegundaInstancia.FaseComBancaSoComEtapasQueTemBancaPropria(fase.Citacao, banca)
                : ExigenciaDaBancaDaSegundaInstancia.FaseComBancaSemNenhumRecursoQueAdmita(fase.Citacao, banca));

    // O ponto único da leitura estrita da banca da fase: ela só tem recurso para julgar se o da fase admite instância superior ou se
    // alguma etapa da fase que admite não tem banca de 2ª instância própria. Trocar essa leitura muda só este método.
    private static AlcanceDaBancaDaFase EtapasNoAlcanceDaBancaDaFase(FaseDeclarada fase, EtapasDoProcesso etapas)
    {
        EtapaDeclarada[] daFase = [.. etapas.DaFase(fase)];
        EtapaDeclarada[] semBancaPropria = [.. daFase.Where(static etapa => etapa.Bancas.BancasDaSegundaInstancia.Count == 0)];

        return new(
            [.. semBancaPropria.Where(static etapa => etapa.Atos.TemRecursoComInstanciaSuperior)],
            etapas.LeituraRecusouEtapa || semBancaPropria.Any(static etapa => etapa.ParteRecusadaEm(EtapaDeclarada.ListaDasBancas) || etapa.ParteRecusadaEm(EtapaDeclarada.ListaDosRecursos)),
            daFase.Any(static etapa => etapa.Atos.TemRecursoComInstanciaSuperior && etapa.Bancas.BancasDaSegundaInstancia.Count > 0));
    }

    private static FaseDeclarada? FaseDaEtapa(CronogramaDoProcesso cronograma, EtapaDeclarada etapa) =>
        etapa.FaseCodigo is { } codigo ? cronograma.FaseDeCodigo(codigo) : null;
}
