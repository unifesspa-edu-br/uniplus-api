namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// As janelas recursais que a etapa abre: onde cada uma se ancora, quando duas disputam o mesmo fato
/// recorrível e como a reidratação da versão congelada as reconcilia com as linhas vivas.
/// </summary>
internal static class JanelasRecursaisDaEtapa
{
    internal const string AncoraForaDosPreliminares = "EtapaProcesso.AncoraNaoEhProdutoPreliminarDaEtapa";
    internal const string CienciaSemParecerIndividual = "EtapaProcesso.CienciaSemParecerIndividual";

    /// <summary>Os códigos das recusas da âncora das janelas, para quem projeta a âncora num item só.</summary>
    internal static readonly IReadOnlySet<string> CodigosDaAncora =
        new HashSet<string>([AncoraForaDosPreliminares, CienciaSemParecerIndividual], StringComparer.Ordinal);

    /// <summary>
    /// A recusa das janelas recursais declaradas: a primeira violação da âncora ou, não havendo, o fato
    /// recorrível que duas janelas disputam. É o predicado que a gravação das janelas e a declaração da
    /// etapa chamam.
    /// </summary>
    internal static DomainError? Recusa(
        IEnumerable<ProdutoDaEtapa> produtos,
        bool notaVemDoEnem,
        bool emiteParecerIndividual,
        IEnumerable<RecursoDaEtapa> recursos) =>
        ViolacaoDaAncora(produtos, notaVemDoEnem, emiteParecerIndividual, recursos) ?? FatoRepetido(recursos);

    /// <summary>
    /// A primeira violação da âncora das janelas, ou <see langword="null"/> quando todas se ancoram onde
    /// podem: a âncora em ato referencia um produto preliminar da etapa, e a âncora em ciência exige a
    /// promessa de parecer individual, porque é dele que o prazo corre. É o predicado que a gravação das
    /// janelas e o gate de publicação chamam: o estado carregado do banco não passa pela gravação, e só o
    /// gate o alcança.
    /// </summary>
    internal static DomainError? ViolacaoDaAncora(EtapaProcesso etapa, IEnumerable<RecursoDaEtapa> recursos) =>
        ViolacaoDaAncora(etapa.Produtos, etapa.DeclaraNotaDoEnem, etapa.EmiteParecerIndividual, recursos);

    internal static DomainError? ViolacaoDaAncora(
        IEnumerable<ProdutoDaEtapa> produtos,
        bool notaVemDoEnem,
        bool emiteParecerIndividual,
        IEnumerable<RecursoDaEtapa> recursos)
    {
        HashSet<Guid> preliminares =
            [.. produtos.Where(static p => p.Papel == PapelProdutoFase.Preliminar).Select(static p => p.Id)];

        return recursos
            .Select(recurso => ViolacaoDaJanela(recurso, preliminares, notaVemDoEnem, emiteParecerIndividual))
            .FirstOrDefault(static v => v is not null);
    }

    /// <summary>
    /// O erro de duas janelas para o mesmo fato, ou <see langword="null"/>: dois prazos para a mesma
    /// publicação, ou dois para a ciência, seriam ambíguos.
    /// </summary>
    internal static DomainError? FatoRepetido(IEnumerable<RecursoDaEtapa> recursos)
    {
        List<string> fatos = [.. recursos.Select(FatoRecorrivel)];

        return fatos.Distinct(StringComparer.Ordinal).Count() == fatos.Count
            ? null
            : new DomainError(
                "EtapaProcesso.RecursoDuplicadoNaEtapa",
                "Cada fato recorrível abre um prazo só: dois prazos para a mesma publicação, ou dois para a ciência, seriam ambíguos.");
    }

    // Na etapa de nota do ENEM o recurso em ato não cabe de modo algum, e a regra do processo diz isso
    // nomeando o caminho certo (a regra de recurso da fase). Pedir aqui um preliminar que ela depois
    // recusa mandaria o operador na direção errada.
    private static DomainError? ViolacaoDaJanela(
        RecursoDaEtapa recurso, HashSet<Guid> preliminares, bool notaVemDoEnem, bool emiteParecerIndividual)
    {
        if (recurso.Ancora == AncoraDoRecurso.AtoPublicado && !notaVemDoEnem && !preliminares.Contains(recurso.ProdutoAncoraId))
        {
            return new DomainError(
                AncoraForaDosPreliminares,
                "O prazo do recurso contra ato publicado começa na publicação de um resultado preliminar desta etapa. Indique um resultado preliminar publicado por esta etapa.");
        }

        return recurso.Ancora == AncoraDoRecurso.CienciaIndividual && !emiteParecerIndividual
            ? new DomainError(
                CienciaSemParecerIndividual,
                "O prazo que corre da ciência do candidato exige que a etapa prometa parecer individual: é dele que o candidato toma ciência.")
            : null;
    }

    private static string FatoRecorrivel(RecursoDaEtapa recurso) =>
        recurso.Ancora == AncoraDoRecurso.AtoPublicado ? $"ato:{recurso.ProdutoAncoraId}" : "ciencia";
}
