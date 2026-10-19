namespace Unifesspa.UniPlus.Selecao.Application.Services;

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
/// definitivo espera os preliminares do mesmo tipo de ato declarados no mesmo lugar (a fase ou a
/// etapa) e, quando o lugar é uma fase, os declarados pelas fases que a apontam como concluinte e
/// deixam aberto o ciclo desse tipo (sem o definitivo do mesmo tipo na própria fase). Duas fases que abrem ciclo com o mesmo tipo e apontam a mesma concluinte
/// fazem o definitivo da concluinte esperar os dois preliminares.
/// </para>
/// <para>
/// A consulta dos itens previstos e a publicação de resultado perguntam aqui; nenhuma delas
/// reimplementa o pareamento. Produto sem preliminar declarado para a matéria não espera nada.
/// </para>
/// </remarks>
internal static class PreliminaresDaMateria
{
    /// <summary>
    /// Os identificadores dos produtos preliminares que o definitivo <paramref name="definitivoId"/>
    /// espera, ou lista vazia quando o cronograma não declara preliminar para a matéria dele.
    /// </summary>
    public static IReadOnlyList<Guid> Esperados(GrafoConfiguracao grafo, Guid definitivoId)
    {
        ArgumentNullException.ThrowIfNull(grafo);

        return Esperados(grafo.CronogramaFases, grafo.Etapas, definitivoId);
    }

    /// <summary>
    /// A mesma pergunta de <see cref="Esperados(GrafoConfiguracao, Guid)"/> sobre um cronograma que
    /// ainda não virou grafo, como o do processo em edição.
    /// </summary>
    public static IReadOnlyList<Guid> Esperados(
        IReadOnlyCollection<FaseCronograma> fases,
        IReadOnlyCollection<EtapaProcesso> etapas,
        Guid definitivoId)
    {
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(etapas);

        FaseCronograma? fase = fases.FirstOrDefault(f => f.Produtos.Any(p => p.Id == definitivoId));
        if (fase is not null)
        {
            string atoCodigo = fase.Produtos.First(p => p.Id == definitivoId).AtoCodigo;
            IEnumerable<Guid> daFase = CicloRecursal.PreliminaresDoTipo(fase.Produtos.Select(static p => (p.Id, p.AtoCodigo, p.Papel)), atoCodigo);

            // Só chega à concluinte o ciclo que a fase de origem deixa aberto: a matéria que ela
            // fecha com o próprio definitivo não espera a concluinte.
            IEnumerable<Guid> deOutrasFases = fases
                .Where(f => f.Id != fase.Id
                    && string.Equals(f.FaseConcluinteCodigo, fase.Codigo, StringComparison.Ordinal)
                    && f.TiposComCicloAberto().Contains(atoCodigo, StringComparer.Ordinal))
                .SelectMany(f => CicloRecursal.PreliminaresDoTipo(f.Produtos.Select(static p => (p.Id, p.AtoCodigo, p.Papel)), atoCodigo));

            return [.. daFase, .. deOutrasFases];
        }

        EtapaProcesso? etapa = etapas.FirstOrDefault(e => e.Produtos.Any(p => p.Id == definitivoId));
        if (etapa is null)
        {
            return [];
        }

        string atoDaEtapa = etapa.Produtos.First(p => p.Id == definitivoId).AtoCodigo;

        return [.. CicloRecursal.PreliminaresDoTipo(etapa.Produtos.Select(static p => (p.Id, p.AtoCodigo, p.Papel)), atoDaEtapa)];
    }

    /// <summary>
    /// Os identificadores dos produtos definitivos que fecham o ciclo do preliminar
    /// <paramref name="preliminarId"/>, ou lista vazia quando nenhum definitivo o espera. É a pergunta
    /// inversa de <see cref="Esperados"/>, respondida por ela, para que o pareamento continue num só lugar.
    /// </summary>
    public static IReadOnlyList<Guid> DefinitivosQueFecham(GrafoConfiguracao grafo, Guid preliminarId)
    {
        ArgumentNullException.ThrowIfNull(grafo);

        return DefinitivosQueFecham(grafo.CronogramaFases, grafo.Etapas, preliminarId);
    }

    /// <summary>
    /// A mesma pergunta de <see cref="DefinitivosQueFecham(GrafoConfiguracao, Guid)"/> sobre um
    /// cronograma que ainda não virou grafo.
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
