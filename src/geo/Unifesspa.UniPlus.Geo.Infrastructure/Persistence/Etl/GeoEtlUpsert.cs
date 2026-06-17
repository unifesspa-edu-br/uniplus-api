namespace Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Helpers de upsert por chave natural compartilhados pelos importadores do ETL DNE
/// (ADR-0092): dedup intra-fonte (last-wins), agregação contando o que a fonte trouxe
/// e normalização das chaves naturais. Extraídos do importador de País/Estado/Cidade
/// (#672) para reuso no de Distrito/Bairro (#673) sem duplicar a lógica idempotente.
/// </summary>
internal static class GeoEtlUpsert
{
    /// <summary>
    /// Upsert por chave natural com dedup intra-fonte: chaves repetidas na própria
    /// carga não disparam unique violation — a 2ª ocorrência atualiza a entidade já
    /// vista (last-wins) e conta como duplicada. Retorna a entidade (para resolver FK)
    /// ou <see langword="null"/> em falha de validação do domínio.
    /// </summary>
    public static T? Upsert<T>(
        string chave,
        Dictionary<string, T> jaVistas,
        Dictionary<string, T> existentes,
        Func<Result<T>> criar,
        Func<T, Result> atualizar,
        DbSet<T> dbSet,
        ContadorTabela contador)
        where T : class
    {
        if (jaVistas.TryGetValue(chave, out T? jaVista))
        {
            contador.ContarDuplicado(chave);
            Result reatualizacao = atualizar(jaVista);
            return reatualizacao.IsSuccess ? jaVista : null;
        }

        if (existentes.TryGetValue(chave, out T? existente))
        {
            Result atualizacao = atualizar(existente);
            if (atualizacao.IsFailure)
            {
                contador.ContarIgnoradoSemChave();
                return null;
            }

            jaVistas[chave] = existente;
            contador.ContarAtualizado();
            return existente;
        }

        Result<T> criado = criar();
        if (criado.IsFailure)
        {
            contador.ContarIgnoradoSemChave();
            return null;
        }

        dbSet.Add(criado.Value!);
        jaVistas[chave] = criado.Value!;
        contador.ContarInserido();
        return criado.Value;
    }

    /// <summary>Agrega a fonte por chave (last-wins) sem contar — para níveis cujos contadores são resolvidos depois.</summary>
    public static async Task<Dictionary<string, T>> AgregarPorChaveAsync<T>(
        IAsyncEnumerable<T> fonte,
        Func<T, string?> chave)
    {
        Dictionary<string, T> mapa = new(StringComparer.Ordinal);
        await foreach (T item in fonte.ConfigureAwait(false))
        {
            string? k = chave(item);
            if (k is not null)
            {
                mapa[k] = item; // last-wins
            }
        }

        return mapa;
    }

    /// <summary>
    /// Como <see cref="AgregarPorChaveAsync{T}"/>, mas registra no contador da tabela
    /// tudo o que a fonte trouxe (lidos), o que foi descartado por chave ausente e as
    /// chaves repetidas (last-wins) — para o relatório não subestimar os "lidos" dos
    /// níveis agregados.
    /// </summary>
    public static async Task<Dictionary<string, T>> AgregarContandoAsync<T>(
        IAsyncEnumerable<T> fonte,
        Func<T, string?> chave,
        ContadorTabela contador)
    {
        Dictionary<string, T> mapa = new(StringComparer.Ordinal);
        await foreach (T item in fonte.ConfigureAwait(false))
        {
            contador.ContarLido();
            string? k = chave(item);
            if (k is null)
            {
                contador.ContarIgnoradoSemChave();
                continue;
            }

            if (!mapa.TryAdd(k, item))
            {
                contador.ContarDuplicado(k); // last-wins
                mapa[k] = item;
            }
        }

        return mapa;
    }

    /// <summary>Chave natural alfabética (sigla/UF): trim + maiúsculas; vazio → <see langword="null"/>.</summary>
    public static string? ChaveSigla(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpperInvariant();

    /// <summary>Chave natural numérica/código (código IBGE/CEP): trim; vazio → <see langword="null"/>.</summary>
    public static string? ChaveCodigo(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>Chave natural composta de uma faixa de CEP: pai (Guid) + limites — casa com a UNIQUE.</summary>
    public static string ChaveFaixa(Guid pai, string cepInicial, string cepFinal) =>
        $"{pai:N}|{cepInicial}|{cepFinal}";
}
