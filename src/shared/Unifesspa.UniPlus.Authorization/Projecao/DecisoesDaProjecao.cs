namespace Unifesspa.UniPlus.Authorization.Projecao;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Quem responde se um par (natureza, classificação) pode sair na resposta.</summary>
public interface IPermissoesDaProjecao
{
    /// <summary>Se a permissão do par foi concedida. A classificação que não se resolve vale como sensível.</summary>
    bool Permite(NaturezaDoItem natureza, ClassificacaoDeProtecao classificacao);
}

/// <summary>
/// As decisões permitidas que o projetor tomou sobre um resource: por par (natureza, classificação), a decisão que o
/// concedeu. A negativa não fica guardada: o par sem decisão permitida é omitido (ADR-0153).
/// </summary>
public sealed class DecisoesDaProjecao : IPermissoesDaProjecao
{
    private readonly IReadOnlyDictionary<(NaturezaDoItem, ClassificacaoDeProtecao), DecisaoPermitida> _permitidas;

    /// <summary>Nenhum par permitido: nada sai.</summary>
    public static DecisoesDaProjecao Vazia { get; } = new(new Dictionary<(NaturezaDoItem, ClassificacaoDeProtecao), DecisaoPermitida>());

    /// <summary>Cria as decisões a partir das permitidas por par.</summary>
    public DecisoesDaProjecao(IReadOnlyDictionary<(NaturezaDoItem, ClassificacaoDeProtecao), DecisaoPermitida> permitidas)
    {
        ArgumentNullException.ThrowIfNull(permitidas);

        _permitidas = permitidas;
    }

    /// <summary>As decisões permitidas, uma por par.</summary>
    public IReadOnlyCollection<DecisaoPermitida> Permitidas => [.. _permitidas.Values];

    /// <inheritdoc />
    public bool Permite(NaturezaDoItem natureza, ClassificacaoDeProtecao classificacao) =>
        _permitidas.ContainsKey((natureza, ClassificacoesDeProtecao.Resolvida(classificacao)));

    /// <summary>A decisão permitida do par, ou <see langword="null"/> quando o par é omitido.</summary>
    public DecisaoPermitida? DecisaoDo(NaturezaDoItem natureza, ClassificacaoDeProtecao classificacao) =>
        _permitidas.GetValueOrDefault((natureza, ClassificacoesDeProtecao.Resolvida(classificacao)));

    /// <summary>As decisões deste conjunto e as do outro; no mesmo par vale a deste.</summary>
    public DecisoesDaProjecao Unir(DecisoesDaProjecao outra)
    {
        ArgumentNullException.ThrowIfNull(outra);

        Dictionary<(NaturezaDoItem, ClassificacaoDeProtecao), DecisaoPermitida> uniao = new(outra._permitidas);
        foreach (KeyValuePair<(NaturezaDoItem, ClassificacaoDeProtecao), DecisaoPermitida> par in _permitidas)
        {
            uniao[par.Key] = par.Value;
        }

        return new DecisoesDaProjecao(uniao);
    }
}

/// <summary>A regra do item e da propriedade sobre a decisão do par.</summary>
public static class PermissoesDaProjecaoExtensions
{
    /// <summary>
    /// Se a propriedade do DTO pode sair: o par que o <see cref="ProtegidoComoAttribute"/> dela declara foi permitido. É a
    /// consulta que o handler faz antes de carregar o valor e ao preenchê-lo, de modo que a classe de cada campo fixo fica
    /// declarada só no DTO.
    /// </summary>
    /// <typeparam name="TDto">O DTO que declara a propriedade.</typeparam>
    /// <param name="permissoes">As decisões do projetor.</param>
    /// <param name="propriedade">O nome da propriedade, por <c>nameof</c>.</param>
    public static bool PermitePropriedade<TDto>(this IPermissoesDaProjecao permissoes, string propriedade)
    {
        ArgumentNullException.ThrowIfNull(permissoes);

        ProtegidoComoAttribute declaracao = ProtecaoDeclaradaNoDto.Da<TDto>(propriedade);

        return permissoes.Permite(declaracao.Natureza, declaracao.Classificacao);
    }

    /// <summary>
    /// Se o item sai: o par dele foi permitido e, sendo sensível, tem base legal congelada. O item sensível sem base
    /// legal não sai, mesmo para quem tem a permissão.
    /// </summary>
    public static bool Permite(this IPermissoesDaProjecao permissoes, IItemClassificado item)
    {
        ArgumentNullException.ThrowIfNull(permissoes);
        ArgumentNullException.ThrowIfNull(item);

        ClassificacaoDeProtecao classificacao = ClassificacoesDeProtecao.Resolvida(item.Classificacao);

        return permissoes.Permite(item.Natureza, classificacao)
            && (classificacao != ClassificacaoDeProtecao.Sensivel || !string.IsNullOrWhiteSpace(item.BaseLegal));
    }
}
