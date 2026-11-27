namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class TiposDeAtoDoCronograma
{
    /// <summary>
    /// As vagas que a operação e os atos já registrados reivindicam, uma por tipo: o ato da operação e o ato já registrado do
    /// mesmo tipo são a mesma vaga — a retificação continua a própria linhagem, não disputa com ela —, e por isso contam uma
    /// vez só.
    /// </summary>
    internal static IEnumerable<DeclaracaoDeAto> VagasJaReivindicadas(TiposDeAtoUnicosPorObjeto tiposUnicos) =>
    [
        .. tiposUnicos.CodigosOcupados
            .Where(codigo => OcupaVagaUnica(tiposUnicos, codigo))
            .Select(static codigo => new DeclaracaoDeAto(codigo, "no ato já registrado do processo")),
        .. VagaDoAtoDaOperacao(tiposUnicos),
    ];

    /// <summary>
    /// Cada produto que o cronograma declara, o das fases e o das etapas, com o lugar que o declara. É a fonte única dos atos
    /// declarados: quem precisa saber quais tipos de ato o processo cita lê daqui. A etapa é identificada pela ordem além do
    /// nome, porque o nome não é único: duas etapas de mesmo nome em lugares distintos seriam contadas como uma.
    /// </summary>
    public static IReadOnlyList<DeclaracaoDeAto> DeclaracoesDeAtos(ConfiguracaoDeclarada configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return [
            .. configuracao.Cronograma.CronogramaFases.SelectMany(static fase => fase.Produtos
                .Select(produto => new DeclaracaoDeAto(produto.AtoCodigo, LugarDaDeclaracao.DaFase(fase.Citacao), produto.Papel))),
            .. configuracao.Avaliacao.Etapas.SelectMany(static etapa => etapa.Produtos
                .Select(produto => new DeclaracaoDeAto(produto.AtoCodigo, LugarDaDeclaracao.DaEtapa(etapa.Nome, etapa.Ordem), produto.Papel))),
        ];
    }

    private static bool OcupaVagaUnica(TiposDeAtoUnicosPorObjeto tiposUnicos, string codigo) =>
        tiposUnicos.EhUnico(codigo)
        || (tiposUnicos.AtoDaOperacaoEhUnico && string.Equals(codigo, tiposUnicos.AtoDaOperacaoCodigo, StringComparison.Ordinal));

    private static IEnumerable<DeclaracaoDeAto> VagaDoAtoDaOperacao(TiposDeAtoUnicosPorObjeto tiposUnicos) =>
        tiposUnicos.AtoDaOperacaoCodigo is { } atoDaOperacao && tiposUnicos.AtoDaOperacaoEhUnico && !tiposUnicos.VagaOcupada(atoDaOperacao)
            ? [new DeclaracaoDeAto(atoDaOperacao, "no ato que está sendo registrado")]
            : [];
}
