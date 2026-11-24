namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As bancas que o processo exige conforme a origem dos candidatos: a de homologação das inscrições, em toda origem e em cada
/// fase que homologa, e as de importação (das notas do INEP, quando a nota do ENEM é usada com inscrição própria; de candidatos,
/// na importação externa). A banca de homologação é a que a cópia do tipo diz que avalia o mérito e julga a homologação das
/// inscrições (<see cref="TipoDeBancaCopiado.AvaliaODecisao"/>), na fase ou nas etapas dela; as de importação, a de papel
/// que importa. Nenhuma é reconhecida pelo código do tipo. Cada exigência julga só o que se pode saber: a parte que a leitura do
/// documento recusou deixa a exigência sem julgar, e o item do painel fica pendente.
/// </summary>
internal static class ExigenciaDasBancasDaHomologacao
{
    private static readonly DomainError NotaDoEnemSemBanca = new(
        ProcessoSeletivoErrorCodes.NotaDoEnemSemBancaDeImportacaoDasNotas,
        "O processo usa a nota do ENEM com inscrição própria e não prevê a banca de importação das notas do INEP em nenhuma fase ou etapa. Acrescente essa banca onde as notas são importadas.");

    private static readonly DomainError ImportacaoExternaSemBanca = new(
        ProcessoSeletivoErrorCodes.ImportacaoExternaSemBancaDeImportacaoDeCandidatos,
        "O processo importa candidatos de fora e não prevê a banca de importação de candidatos em nenhuma fase ou etapa. Acrescente essa banca onde os candidatos são importados.");

    /// <summary>As causas encontradas e se a exigência ficou sem julgar por parte recusada ou por não se aplicar ainda.</summary>
    internal readonly record struct Veredicto(IReadOnlyList<FieldError> Causas, bool Indeterminado)
    {
        internal static Veredicto Atendido => new([], Indeterminado: false);

        internal static Veredicto SemJulgar => new([], Indeterminado: true);
    }

    private sealed record FaseNaPosicao(FaseDeclarada Fase, int Posicao);

    /// <summary>As causas das três exigências, na ordem em que o autor as corrige: homologação, importação das notas e importação de candidatos.</summary>
    internal static IReadOnlyList<FieldError> Causas(OrigemCandidatos? origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        .. BancaDeHomologacaoDasInscricoes(origem, cronograma, etapas).Causas,
        .. BancaDeImportacaoDasNotasDoInep(origem, cronograma, etapas).Causas,
        .. BancaDeImportacaoDeCandidatos(origem, cronograma, etapas).Causas,
    ];

    /// <summary>
    /// Cada fase que homologa tem a banca de homologação das inscrições, nela ou nas etapas dela. Sem fase que homologa, ou com
    /// a origem por declarar, a exigência não se julga.
    /// </summary>
    internal static Veredicto BancaDeHomologacaoDasInscricoes(OrigemCandidatos? origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas)
    {
        FaseNaPosicao[] homologam = [.. cronograma.Fases.Select(static (fase, posicao) => new FaseNaPosicao(fase, posicao)).Where(static par => ExigenciaDaHomologacaoPorOrigem.HomologaAsInscricoes(par.Fase))];

        if (origem is null || homologam.Length == 0 || FasesSemJulgar(cronograma))
        {
            return Veredicto.SemJulgar;
        }

        FaseNaPosicao[] semBanca = [.. homologam.Where(par => !TemBancaDeHomologacao(par.Fase, etapas))];

        return new(
            [.. semBanca.Where(par => !BancasDaFaseIncertas(par.Fase, etapas)).Select(CausaDaFase)],
            semBanca.Any(par => BancasDaFaseIncertas(par.Fase, etapas)));
    }

    /// <summary>O processo que usa a nota do ENEM com inscrição própria tem a banca de importação das notas do INEP em alguma fase ou etapa.</summary>
    internal static Veredicto BancaDeImportacaoDasNotasDoInep(OrigemCandidatos? origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) => origem switch
    {
        null => Veredicto.SemJulgar,
        OrigemCandidatos.InscricaoPropria => NotaDoEnemComInscricaoPropria(cronograma, etapas),
        _ => Veredicto.Atendido,
    };

    /// <summary>O processo que importa candidatos de fora tem a banca de importação de candidatos em alguma fase ou etapa.</summary>
    internal static Veredicto BancaDeImportacaoDeCandidatos(OrigemCandidatos? origem, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) => origem switch
    {
        null => Veredicto.SemJulgar,
        OrigemCandidatos.ImportacaoExterna => ComBancaDeImportacao(PapelDaBanca.ImportaCandidatos, ImportacaoExternaSemBanca, cronograma, etapas),
        _ => Veredicto.Atendido,
    };

    private static Veredicto NotaDoEnemComInscricaoPropria(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        etapas.UsoDaNotaDoEnemIndeterminado ? Veredicto.SemJulgar
        : etapas.UsaNotaDoEnem ? ComBancaDeImportacao(PapelDaBanca.ImportaAsNotasDoInep, NotaDoEnemSemBanca, cronograma, etapas)
        : Veredicto.Atendido;

    // Quem decide é o papel copiado. Sem a banca entre as declaradas, parte recusada na leitura pode escondê-la, e a recusa
    // não se afirma.
    private static Veredicto ComBancaDeImportacao(PapelDaBanca papel, DomainError semBanca, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        BancasDoProcesso(cronograma, etapas).Any(banca => banca.Tipo.Papel == papel) ? Veredicto.Atendido
        : BancasDoProcessoIncertas(cronograma, etapas) ? Veredicto.SemJulgar
        : new([new FieldError(CronogramaDoProcesso.ListaDasFases, semBanca)], Indeterminado: false);

    // A lista de fases ou a regra de homologação recusada na leitura: não se sabe quais fases homologam.
    private static bool FasesSemJulgar(CronogramaDoProcesso cronograma) =>
        cronograma.TemParteRecusadaNaListaDeFases || ExigenciaDaHomologacaoPorOrigem.RegraRecusadaEmAlgumaFase(cronograma);

    private static bool TemBancaDeHomologacao(FaseDeclarada fase, EtapasDoProcesso etapas) =>
        fase.Bancas.Declaradas.Concat(etapas.DaFase(fase).SelectMany(static etapa => etapa.Bancas.Declaradas))
            .Any(static banca => banca.Tipo.AvaliaODecisao(TipoMotivoDeDecisao.HomologacaoDasInscricoes));

    private static BancaDeclarada[] BancasDoProcesso(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        [.. cronograma.Fases.SelectMany(static fase => fase.Bancas.Declaradas), .. etapas.Etapas.SelectMany(static etapa => etapa.Bancas.Declaradas)];

    private static bool BancasDaFaseIncertas(FaseDeclarada fase, EtapasDoProcesso etapas) =>
        fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas)
        || etapas.LeituraRecusouEtapa
        || etapas.DaFase(fase).Any(static etapa => etapa.ParteRecusadaEm(EtapaDeclarada.ListaDasBancas));

    // A fase ou a etapa que a leitura recusou inteira pode ter a banca, e a lista de bancas recusada em parte também.
    private static bool BancasDoProcessoIncertas(CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
        cronograma.TemParteRecusadaNaListaDeFases
        || etapas.LeituraRecusouEtapa
        || cronograma.Fases.Any(static fase => fase.ParteRecusadaEm(FaseDeclarada.ListaDasBancasRequeridas))
        || etapas.Etapas.Any(static etapa => etapa.ParteRecusadaEm(EtapaDeclarada.ListaDasBancas));

    private static FieldError CausaDaFase(FaseNaPosicao par) => CronogramaDoProcesso.NaFase(
        par.Posicao,
        FaseDeclarada.ListaDasBancasRequeridas,
        new DomainError(
            ProcessoSeletivoErrorCodes.HomologacaoDasInscricoesSemBanca,
            $"A fase '{par.Fase.Codigo}' homologa as inscrições, e nem ela nem as etapas dela preveem banca que julgue a homologação das inscrições. Toda origem de candidatos exige essa banca. Acrescente-a à fase ou a uma etapa dela."));
}
