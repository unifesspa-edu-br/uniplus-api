namespace Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.OrdemCanonica;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As fases e as etapas em que o processo pode decidir, as fases por ordem e depois as etapas por ordem. É o único
/// lugar que sabe se uma fase ou etapa existe no processo e quais tipos de decisão ela declara produzir, e a diferença
/// entre dois retratos dele diz quais saíram. Quando o retrato nasce de um documento lido, sabe também o que a leitura
/// recusou do que declara os tipos de decisão: o que se conclui de nenhum lugar declarar um tipo fica sem saber.
/// </summary>
public sealed class LugaresDaDecisao
{
    private const string ListaDasFases = "fases";
    private const string ListaDasEtapas = "etapas";

    private LugaresDaDecisao(IReadOnlyList<LugarNoProcesso> lugares, bool temLugarNaoLido)
    {
        Lugares = lugares;
        DeclaracaoDosTiposComParteRecusada = temLugarNaoLido || lugares.Any(static l => l.TiposDeDecisaoComParteRecusada);
    }

    /// <summary>Os lugares do retrato, na ordem das fases e depois das etapas.</summary>
    internal IReadOnlyList<LugarNoProcesso> Lugares { get; }

    /// <summary>
    /// Se a leitura recusou uma fase ou uma etapa inteira, ou a lista dos tipos de decisão de uma delas: não se sabe quais
    /// lugares declaram um tipo, e nenhuma regra conclui que nenhum o declara.
    /// </summary>
    internal bool DeclaracaoDosTiposComParteRecusada { get; }

    /// <summary>Retrato dos lugares nas fases e etapas informadas, sem parte recusada.</summary>
    public static LugaresDaDecisao Das(IEnumerable<FaseCronograma> fases, IEnumerable<EtapaProcesso> etapas) =>
        Das(fases, etapas, OrigemNoDocumento.Identidade);

    /// <summary>Retrato dos lugares da configuração declarada, com o que a leitura do documento recusou.</summary>
    public static LugaresDaDecisao Da(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return Das(configuracao.Cronograma.CronogramaFases, configuracao.Avaliacao.Etapas, configuracao.Origem);
    }

    private static LugaresDaDecisao Das(IEnumerable<FaseCronograma> fases, IEnumerable<EtapaProcesso> etapas, OrigemNoDocumento origem)
    {
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(etapas);

        IEnumerable<LugarNoProcesso> dasFases = OrdemCanonicaDaConfiguracao.Fases(fases).Select((f, posicao) => new LugarNoProcesso(
            LugarDaDecisao.DaFase(f.FaseCanonicaOrigemId), $"A fase '{f.Codigo}'", TiposDeDecisaoDe(f.TiposDeDecisao), TiposRecusadosEm(origem, ListaDasFases, posicao)));
        IEnumerable<LugarNoProcesso> dasEtapas = OrdemCanonicaDaConfiguracao.Etapas(etapas).Select((e, posicao) => new LugarNoProcesso(
            LugarDaDecisao.DaEtapa(e.Id), $"A etapa '{e.Nome}'", TiposDeDecisaoDe(e.TiposDeDecisao), TiposRecusadosEm(origem, ListaDasEtapas, posicao)));

        return new([.. dasFases, .. dasEtapas], origem.TemParteRecusadaEm(ListaDasFases) || origem.TemParteRecusadaEm(ListaDasEtapas));
    }

    public bool Contem(LugarDaDecisao lugar) => Lugares.Any(l => l.Lugar == lugar);

    /// <summary>Se o lugar existe no processo e declara produzir decisões do tipo.</summary>
    public bool Declara(LugarDaDecisao lugar, TipoMotivoDeDecisao tipo) =>
        Lugares.Any(l => l.Lugar == lugar && l.TiposDeDecisao.Contains(tipo));

    /// <summary>Os lugares que declaram produzir decisões do tipo, na ordem do retrato.</summary>
    internal IEnumerable<LugarNoProcesso> QueDeclaram(TipoMotivoDeDecisao tipo) => Lugares.Where(l => l.TiposDeDecisao.Contains(tipo));

    /// <summary>Os lugares deste retrato que o retrato posterior não traz.</summary>
    internal IEnumerable<LugarDaDecisao> AusentesEm(LugaresDaDecisao depois)
    {
        ArgumentNullException.ThrowIfNull(depois);

        return Lugares.Where(l => !depois.Contem(l.Lugar)).Select(static l => l.Lugar);
    }

    private static bool TiposRecusadosEm(OrigemNoDocumento origem, string lista, int posicao) =>
        origem.PartesRecusadasDe($"{lista}[{posicao}]").Contains(TiposDeDecisaoDeclarados.Campo);

    private static HashSet<TipoMotivoDeDecisao> TiposDeDecisaoDe(IEnumerable<string> declarados) =>
        [.. declarados.Select(TipoMotivoDeDecisaoCodigo.FromCodigo)];
}

/// <summary>
/// Um lugar da decisão com o nome dele na mensagem, os tipos de decisão que declara e se a leitura recusou parte da lista
/// deles: então os tipos declarados são só os aceitos, e o lugar pode declarar outros que não se sabe quais são.
/// </summary>
internal sealed record LugarNoProcesso(
    LugarDaDecisao Lugar, string Descricao, IReadOnlySet<TipoMotivoDeDecisao> TiposDeDecisao, bool TiposDeDecisaoComParteRecusada);
