namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Paginacao;

using System.Text.Json;

using Unifesspa.UniPlus.Infrastructure.Core.Pagination;

/// <summary>O cursor decifrado inteiro: o conteúdo do payload e as partes da chave de continuação.</summary>
public sealed record ConteudoDoCursor(CursorPayload Payload, string Resumo, string InstanteCongelado, IReadOnlyList<string> Colunas)
{
    private const int PartesFixas = 2;
    private const int MaximoDeColunas = 8;

    /// <summary>Todo o payload em texto, para conferir que nenhum campo carrega o que não pode.</summary>
    public string Texto => JsonSerializer.Serialize(Payload);

    public static ConteudoDoCursor De(CursorPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        for (int colunas = 0; colunas <= MaximoDeColunas; colunas++)
        {
            if (CompositeSortKey.TryDeserialize(payload.SortKey, PartesFixas + colunas, out IReadOnlyList<string> partes))
            {
                return new ConteudoDoCursor(payload, partes[0], partes[1], [.. partes.Skip(PartesFixas)]);
            }
        }

        throw new InvalidOperationException("A chave de continuação do cursor não tem a forma esperada.");
    }
}
