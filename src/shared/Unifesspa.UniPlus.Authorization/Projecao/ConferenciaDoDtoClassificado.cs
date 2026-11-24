namespace Unifesspa.UniPlus.Authorization.Projecao;

using System.Reflection;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Confere os DTOs administrativos contra o mapa coluna → classificação do modelo (ADR-0153): a propriedade lida direto da
/// coluna não pode ficar abaixo da classificação dela, e o valor derivado declara a transformação que autoriza a
/// classificação própria. Falha fechada: fonte que o modelo não conhece reprova.
/// </summary>
public static class ConferenciaDoDtoClassificado
{
    /// <summary>As violações, cada uma nomeando o tipo e a propriedade que as causam.</summary>
    /// <param name="tipos">Os tipos a conferir; os que não declaram classificação em propriedade alguma são ignorados.</param>
    /// <param name="classificacoes">Por <c>schema.tabela.coluna</c>, a classificação que o modelo resolve.</param>
    public static IReadOnlyList<string> Conferir(
        IEnumerable<Type> tipos, IReadOnlyDictionary<string, ClassificacaoDeProtecao> classificacoes)
    {
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(classificacoes);

        return [.. tipos.SelectMany(tipo => tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(propriedade => ViolacaoDaPropriedade(tipo, propriedade, classificacoes)))
            .OfType<string>()];
    }

    private static string? ViolacaoDaPropriedade(
        Type tipo, PropertyInfo propriedade, IReadOnlyDictionary<string, ClassificacaoDeProtecao> classificacoes)
    {
        if (propriedade.GetCustomAttribute<ProtegidoComoAttribute>() is not { } declaracao)
        {
            return null;
        }

        string quem = $"{tipo.Name}.{propriedade.Name}";
        return ViolacaoDaDeclaracao(quem, declaracao) ?? ViolacaoContraAFonte(quem, declaracao, classificacoes);
    }

    private static string? ViolacaoDaDeclaracao(string quem, ProtegidoComoAttribute declaracao)
    {
        if (!Enum.IsDefined(declaracao.Classificacao))
        {
            return $"{quem}: a classificação declarada não é uma classe de proteção.";
        }

        return declaracao.Fonte is null && declaracao.Transformacao is not null
            ? $"{quem}: declara a transformação '{declaracao.Transformacao}' sem a fonte que ela transforma."
            : null;
    }

    private static string? ViolacaoContraAFonte(
        string quem, ProtegidoComoAttribute declaracao, IReadOnlyDictionary<string, ClassificacaoDeProtecao> classificacoes)
    {
        if (declaracao.Fonte is null)
        {
            return null;
        }

        if (!classificacoes.TryGetValue(declaracao.Fonte, out ClassificacaoDeProtecao daFonte))
        {
            return $"{quem}: a fonte '{declaracao.Fonte}' não é coluna do modelo.";
        }

        return declaracao.Classificacao < daFonte && string.IsNullOrWhiteSpace(declaracao.Transformacao)
            ? $"{quem}: lida direto de '{declaracao.Fonte}' ({daFonte}), não pode ficar em {declaracao.Classificacao}; "
                + "classifique-a pela fonte ou declare a transformação que a deriva."
            : null;
    }
}
