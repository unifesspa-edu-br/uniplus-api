namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// As janelas recursais que a etapa abre: onde cada uma se ancora, quando duas disputam o mesmo fato
/// recorrível e como a restauração da versão congelada as reconcilia com as linhas vivas.
/// </summary>
internal static class JanelasRecursaisDaEtapa
{
    /// <summary>
    /// A primeira violação da âncora das janelas, ou <see langword="null"/> quando todas se ancoram onde
    /// podem: a âncora em ato referencia um produto preliminar da etapa, e a âncora em ciência exige a
    /// promessa de parecer individual, porque é dele que o prazo corre. É o predicado que a gravação das
    /// janelas e o gate de publicação chamam: o estado carregado do banco não passa pela gravação, e só o
    /// gate o alcança.
    /// </summary>
    internal static DomainError? ViolacaoDaAncora(EtapaProcesso etapa, IEnumerable<RecursoDaEtapa> recursos)
    {
        HashSet<Guid> preliminares =
            [.. etapa.Produtos.Where(static p => p.Papel == PapelProdutoFase.Preliminar).Select(static p => p.Id)];

        return recursos.Select(recurso => ViolacaoDaJanela(etapa, recurso, preliminares)).FirstOrDefault(static v => v is not null);
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
                "Cada fato recorrível abre uma janela só: dois prazos para a mesma publicação, ou dois para a ciência, seriam ambíguos.");
    }

    /// <summary>
    /// Repõe as janelas pelo Id da âncora traduzido para o que o produto tem depois da reconciliação:
    /// sem isso a âncora apontaria para uma linha que a mesma transação remove como órfã, e o sintoma só
    /// apareceria muito depois, ao resolver o recurso de um ato publicado. Aqui não há índice único além
    /// da própria chave primária, e é ELA que colidiria se a mesma janela saísse e voltasse na mesma
    /// transação. Cada janela viva é consumida no máximo uma vez.
    /// </summary>
    /// <returns>As janelas que ficam, na ordem das congeladas: a viva reaproveitada ou a própria congelada.</returns>
    internal static List<RecursoDaEtapa> Repor(
        IEnumerable<RecursoDaEtapa> vivos,
        IEnumerable<RecursoDaEtapa> congelados,
        IReadOnlyDictionary<Guid, Guid> produtoVivoPorCongelado)
    {
        List<RecursoDaEtapa> disponiveis = [.. vivos];

        return [.. congelados.Select(congelado => ReporPelaAncora(disponiveis, congelado, produtoVivoPorCongelado))];
    }

    // Na etapa de nota do ENEM o recurso em ato não cabe de modo algum, e a regra do processo diz isso
    // nomeando o caminho certo (a regra de recurso da fase). Pedir aqui um preliminar que ela depois
    // recusa mandaria o operador na direção errada.
    private static DomainError? ViolacaoDaJanela(EtapaProcesso etapa, RecursoDaEtapa recurso, HashSet<Guid> preliminares)
    {
        if (recurso.Ancora == AncoraDoRecurso.AtoPublicado && !etapa.DeclaraNotaDoEnem && !preliminares.Contains(recurso.ProdutoAncoraId))
        {
            return new DomainError(
                "EtapaProcesso.AncoraNaoEhProdutoPreliminarDaEtapa",
                "O recurso contra ato publicado ancora num resultado preliminar desta etapa — é a publicação dele que abre a janela.");
        }

        return recurso.Ancora == AncoraDoRecurso.CienciaIndividual && !etapa.EmiteParecerIndividual
            ? new DomainError(
                "EtapaProcesso.CienciaSemParecerIndividual",
                "O prazo que corre da ciência do candidato exige que a etapa prometa parecer individual: é dele que o candidato toma ciência.")
            : null;
    }

    private static string FatoRecorrivel(RecursoDaEtapa recurso) =>
        recurso.Ancora == AncoraDoRecurso.AtoPublicado ? $"ato:{recurso.ProdutoAncoraId}" : "ciencia";

    // Casa pela ÂNCORA, que é o que identifica a janela no negócio: o envelope não congela o id do
    // recurso, e casar por ele nunca acertaria, com a linha viva saindo e voltando a cada restauração,
    // trocando de identidade sem que nada tivesse mudado.
    private static RecursoDaEtapa ReporPelaAncora(
        List<RecursoDaEtapa> disponiveis, RecursoDaEtapa congelado, IReadOnlyDictionary<Guid, Guid> produtoVivoPorCongelado)
    {
        Guid ancora = produtoVivoPorCongelado.GetValueOrDefault(congelado.ProdutoAncoraId, congelado.ProdutoAncoraId);
        RecursoDaEtapa? viva = disponiveis.Find(r => r.Ancora == congelado.Ancora && r.ProdutoAncoraId == ancora);
        if (viva is not null)
        {
            disponiveis.Remove(viva);
        }

        RecursoDaEtapa destino = viva ?? congelado;
        destino.ReporDadosCongelados(congelado.Ancora, congelado.Regra, congelado.Args, ancora, congelado.Procedimentos);

        return destino;
    }
}
