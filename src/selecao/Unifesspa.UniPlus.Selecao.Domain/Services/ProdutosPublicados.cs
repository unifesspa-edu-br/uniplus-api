namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

/// <summary>
/// Os produtos que chegam substituem os produtos da fase ou da etapa sem trocar a identidade do que
/// continua sendo a mesma publicação: a instância rastreada sobrevive, com o <c>Id</c> e o
/// <c>CreatedAt</c> que já tinha, e só o que de fato mudou sai ou entra. Recriar a linha giraria o
/// <c>Id</c>, que entra nos bytes canônicos e é a referência do produto âncora do recurso.
/// </summary>
/// <remarks>
/// A comparação do ato é de quem chama, porque a fase compara o código como foi escrito e a etapa o
/// compara em NFC, como o documento canônico. O papel nunca é normalizado: é um valor do domínio.
/// </remarks>
internal static class ProdutosPublicados
{
    /// <summary>
    /// O primeiro produto do primeiro par ato e papel declarado mais de uma vez, ou
    /// <see langword="null"/>. Quando mais de um par se repete, vence o que tem a primeira
    /// ocorrência antes na lista.
    /// </summary>
    internal static T? PrimeiroParRepetido<T>(IEnumerable<T> produtos, Func<string, string> chaveDoAto)
        where T : class, IProdutoPublicado =>
        produtos
            .GroupBy(produto => (Ato: chaveDoAto(produto.AtoCodigo), produto.Papel))
            .FirstOrDefault(static grupo => grupo.Count() > 1)?.First();

    /// <summary>
    /// Reconcilia numa passada, pelo par exato ato e papel. Para cada produto que chega, em ordem
    /// de chegada, reaproveita a primeira linha viva ainda não consumida com o mesmo par; sem par, o
    /// que chegou entra como produto novo. A linha viva que ninguém consumiu sai, e a troca de papel
    /// é saída da antiga e entrada de um produto novo.
    /// </summary>
    internal static ProdutosReconciliados<T> ReconciliarPreservandoIdentidade<T>(
        IEnumerable<T> vivos, IEnumerable<T> chegados, Func<string, string> chaveDoAto)
        where T : class, IProdutoPublicado
    {
        List<T> disponiveis = [.. vivos];
        T[] quemChegou = [.. chegados];

        return Montar(quemChegou, CasarPeloPar(disponiveis, quemChegou, chaveDoAto));
    }

    /// <summary>
    /// Reconcilia em duas passadas, para quando a edição pode ter trocado só o papel. A primeira
    /// casa o par exato, como em <see cref="ReconciliarPreservandoIdentidade{T}"/>. A segunda, só
    /// para quem chegou sem par exato, procura entre as linhas vivas ainda não consumidas uma do
    /// mesmo ato e a reaproveita atualizando o papel: sem ela, a linha sairia como órfã e voltaria
    /// como inserção, DELETE e INSERT disputando o mesmo slot do índice único na mesma transação.
    /// O par exato tem prioridade sobre o ato, e cada linha viva é consumida no máximo uma vez, senão
    /// a preliminar e a definitiva da mesma matéria casariam com a mesma linha.
    /// </summary>
    internal static ProdutosReconciliados<T> ReconciliarPreservandoIdentidadeMesmoComTrocaDePapel<T>(
        IEnumerable<T> vivos, IEnumerable<T> chegados, Func<string, string> chaveDoAto)
        where T : class, IProdutoPublicado
    {
        List<T> disponiveis = [.. vivos];
        T[] quemChegou = [.. chegados];
        T?[] casados = CasarPeloPar(disponiveis, quemChegou, chaveDoAto);
        CasarPeloAtoAtualizandoOPapel(disponiveis, quemChegou, casados, chaveDoAto);

        return Montar(quemChegou, casados);
    }

    private static T?[] CasarPeloPar<T>(List<T> disponiveis, T[] chegados, Func<string, string> chaveDoAto)
        where T : class, IProdutoPublicado
    {
        T?[] casados = new T?[chegados.Length];
        for (int posicao = 0; posicao < chegados.Length; posicao++)
        {
            T chegado = chegados[posicao];
            casados[posicao] = TomarPrimeira(disponiveis, viva =>
                MesmoAto(viva, chegado, chaveDoAto) && viva.Papel == chegado.Papel);
        }

        return casados;
    }

    private static void CasarPeloAtoAtualizandoOPapel<T>(
        List<T> disponiveis, T[] chegados, T?[] casados, Func<string, string> chaveDoAto)
        where T : class, IProdutoPublicado
    {
        for (int posicao = 0; posicao < chegados.Length; posicao++)
        {
            T chegado = chegados[posicao];
            if (casados[posicao] is not null)
            {
                continue;
            }

            T? viva = TomarPrimeira(disponiveis, candidata => MesmoAto(candidata, chegado, chaveDoAto));
            viva?.AtualizarPapel(chegado.Papel);
            casados[posicao] = viva;
        }
    }

    private static ProdutosReconciliados<T> Montar<T>(T[] chegados, T?[] casados)
        where T : class, IProdutoPublicado
    {
        T[] finais = [.. chegados.Select((chegado, posicao) => casados[posicao] ?? chegado)];
        Dictionary<Guid, Guid> identidadeViva = [];
        for (int posicao = 0; posicao < chegados.Length; posicao++)
        {
            identidadeViva[chegados[posicao].Id] = finais[posicao].Id;
        }

        return new ProdutosReconciliados<T>(finais, identidadeViva);
    }

    private static bool MesmoAto<T>(T viva, T chegado, Func<string, string> chaveDoAto)
        where T : IProdutoPublicado =>
        string.Equals(chaveDoAto(viva.AtoCodigo), chaveDoAto(chegado.AtoCodigo), StringComparison.Ordinal);

    private static T? TomarPrimeira<T>(List<T> disponiveis, Predicate<T> criterio)
        where T : class
    {
        int posicao = disponiveis.FindIndex(criterio);
        if (posicao < 0)
        {
            return null;
        }

        T tomada = disponiveis[posicao];
        disponiveis.RemoveAt(posicao);

        return tomada;
    }
}
