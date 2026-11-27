namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O cronograma como conjunto de fases: existência (que a parte recusada na lista suspende), ordem única, fase canônica única,
/// a marca "no mesmo momento da anterior" e a precedência que o cadastro declara entre elas. Cada causa sai na posição da fase
/// que a repete ou a viola.
/// </summary>
internal static class EstruturaDoCronograma
{
    internal const string CronogramaVazio = "ProcessoSeletivo.CronogramaFasesVazio";

    private const string CampoDaMarca = "noMesmoMomentoDaAnterior";

    internal static IReadOnlyList<FieldError> Recusas(CronogramaDoProcesso cronograma) =>
    [
        .. FasesAusentes(cronograma),
        .. OrdensRepetidas(cronograma.Fases),
        .. FasesCanonicasRepetidas(cronograma.Fases),
        .. PrimeiraFaseNoMesmoMomentoDaAnterior(cronograma),
    ];

    /// <summary>
    /// A precedência de cada aresta do cadastro. A aresta com fase ausente não se aplica, e a que envolve código
    /// repetido fica suspensa: a repetição já é causa de estrutura, e não há como saber qual fase o código designa. A
    /// aresta entre duas fases de ordens distintas que acontecem no mesmo momento, por causa da marca, é recusada pela marca; a
    /// de ordens iguais fica com a recusa da ordem repetida e da precedência, sem recusa nova.
    /// </summary>
    internal static IReadOnlyList<FieldError> Precedencia(CronogramaDoProcesso cronograma, IReadOnlyList<ArestaPrecedencia> arestas)
    {
        IReadOnlyDictionary<int, int> momentos = OrdemDoCronograma.MomentosPorOrdem(
            cronograma.Fases.Select(static fase => (fase.Ordem, fase.NoMesmoMomentoDaAnterior)));

        return [.. arestas.Select(aresta => ViolacaoDaAresta(cronograma, aresta, momentos)).OfType<FieldError>()];
    }

    // A marca na fase de menor ordem não junta a fase a nenhuma outra. Com a menor ordem repetida a recusa é da ordem repetida, e a
    // leitura que recusou fase do documento deixou a lista sem as fases que o autor escreveu: a primeira não se afirma.
    private static IEnumerable<FieldError> PrimeiraFaseNoMesmoMomentoDaAnterior(CronogramaDoProcesso cronograma)
    {
        if (cronograma.TemParteRecusadaNaListaDeFases || cronograma.Fases.Count == 0)
        {
            return [];
        }

        int menorOrdem = cronograma.Fases.Min(static fase => fase.Ordem);

        return cronograma.Fases.Where(fase => fase.Ordem == menorOrdem).ToArray() is [{ NoMesmoMomentoDaAnterior: true } primeira]
            ? [PrimeiraFaseMarcada(cronograma, primeira)]
            : [];
    }

    private static FieldError PrimeiraFaseMarcada(CronogramaDoProcesso cronograma, FaseDeclarada primeira) =>
        CronogramaDoProcesso.NaFase(
            cronograma.PosicaoDe(primeira),
            CampoDaMarca,
            new DomainError(
                ProcessoSeletivoErrorCodes.PrimeiraFaseNoMesmoMomentoDaAnterior,
                "A primeira fase do cronograma está marcada para acontecer no mesmo momento da anterior, mas não há fase antes dela. Retire a marca dessa fase ou coloque antes dela a fase com que ela deve acontecer."));

    // A leitura que recusou fase do documento deixa a lista sem as fases que o autor escreveu: a ausência não se afirma.
    private static IEnumerable<FieldError> FasesAusentes(CronogramaDoProcesso cronograma) =>
        cronograma.Fases.Count == 0 && !cronograma.TemParteRecusadaNaListaDeFases
            ? [new FieldError(CronogramaDoProcesso.ListaDasFases, new DomainError(CronogramaVazio, "O processo deve ter ao menos uma fase no cronograma."))]
            : [];

    private static IEnumerable<FieldError> OrdensRepetidas(IReadOnlyList<FaseDeclarada> fases)
    {
        HashSet<int> vistas = [];
        for (int posicao = 0; posicao < fases.Count; posicao++)
        {
            if (!vistas.Add(fases[posicao].Ordem))
            {
                yield return CronogramaDoProcesso.NaFase(
                    posicao, "ordem", new DomainError("ProcessoSeletivo.OrdemFaseDuplicada", "Cada fase deve ter uma ordem única dentro do cronograma."));
            }
        }
    }

    // Origem e código repetidos são a mesma causa: o cadastro só garante código único entre as fases não excluídas, então
    // origens diferentes podem congelar o mesmo código.
    private static IEnumerable<FieldError> FasesCanonicasRepetidas(IReadOnlyList<FaseDeclarada> fases)
    {
        HashSet<Guid> origens = [];
        HashSet<string> codigos = new(StringComparer.Ordinal);
        for (int posicao = 0; posicao < fases.Count; posicao++)
        {
            bool origemRepetida = !origens.Add(fases[posicao].OrigemId);
            bool codigoRepetido = !codigos.Add(fases[posicao].Codigo);
            if (origemRepetida || codigoRepetido)
            {
                yield return CronogramaDoProcesso.NaFase(
                    posicao, "faseCanonicaId", new DomainError("ProcessoSeletivo.FaseCanonicaDuplicada", "A mesma fase canônica não pode aparecer duas vezes no cronograma."));
            }
        }
    }

    private static FieldError? ViolacaoDaAresta(CronogramaDoProcesso cronograma, ArestaPrecedencia aresta, IReadOnlyDictionary<int, int> momentos)
    {
        FaseDeclarada? antecessora = cronograma.FaseDeCodigo(aresta.AntecessoraCodigo);
        FaseDeclarada? sucessora = cronograma.FaseDeCodigo(aresta.SucessoraCodigo);
        if (antecessora is null || sucessora is null)
        {
            return null;
        }

        return MesmoMomento(cronograma, antecessora, sucessora, momentos)
            ?? OrdemViolada(cronograma, antecessora, sucessora, momentos)
            ?? SobreposicaoNaoPermitida(cronograma, aresta, antecessora, sucessora);
    }

    // A marca, e não a ordem, pôs as duas no mesmo momento: a recusa fica na fase que vem depois na lista. A lista com fase recusada
    // pela leitura não diz quem tem a marca.
    private static FieldError? MesmoMomento(
        CronogramaDoProcesso cronograma, FaseDeclarada antecessora, FaseDeclarada sucessora, IReadOnlyDictionary<int, int> momentos)
    {
        bool juntasPelaMarca = antecessora.Ordem != sucessora.Ordem && momentos[antecessora.Ordem] == momentos[sucessora.Ordem];
        if (cronograma.TemParteRecusadaNaListaDeFases || !juntasPelaMarca)
        {
            return null;
        }

        FaseDeclarada junta = antecessora.Ordem > sucessora.Ordem ? antecessora : sucessora;

        return CronogramaDoProcesso.NaFase(
            cronograma.PosicaoDe(junta),
            CampoDaMarca,
            new DomainError(
                ProcessoSeletivoErrorCodes.FasesComPrecedenciaNoMesmoMomento,
                $"A fase {antecessora.Citacao} vem antes da fase {sucessora.Citacao} no cadastro de precedência, e as duas estão marcadas para acontecer no mesmo momento. Retire a marca \"no mesmo momento da anterior\" que junta as duas ou separe-as na ordem do cronograma."));
    }

    private static FieldError? OrdemViolada(
        CronogramaDoProcesso cronograma, FaseDeclarada antecessora, FaseDeclarada sucessora, IReadOnlyDictionary<int, int> momentos) =>
        antecessora.Ordem == sucessora.Ordem || momentos[antecessora.Ordem] > momentos[sucessora.Ordem]
            ? CronogramaDoProcesso.NaFase(
                cronograma.PosicaoDe(sucessora),
                "ordem",
                new DomainError(
                    "ProcessoSeletivo.PrecedenciaFaseViolada",
                    $"A fase {antecessora.Citacao} (ordem {antecessora.Ordem}) precede {sucessora.Citacao} (ordem {sucessora.Ordem}) — a ordem declarada viola a precedência do cadastro."))
            : null;

    // Janela invertida não tem extremos confiáveis: a sobreposição daquela fase não se avalia, e a recusa da janela já está na declaração.
    private static FieldError? SobreposicaoNaoPermitida(
        CronogramaDoProcesso cronograma, ArestaPrecedencia aresta, FaseDeclarada antecessora, FaseDeclarada sucessora) =>
        !aresta.PermiteSobreposicao
        && !antecessora.Janela.Invertida
        && !sucessora.Janela.Invertida
        && antecessora.Janela.Fim > sucessora.Janela.Inicio
            ? CronogramaDoProcesso.NaFase(
                cronograma.PosicaoDe(sucessora),
                "inicio",
                new DomainError(
                    "ProcessoSeletivo.SobreposicaoDeJanelasNaoPermitida",
                    "O período desta fase coincide em parte com o da fase anterior, e o cadastro não permite essa coincidência."))
            : null;
}
