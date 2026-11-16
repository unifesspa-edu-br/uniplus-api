namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.SchemaRegistry;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Confluent.SchemaRegistry;

using Microsoft.Extensions.Options;

/// <summary>Lê a regra de compatibilidade que o Schema Registry aplica a um subject.</summary>
public interface ILeitorDaRegraDeCompatibilidadeDoSubject
{
    Task<Compatibility> LerRegraDeCompatibilidadeEfetivaAsync(string subject, CancellationToken cancellationToken);
}

/// <summary>
/// Lê a regra <b>efetiva</b> do subject: a própria, ou a global quando o subject não tem uma.
/// </summary>
/// <remarks>
/// Existe porque o cliente oficial (2.16) não expõe <c>defaultToGlobal</c>: sem ele, o Apicurio 3.2.4 devolve
/// <c>NONE</c> para um subject sem regra própria, ainda que aplique a regra global nas novas versões, e a leitura
/// simples daria um falso <c>NONE</c>. Só lê; o registro segue pelo cliente oficial. A URL e a autenticação vêm das
/// mesmas opções do <see cref="SchemaRegistrySettings"/>, sem segunda configuração.
/// </remarks>
internal sealed class LeitorDaRegraDeCompatibilidadeDoSubject : ILeitorDaRegraDeCompatibilidadeDoSubject
{
    private readonly HttpClient _http;
    private readonly SchemaRegistrySettings _configuracao;
    private readonly IAuthenticationHeaderValueProvider? _autenticacaoOAuth;

    public LeitorDaRegraDeCompatibilidadeDoSubject(
        HttpClient http,
        IOptions<SchemaRegistrySettings> configuracao,
        IAuthenticationHeaderValueProvider? autenticacaoOAuth = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(configuracao);

        _http = http;
        _configuracao = configuracao.Value;
        _http.Timeout = TimeSpan.FromMilliseconds(_configuracao.RequestTimeoutMs);
        _autenticacaoOAuth = autenticacaoOAuth;
    }

    public async Task<Compatibility> LerRegraDeCompatibilidadeEfetivaAsync(string subject, CancellationToken cancellationToken)
    {
        string? corpo = await LerAsync($"config/{Uri.EscapeDataString(subject)}?defaultToGlobal=true", cancellationToken).ConfigureAwait(false);

        // Subject que ainda não existe e sem regra própria, como na primeira subida: a regra que ele recebe ao ser
        // criado é a global.
        corpo ??= await LerAsync("config", cancellationToken).ConfigureAwait(false)
            ?? throw new HttpRequestException("A regra de compatibilidade global do Schema Registry não foi encontrada.", null, HttpStatusCode.NotFound);

        return Regra(corpo, subject);
    }

    // Nulo quando o Schema Registry responde 404; qualquer outra resposta de erro propaga.
    private async Task<string?> LerAsync(string caminho, CancellationToken cancellationToken)
    {
        using HttpRequestMessage requisicao = new(HttpMethod.Get, new Uri($"{_configuracao.Url.TrimEnd('/')}/{caminho}"));
        requisicao.Headers.Authorization = Autenticacao();

        using HttpResponseMessage resposta = await _http.SendAsync(requisicao, cancellationToken).ConfigureAwait(false);
        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        resposta.EnsureSuccessStatusCode();

        return await resposta.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private AuthenticationHeaderValue? Autenticacao()
    {
        if (_autenticacaoOAuth is not null)
        {
            return _autenticacaoOAuth.GetAuthenticationHeader();
        }

        return string.IsNullOrWhiteSpace(_configuracao.BasicAuthUserInfo)
            ? null
            : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(_configuracao.BasicAuthUserInfo)));
    }

    private static Compatibility Regra(string corpo, string subject)
    {
        using JsonDocument documento = JsonDocument.Parse(corpo);
        string? nome = documento.RootElement.TryGetProperty("compatibilityLevel", out JsonElement nivel) ? nivel.GetString() : null;

        return Enum.TryParse(nome?.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out Compatibility regra)
            ? regra
            : throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture, $"A regra de compatibilidade do subject '{subject}' veio ilegível: '{nome}'."));
    }
}
