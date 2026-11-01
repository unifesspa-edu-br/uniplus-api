namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Protecao;

using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Confere que toda coluna mapeada do modelo resolve uma classificação de proteção e gera o retrato
/// do mapa coluna → classificação (ADR-0153). A conferência falha fechada: sem classificação resolvida,
/// ou com um mapeamento que ela não sabe conferir (tipo sem tabela, propriedade sem coluna, complex
/// property, tabela de fragmento), reprova — nunca ignora em silêncio.
/// </summary>
public static class ConferenciaDaClassificacaoDoModelo
{
    /// <summary>
    /// As violações, cada uma com a mensagem que nomeia a tabela, a coluna e o tipo que não declarou (a coluna sem
    /// classificação e o mapeamento que a conferência não sabe verificar), e o retrato do modelo: uma linha
    /// <c>tabela.coluna: Classificacao</c> por coluna, em ordem estável.
    /// </summary>
    public static (IReadOnlyList<string> Violacoes, string Retrato) Conferir(IModel modelo)
    {
        (SortedDictionary<string, ClassificacaoDeProtecao?> colunas, List<string> naoConferidos, Dictionary<string, SortedSet<string>> semDeclaracao) = Analisar(modelo);

        IReadOnlyList<string> violacoes =
        [
            .. naoConferidos,
            .. colunas
                .Where(c => c.Value is null)
                .Select(c => $"{c.Key}: sem classificação de proteção em {string.Join(", ", semDeclaracao[c.Key])}; declare o padrão do tipo (ClassificaPorPadrao) ou a coluna (ClassificaComo)."),
        ];

        StringBuilder retrato = new();
        foreach ((string coluna, ClassificacaoDeProtecao? classificacao) in colunas)
        {
            retrato.Append(coluna).Append(": ").Append(classificacao?.ToString() ?? "SemClassificacao").Append('\n');
        }

        return (violacoes, retrato.ToString());
    }

    /// <summary>A classificação de cada coluna mapeada, por <c>schema.tabela.coluna</c>; a coluna sem classificação fica fora.</summary>
    public static IReadOnlyDictionary<string, ClassificacaoDeProtecao> Classificacoes(IModel modelo) =>
        Analisar(modelo).Colunas
            .Where(static c => c.Value is not null)
            .ToDictionary(static c => c.Key, static c => c.Value!.Value, StringComparer.Ordinal);

    private static (SortedDictionary<string, ClassificacaoDeProtecao?> Colunas, List<string> NaoConferidos, Dictionary<string, SortedSet<string>> SemDeclaracao) Analisar(IModel modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        SortedDictionary<string, ClassificacaoDeProtecao?> colunas = new(StringComparer.Ordinal);
        List<string> naoConferidos = [];
        Dictionary<string, SortedSet<string>> semDeclaracao = new(StringComparer.Ordinal);

        foreach (IEntityType tipo in modelo.GetEntityTypes())
        {
            string nomeDoTipo = tipo.DisplayName();

            if (tipo.GetComplexProperties().Any())
            {
                naoConferidos.Add($"{nomeDoTipo}: complex property não é conferida; mapeie como owned type com classificação própria.");
            }

            if (tipo.GetMappingFragments().Any())
            {
                naoConferidos.Add($"{nomeDoTipo}: tabela de fragmento (SplitToTable) não é conferida.");
            }

            if (tipo.GetMappingStrategy() is RelationalAnnotationNames.TptMappingStrategy or RelationalAnnotationNames.TpcMappingStrategy
                && (tipo.BaseType is not null || tipo.GetDirectlyDerivedTypes().Any()))
            {
                naoConferidos.Add($"{nomeDoTipo}: herança mapeada como TPT ou TPC não é conferida; as colunas herdadas ficariam fora do retrato.");
            }

            if (StoreObjectIdentifier.Create(tipo, StoreObjectType.Table) is not null && StoreObjectIdentifier.Create(tipo, StoreObjectType.View) is not null)
            {
                naoConferidos.Add($"{nomeDoTipo}: tipo mapeado a tabela e a view ao mesmo tempo; a conferência não sabe qual coluna vale.");
                continue;
            }

            StoreObjectIdentifier? objeto = ObjetoDeArmazenamento(tipo);
            if (objeto is null)
            {
                naoConferidos.Add($"{nomeDoTipo}: tipo mapeado sem tabela nem view; a conferência não sabe resolver as colunas dele.");
                continue;
            }

            foreach (IProperty propriedade in tipo.GetDeclaredProperties())
            {
                string? nomeDaColuna = propriedade.GetColumnName(objeto.Value);
                if (nomeDaColuna is null)
                {
                    naoConferidos.Add($"{nomeDoTipo}.{propriedade.Name}: propriedade sem coluna na tabela do tipo; a conferência não sabe resolvê-la.");
                    continue;
                }

                string coluna = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{(objeto.Value.Schema is null ? string.Empty : objeto.Value.Schema + ".")}{objeto.Value.Name}.{nomeDaColuna}");
                ClassificacaoDeProtecao? classificacao = ClassificacaoDeProtecaoNoModelo.Resolver(propriedade);

                if (classificacao is null)
                {
                    if (!semDeclaracao.TryGetValue(coluna, out SortedSet<string>? tipos))
                    {
                        semDeclaracao[coluna] = tipos = new SortedSet<string>(StringComparer.Ordinal);
                    }

                    tipos.Add(nomeDoTipo);
                }

                // Tipos que dividem a tabela e a coluna (a chave) resolvem a mais alta: nunca rebaixa.
                colunas[coluna] = colunas.TryGetValue(coluna, out ClassificacaoDeProtecao? existente)
                    ? MaisAlta(existente, classificacao)
                    : classificacao;
            }
        }

        return (colunas, naoConferidos, semDeclaracao);
    }

    private static StoreObjectIdentifier? ObjetoDeArmazenamento(IEntityType tipo) =>
        StoreObjectIdentifier.Create(tipo, StoreObjectType.Table)
        ?? StoreObjectIdentifier.Create(tipo, StoreObjectType.View);

    private static ClassificacaoDeProtecao? MaisAlta(ClassificacaoDeProtecao? a, ClassificacaoDeProtecao? b) =>
        a is null || b is null ? null : (ClassificacaoDeProtecao)Math.Max((int)a, (int)b);
}
