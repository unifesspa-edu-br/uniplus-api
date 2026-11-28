namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A retificação que muda a regra ou os dados de entrada de um resultado preliminar já publicado exige novo
/// preliminar, em novo ciclo, antes do definitivo (UNI-REQ-0157, UNI-REQ-0282).
/// </summary>
/// <remarks>
/// <para>
/// O preliminar elegível é o da versão publicada, já publicado, cujo ciclo segue aberto: nenhum dos definitivos
/// que o fecham foi publicado (o mesmo pareamento de <see cref="ConferenciaDoCicloRecursalAberto"/>). O sistema não
/// sabe o que é dado de entrada de cada tipo de ato; por isso o piso é calculado do documento
/// (<see cref="PisoDoCicloNovo"/>, sobre <see cref="EntradaDoPreliminar"/>) e o bloco da retificação declara os
/// ciclos novos, ao menos o piso, e pode declarar mais.
/// </para>
/// <para>
/// As recusas se acumulam (ADR-0125): primeiro o piso que não foi declarado e depois o declarado que não tem preliminar
/// publicado com ciclo aberto, cada grupo em ordem ordinal da chave da <see cref="IdentidadeDoItemDoEdital"/>. Os ciclos novos
/// são declarados no pedido da retificação, e não no documento da configuração: a recusa chega sem campo, e a mensagem diz o
/// item e a declaração que ela aponta.
/// </para>
/// </remarks>
public static class ConferenciaDoCicloNovoDaRetificacao
{
    /// <param name="fasesDaBase">As fases da versão publicada vigente.</param>
    /// <param name="etapasDaBase">As etapas da versão publicada vigente.</param>
    /// <param name="fasesNovas">As fases do cronograma que a retificação propõe.</param>
    /// <param name="etapasNovas">As etapas do cronograma que a retificação propõe.</param>
    /// <param name="declarados">Os itens que o bloco da retificação declara como novo ciclo.</param>
    /// <param name="fatos">O que a execução já fez, lido depois da trava.</param>
    /// <param name="nomesDaVersaoPublicada">Os nomes que a cópia de cada tipo de ato guarda na versão publicada, e não os do catálogo vigente.</param>
    public static ResultadoDoCicloNovo Conferir(
        IReadOnlyCollection<FaseCronograma> fasesDaBase,
        IReadOnlyCollection<EtapaProcesso> etapasDaBase,
        IReadOnlyCollection<FaseCronograma> fasesNovas,
        IReadOnlyCollection<EtapaProcesso> etapasNovas,
        IReadOnlySet<IdentidadeDoItemDoEdital> declarados,
        FatosDaExecucao fatos,
        NomesDosTiposDeAto nomesDaVersaoPublicada)
    {
        ArgumentNullException.ThrowIfNull(fasesDaBase);
        ArgumentNullException.ThrowIfNull(etapasDaBase);
        ArgumentNullException.ThrowIfNull(fasesNovas);
        ArgumentNullException.ThrowIfNull(etapasNovas);
        ArgumentNullException.ThrowIfNull(declarados);
        ArgumentNullException.ThrowIfNull(fatos);
        ArgumentNullException.ThrowIfNull(nomesDaVersaoPublicada);

        HashSet<IdentidadeDoItemDoEdital> abertos = PreliminaresComCicloAberto(fasesDaBase, etapasDaBase, fatos);
        IEnumerable<IdentidadeDoItemDoEdital> piso = PisoDoCicloNovo.Preliminares(fasesDaBase, etapasDaBase, fasesNovas, etapasNovas, abertos);

        return new(
            [.. RecusasDoPisoNaoDeclarado(piso, declarados, nomesDaVersaoPublicada), .. RecusasDosDeclaradosSemCicloAberto(declarados, abertos)],
            [.. EmOrdem(declarados.Where(abertos.Contains))]);
    }

    private static HashSet<IdentidadeDoItemDoEdital> PreliminaresComCicloAberto(
        IReadOnlyCollection<FaseCronograma> fasesDaBase, IReadOnlyCollection<EtapaProcesso> etapasDaBase, FatosDaExecucao fatos) =>
        [.. ConferenciaDoCicloRecursalAberto.CiclosAbertos(fasesDaBase, etapasDaBase, fatos.ItensPublicados).Select(static ciclo => ciclo.Preliminar)];

    private static IEnumerable<FieldError> RecusasDoPisoNaoDeclarado(
        IEnumerable<IdentidadeDoItemDoEdital> piso, IReadOnlySet<IdentidadeDoItemDoEdital> declarados, NomesDosTiposDeAto nomes) =>
        EmOrdem(piso.Where(item => !declarados.Contains(item))).Select(item => new FieldError(
            null,
            new DomainError(
                RetificacaoErrorCodes.CicloNovoNaoDeclarado,
                $"A retificação muda a regra ou os dados de entrada {PreliminarCitado(nomes.NomeDe(item.AtoCodigo))}, que já foi publicado, "
                + "e exige declarar o novo ciclo no pedido: o novo preliminar é publicado antes do definitivo.")));

    private static string PreliminarCitado(string? nomeDoTipo) =>
        nomeDoTipo is null ? "de um resultado preliminar" : $"do resultado preliminar '{nomeDoTipo}'";

    private static IEnumerable<FieldError> RecusasDosDeclaradosSemCicloAberto(
        IReadOnlySet<IdentidadeDoItemDoEdital> declarados, HashSet<IdentidadeDoItemDoEdital> abertos) =>
        EmOrdem(declarados.Where(item => !abertos.Contains(item))).Select(static _ => new FieldError(
            null,
            new DomainError(
                RetificacaoErrorCodes.CicloNovoDeItemSemPreliminarAberto,
                "O ciclo novo declarado no pedido não tem resultado preliminar publicado com o ciclo aberto: "
                + "o preliminar não foi publicado, o definitivo já o fechou ou o item não existe na versão publicada. Retire o item dos ciclos novos.")));

    private static IOrderedEnumerable<IdentidadeDoItemDoEdital> EmOrdem(IEnumerable<IdentidadeDoItemDoEdital> itens) =>
        itens.OrderBy(static item => item.Chave, StringComparer.Ordinal);
}
