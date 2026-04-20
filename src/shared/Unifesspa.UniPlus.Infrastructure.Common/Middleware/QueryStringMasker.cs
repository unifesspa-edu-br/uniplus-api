namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

using System.Collections.Frozen;
using System.Text;

using Microsoft.AspNetCore.Http;

public static class QueryStringMasker
{
    public const string MaskedValue = "***";

    // Comparador ordinal case-insensitive cobre variantes como "CPF", "Cpf" e "cpf"
    // sem depender de cultura — critério LGPD aplica-se igual ao parâmetro qualquer
    // que seja a grafia enviada pelo cliente.
    internal static readonly FrozenSet<string> NomesSensiveis = FrozenSet.ToFrozenSet(
        new[] { "cpf", "email", "senha", "password", "token" },
        StringComparer.OrdinalIgnoreCase);

    public static string Mascarar(QueryString queryString)
    {
        if (!queryString.HasValue || queryString.Value!.Length <= 1)
        {
            return queryString.Value ?? string.Empty;
        }

        string raw = queryString.Value![1..];
        StringBuilder sb = new(queryString.Value.Length);
        sb.Append('?');

        bool primeiro = true;
        foreach (string pedaco in raw.Split('&'))
        {
            if (pedaco.Length == 0)
            {
                continue;
            }

            if (!primeiro)
            {
                sb.Append('&');
            }

            primeiro = false;

            int eqIdx = pedaco.IndexOf('=', StringComparison.Ordinal);
            if (eqIdx < 0)
            {
                sb.Append(pedaco);
                continue;
            }

            string keyEncoded = pedaco[..eqIdx];
            // Decodifica a chave para comparar com a lista ignorando URL-encoding,
            // mas preserva o encoding original ao reescrever o par.
            string keyDecoded = Uri.UnescapeDataString(keyEncoded);

            sb.Append(keyEncoded);
            sb.Append('=');
            if (NomesSensiveis.Contains(keyDecoded))
            {
                sb.Append(MaskedValue);
            }
            else
            {
                sb.Append(pedaco.AsSpan(eqIdx + 1));
            }
        }

        return sb.ToString();
    }
}
