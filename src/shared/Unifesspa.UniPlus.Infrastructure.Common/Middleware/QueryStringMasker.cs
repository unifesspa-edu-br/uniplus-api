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

        // Snapshot imutável das opções no momento da construção: o masker é
        // singleton de longa duração e FrozenSet oferece lookup mais rápido
        // do que HashSet normal a um custo de build pago uma única vez aqui.
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
            // Decodifica a chave para comparar com a lista ignorando URL-encoding,
            // mas preserva o encoding original ao reescrever o par — evita vetores
            // de bypass como `?%63%70%66=123` escaparem do masking.
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
