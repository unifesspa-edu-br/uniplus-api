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

        List<string> violacoes = [];
        foreach (Type tipo in tipos)
        {
            foreach (PropertyInfo propriedade in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (propriedade.GetCustomAttribute<ProtegidoComoAttribute>() is not { } declaracao)
                {
                    continue;
                }

                string quem = $"{tipo.Name}.{propriedade.Name}";
                if (!Enum.IsDefined(declaracao.Classificacao) || declaracao.Classificacao == ClassificacaoDeProtecao.Nenhuma)
                {
                    violacoes.Add($"{quem}: a classificação declarada não é uma classe de proteção.");
                    continue;
                }

                if (declaracao.Fonte is null)
                {
                    if (declaracao.Transformacao is not null)
                    {
                        violacoes.Add($"{quem}: declara a transformação '{declaracao.Transformacao}' sem a fonte que ela transforma.");
                    }

                    continue;
                }

                if (!classificacoes.TryGetValue(declaracao.Fonte, out ClassificacaoDeProtecao daFonte))
                {
                    violacoes.Add($"{quem}: a fonte '{declaracao.Fonte}' não é coluna do modelo.");
                    continue;
                }

                if (declaracao.Classificacao < daFonte && string.IsNullOrWhiteSpace(declaracao.Transformacao))
                {
                    violacoes.Add(
                        $"{quem}: lida direto de '{declaracao.Fonte}' ({daFonte}), não pode ficar em {declaracao.Classificacao}; "
                        + "classifique-a pela fonte ou declare a transformação que a deriva.");
                }
            }
        }

        return violacoes;
    }
}
