namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

using System.Collections.Frozen;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

public sealed class QueryStringMasker
{
    private readonly FrozenSet<string> _nomesSensiveis;
    private readonly string _valorMascarado;

    public QueryStringMasker(IOptions<RequestLoggingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        RequestLoggingOptions valor = options.Value;

        // FrozenSet: build custoso amortizado (singleton), lookup mais rápido que HashSet.
        _nomesSensiveis = FrozenSet.ToFrozenSet(valor.NomesParametrosSensiveis, StringComparer.OrdinalIgnoreCase);
        _valorMascarado = valor.ValorMascarado;
    }

    public string Mascarar(QueryString queryString)
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
            // Segurança: compara a chave decodificada para não deixar bypass via
            // percent-encoding (ex.: `?%63%70%66=123` equivale a `?cpf=123`).
            string keyDecoded = Uri.UnescapeDataString(keyEncoded);

            sb.Append(keyEncoded);
            sb.Append('=');
            if (_nomesSensiveis.Contains(keyDecoded))
            {
                sb.Append(_valorMascarado);
            }
            else
            {
                sb.Append(pedaco.AsSpan(eqIdx + 1));
            }
        }

        return sb.ToString();
    }
}
