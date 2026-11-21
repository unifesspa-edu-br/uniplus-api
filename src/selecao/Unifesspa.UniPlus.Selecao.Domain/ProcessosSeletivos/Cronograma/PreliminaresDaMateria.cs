namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O único ponto que diz qual preliminar libera qual definitivo no cronograma do processo.
/// </summary>
/// <remarks>
/// <para>
/// A matéria é o tipo de ato, e o papel diz se a publicação é preliminar ou definitiva. O
/// definitivo espera os preliminares do mesmo tipo de ato declarados no mesmo lugar, a fase ou a
/// etapa que o publica; dois lugares que publicam o mesmo tipo não se ligam.
/// </para>
/// <para>
/// A consulta dos itens previstos e a publicação de resultado perguntam aqui; nenhuma delas
/// reimplementa o pareamento. Produto sem preliminar declarado para a matéria não espera nada.
/// </para>
/// </remarks>
public static class PreliminaresDaMateria
{
    /// <summary>
    /// Os identificadores dos produtos preliminares que o definitivo <paramref name="definitivoId"/>
    /// espera, ou lista vazia quando o cronograma não declara preliminar para a matéria dele.
    /// </summary>
    public static IReadOnlyList<Guid> Esperados(ConfiguracaoPublicada configuracao, Guid definitivoId)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return Esperados(configuracao.Cronograma.CronogramaFases, configuracao.Avaliacao.Etapas, definitivoId);
    }

    /// <summary>
    /// A mesma pergunta de <see cref="Esperados(ConfiguracaoPublicada, Guid)"/> sobre um cronograma que
    /// pertence ao processo em edição.
    /// </summary>
    public static IReadOnlyList<Guid> Esperados(
        IReadOnlyCollection<FaseCronograma> fases,
        IReadOnlyCollection<EtapaProcesso> etapas,
        Guid definitivoId)
    {
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(etapas);

        return fases.FirstOrDefault(f => f.Produtos.Any(p => p.Id == definitivoId)) is { } fase
            ? PreliminaresDoLugar(fase.Produtos.Select(static p => (p.Id, p.AtoCodigo, p.Papel)), definitivoId)
            : EsperadosNaEtapa(etapas, definitivoId);
    }

    private static IReadOnlyList<Guid> EsperadosNaEtapa(IReadOnlyCollection<EtapaProcesso> etapas, Guid definitivoId) =>
        etapas.FirstOrDefault(e => e.Produtos.Any(p => p.Id == definitivoId)) is { } etapa
            ? PreliminaresDoLugar(etapa.Produtos.Select(static p => (p.Id, p.AtoCodigo, p.Papel)), definitivoId)
            : [];

    private static IReadOnlyList<Guid> PreliminaresDoLugar(IEnumerable<(Guid Id, string AtoCodigo, PapelProdutoFase? Papel)> produtos, Guid definitivoId)
    {
        (Guid Id, string AtoCodigo, PapelProdutoFase? Papel)[] doLugar = [.. produtos];

        return CicloRecursal.PreliminaresDoTipo(doLugar, doLugar.First(p => p.Id == definitivoId).AtoCodigo);
    }

    /// <summary>
    /// Os identificadores dos produtos definitivos que fecham o ciclo do preliminar
    /// <paramref name="preliminarId"/>, ou lista vazia quando nenhum definitivo o espera. É a pergunta
    /// inversa de <see cref="Esperados"/>, respondida por ela, para que o pareamento continue num só lugar.
    /// </summary>
    public static IReadOnlyList<Guid> DefinitivosQueFecham(ConfiguracaoPublicada configuracao, Guid preliminarId)
    {
        ArgumentNullException.ThrowIfNull(configuracao);

        return DefinitivosQueFecham(configuracao.Cronograma.CronogramaFases, configuracao.Avaliacao.Etapas, preliminarId);
    }

    /// <summary>
    /// A mesma pergunta de <see cref="DefinitivosQueFecham(ConfiguracaoPublicada, Guid)"/> sobre um
    /// cronograma do processo em edição.
    /// </summary>
    public static IReadOnlyList<Guid> DefinitivosQueFecham(
        IReadOnlyCollection<FaseCronograma> fases,
        IReadOnlyCollection<EtapaProcesso> etapas,
        Guid preliminarId)
    {
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(etapas);

        return
        [
            .. fases.SelectMany(static f => f.Produtos.Select(static p => (p.Id, p.Papel)))
                .Concat(etapas.SelectMany(static e => e.Produtos.Select(static p => (p.Id, p.Papel))))
                .Where(static p => p.Papel == PapelProdutoFase.Definitivo)
                .Select(static p => p.Id)
                .Where(definitivoId => Esperados(fases, etapas, definitivoId).Contains(preliminarId))
                .Distinct(),
        ];
    }
}
