namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProdutosPrevistos;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Os atos do processo agrupados pelo produto do edital que cumprem: o ato que o publicou pela
/// primeira vez e a sequência de atos que o corrigem.
/// </summary>
/// <remarks>
/// A primeira publicação é o ato do produto que não corrige outro; a correção de uma lista
/// publicada é um novo ato ligado ao anterior (ADR-0103), e por isso não disputa o lugar da
/// primeira. Que a correção carregue ou não o produto não muda a cadeia: ela se segue pela relação
/// entre os atos.
/// </remarks>
internal sealed class CadeiasDosProdutosPrevistos
{
    private readonly Dictionary<Guid, AtoDoProcesso> _primeiraPublicacao;
    private readonly ILookup<Guid, AtoDoProcesso> _corrigidosPor;

    private CadeiasDosProdutosPrevistos(Dictionary<Guid, AtoDoProcesso> primeiraPublicacao, ILookup<Guid, AtoDoProcesso> corrigidosPor)
    {
        _primeiraPublicacao = primeiraPublicacao;
        _corrigidosPor = corrigidosPor;
    }

    public static CadeiasDosProdutosPrevistos De(IEnumerable<AtoDoProcesso> atos)
    {
        ArgumentNullException.ThrowIfNull(atos);

        AtoDoProcesso[] doProcesso = [.. atos];
        Dictionary<Guid, AtoDoProcesso> primeiras = doProcesso
            .Where(static a => a.ProdutoDoEditalId is not null && a.AtoCorrigidoId is null)
            .GroupBy(static a => a.ProdutoDoEditalId!.Value)
            .ToDictionary(
                static g => g.Key,
                static g => g.OrderBy(static a => a.CreatedAt).ThenBy(static a => a.Id).First());
        ILookup<Guid, AtoDoProcesso> corrigidosPor = doProcesso
            .Where(static a => a.AtoCorrigidoId is not null)
            .ToLookup(static a => a.AtoCorrigidoId!.Value);

        return new CadeiasDosProdutosPrevistos(primeiras, corrigidosPor);
    }

    /// <summary>O ato que publicou o produto pela primeira vez; nulo enquanto ele está aberto.</summary>
    public AtoDoProcesso? PrimeiraPublicacaoDe(Guid produtoId) =>
        _primeiraPublicacao.GetValueOrDefault(produtoId);

    /// <summary>Os atos que corrigem a primeira publicação, do mais antigo para o mais recente.</summary>
    public IReadOnlyList<AtoDoProcesso> CorrecoesDe(AtoDoProcesso primeiraPublicacao)
    {
        ArgumentNullException.ThrowIfNull(primeiraPublicacao);

        // A cadeia é linear, mas o guarda contra o ciclo mantém a consulta finita se o dado for corrompido.
        HashSet<Guid> vistos = [primeiraPublicacao.Id];
        List<AtoDoProcesso> correcoes = [];
        AtoDoProcesso? atual = primeiraPublicacao;
        while (atual is not null)
        {
            atual = _corrigidosPor[atual.Id]
                .Where(a => !vistos.Contains(a.Id))
                .OrderBy(static a => a.CreatedAt)
                .ThenBy(static a => a.Id)
                .FirstOrDefault();
            if (atual is not null)
            {
                vistos.Add(atual.Id);
                correcoes.Add(atual);
            }
        }

        return correcoes;
    }

    /// <summary>A primeira publicação e as correções dos produtos dados.</summary>
    public IEnumerable<AtoDoProcesso> AtosDe(IEnumerable<Guid> produtoIds)
    {
        ArgumentNullException.ThrowIfNull(produtoIds);

        foreach (Guid produtoId in produtoIds.Distinct())
        {
            if (PrimeiraPublicacaoDe(produtoId) is not { } primeira)
            {
                continue;
            }

            yield return primeira;
            foreach (AtoDoProcesso correcao in CorrecoesDe(primeira))
            {
                yield return correcao;
            }
        }
    }
}
