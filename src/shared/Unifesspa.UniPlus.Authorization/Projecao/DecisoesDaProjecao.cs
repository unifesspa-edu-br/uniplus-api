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
    private readonly IReadOnlyDictionary<(NaturezaDoItem, ClassificacaoDeProtecao), DecisaoDeAcesso> _permitidas;

    /// <summary>Nenhum par permitido: nada sai.</summary>
    public static DecisoesDaProjecao Vazia { get; } = new(new Dictionary<(NaturezaDoItem, ClassificacaoDeProtecao), DecisaoDeAcesso>());

    /// <summary>Cria as decisões a partir das permitidas por par.</summary>
    public DecisoesDaProjecao(IReadOnlyDictionary<(NaturezaDoItem, ClassificacaoDeProtecao), DecisaoDeAcesso> permitidas)
    {
        ArgumentNullException.ThrowIfNull(permitidas);

        _permitidas = permitidas;
    }

    /// <summary>As decisões permitidas, uma por par.</summary>
    public IReadOnlyCollection<DecisaoDeAcesso> Permitidas => [.. _permitidas.Values];

    /// <inheritdoc />
    public bool Permite(NaturezaDoItem natureza, ClassificacaoDeProtecao classificacao) =>
        _permitidas.ContainsKey((natureza, Resolvida(classificacao)));

    /// <summary>A decisão permitida do par, ou <see langword="null"/> quando o par é omitido.</summary>
    public DecisaoDeAcesso? DecisaoDo(NaturezaDoItem natureza, ClassificacaoDeProtecao classificacao) =>
        _permitidas.GetValueOrDefault((natureza, Resolvida(classificacao)));

    /// <summary>As decisões deste conjunto e as do outro; no mesmo par vale a deste.</summary>
    public DecisoesDaProjecao Unir(DecisoesDaProjecao outra)
    {
        ArgumentNullException.ThrowIfNull(outra);

        Dictionary<(NaturezaDoItem, ClassificacaoDeProtecao), DecisaoDeAcesso> uniao = new(outra._permitidas);
        foreach (KeyValuePair<(NaturezaDoItem, ClassificacaoDeProtecao), DecisaoDeAcesso> par in _permitidas)
        {
            uniao[par.Key] = par.Value;
        }

        return new DecisoesDaProjecao(uniao);
    }

    /// <summary>A classificação que não se resolve vale como sensível: na dúvida, a permissão mais restrita.</summary>
    internal static ClassificacaoDeProtecao Resolvida(ClassificacaoDeProtecao classificacao) =>
        classificacao == ClassificacaoDeProtecao.Nenhuma || !Enum.IsDefined(classificacao)
            ? ClassificacaoDeProtecao.Sensivel
            : classificacao;
}

/// <summary>A regra do item sobre a decisão do par.</summary>
public static class PermissoesDaProjecaoExtensions
{
    /// <summary>
    /// Se o item sai: o par dele foi permitido e, sendo sensível, tem base legal congelada. O item sensível sem base
    /// legal não sai, mesmo para quem tem a permissão.
    /// </summary>
    public static bool Permite(this IPermissoesDaProjecao permissoes, IItemClassificado item)
    {
        ArgumentNullException.ThrowIfNull(permissoes);
        ArgumentNullException.ThrowIfNull(item);

        ClassificacaoDeProtecao classificacao = DecisoesDaProjecao.Resolvida(item.Classificacao);

        return permissoes.Permite(item.Natureza, classificacao)
            && (classificacao != ClassificacaoDeProtecao.Sensivel || !string.IsNullOrWhiteSpace(item.BaseLegal));
    }
}
