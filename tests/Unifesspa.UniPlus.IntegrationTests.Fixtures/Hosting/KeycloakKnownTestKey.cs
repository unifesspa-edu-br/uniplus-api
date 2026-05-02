namespace Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;

using System.Security.Cryptography;

using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Par RSA fixo embarcado no realm sintético de testes (<c>realm-e2e-tests.json</c>) via
/// <c>components.org.keycloak.keys.KeyProvider</c>. A chave pública correspondente é publicada pelo
/// Keycloak no JWKS do realm, portanto qualquer token assinado com a privada abaixo é aceito pela
/// validação criptográfica do <c>JwtBearer</c> da API.
///
/// O propósito é permitir que testes E2E forjem tokens com assinatura válida e isolem ESPECIFICAMENTE
/// validações lógicas (issuer, audience, lifetime) — em vez de cair sempre em
/// <c>ValidateIssuerSigningKey</c>. Sem isso, um regressão que desligue <c>ValidateIssuer</c> mas
/// mantenha a checagem de signing key ainda passaria pelos testes, dando falsa cobertura.
///
/// <para>
/// <b>Segurança:</b> esta chave privada é PUBLICAMENTE conhecida pelo repositório e existe SOMENTE no
/// realm sintético <c>unifesspa-e2e</c> (arquivo <c>docker/keycloak/realm-e2e-tests.json</c>), que
/// jamais é montado em <c>docker-compose</c>, Helm, dev, homologação ou produção. O realm canônico
/// (<c>realm-export.json</c>) não contém este key provider e o JWKS de produção é gerado por chaves
/// privadas autônomas do KC. Não há vetor de exposição da aplicação real.
/// </para>
///
/// <para>
/// <b>Como atualizar:</b> se for necessário girar a chave (improvável; ela é fixa de propósito),
/// gerar novo par com <c>openssl genrsa -out key.pem 2048 &amp;&amp; openssl req -new -x509 -key key.pem
/// -out cert.pem -days 36500 -subj "/CN=uniplus-e2e-test-key"</c>, atualizar <see cref="PrivateKeyPem"/>
/// abaixo (PKCS#8) e o campo <c>privateKey</c>+<c>certificate</c> em <c>realm-e2e-tests.json</c>.
/// Os dois precisam permanecer sincronizados.
/// </para>
/// </summary>
public static class KeycloakKnownTestKey
{
    /// <summary>
    /// Nome do <c>KeyProvider</c> declarado em <c>realm-e2e-tests.json</c>. Serve apenas para
    /// localização em logs do KC.
    /// </summary>
    public const string ProviderName = "uniplus-e2e-known-test-key";

    /// <summary>
    /// Key ID (<c>kid</c>) que o Keycloak gera deterministicamente para esta chave ao importar o
    /// realm — é um SHA-256 derivado da modulus pública. Tokens forjados com este <c>kid</c> são
    /// validados pelo <c>JwtBearer</c> da API porque o JWKS do realm publica essa kid.
    ///
    /// <para>
    /// <b>Como recalcular se a chave mudar:</b> subir a imagem composta com o realm importado, fazer
    /// <c>curl http://&lt;kc&gt;/realms/unifesspa-e2e/protocol/openid-connect/certs</c> e copiar o
    /// campo <c>keys[].kid</c> da chave (kty=RSA, use=sig).
    /// </para>
    /// </summary>
    public const string KeyId = "QX3fDYzTPMKOzUJO2CvCnQdp_LB9yOIizHBHZvhAecA";

    /// <summary>
    /// Chave privada RSA 2048 em formato PKCS#8 PEM. Mesmo conteúdo (sem headers/footers e sem
    /// quebras de linha) está em <c>realm-e2e-tests.json</c> no campo
    /// <c>components.org.keycloak.keys.KeyProvider[0].config.privateKey[0]</c>.
    /// </summary>
    public const string PrivateKeyPem =
        "-----BEGIN PRIVATE KEY-----\n" +
        "MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDsWkGp3wd424ml\n" +
        "KBkPnrWzmZE8BBw1vcvzFSnMj9NED5H/PVvuCoCY8PMghK4/HFJUMHIX0qG5ue9I\n" +
        "RJvZ6hCUkqhcjSDd4sraljQsT7/jBZ+RqofagOM+1OizyTBOzBboGRsi86Bze1SH\n" +
        "8Dc+bQ96R0TXWhtAOC0Yk6jv+4g7T7JZwEODElPGBVOvaJJDvaV5q8wVYDlUdFbd\n" +
        "dBsWfCy4Sn8KEozPg7tEyYLiYQrI2TSNFn5e6QOW8BLmNQQ8/g0Kghmcrpey7KPH\n" +
        "1VMiZ7nlah5Ftn7U4VjJbJTm9KVEZ4oZ6qS5EcCIT2BtsUXtvZbcs6jDZCPElXGg\n" +
        "mEGGvOi3AgMBAAECggEAUWSPolk88IDh+O9DGh70weHLoxhjQpqW5qJOH7UT8ydN\n" +
        "htFxnBsfyAuKHpOykedF7to0IEIYEaaXYZLG/RdfGFsdAapUPDVC2F3Ln8ri8OJZ\n" +
        "3kcUu8mQ+G1HqcpKCYi9BrbGopW1lq9NH/c4fxX9s4VhjqvoIIh39zO6hNJhStLw\n" +
        "z167xGZylj1HTDNtuGtx+Fwwl9P3b1esWWvMxatKrKpC1TEt1AjZTFlfkjF6xQOj\n" +
        "Iq2B8PQn0BBpbUZrUlPoA0o4/gzaON2jLfjYs14vyaTuSAEb2zdMt3r42X6vLogx\n" +
        "KcVxaW5CqjKDlqROoalItrnN2hP0Q/dTZkBhAY5IYQKBgQD5qs82w42E+ac21Hb5\n" +
        "xqDy+ttXBosV9R+N0vU/XI8n63V4dTAHoJTf+arGKz2RXP9krrSul1W5UThuphfH\n" +
        "drbVqROcWuJWbUrso9Cwozayg/Ntb9eXP9IU9WODd8TkyDZ5a4NCsmxl/d3NBIMZ\n" +
        "R5EfqeLwssG8qWbPpNiV6jyzkQKBgQDyWP1VK+7DSenUYSJqsuMpQjrDqig75HZx\n" +
        "h1uFzyLzZoThvukWbYzN07o6Uj340feY1DM69Ey3eAVem6dmTdjYW62TaCOR+wfK\n" +
        "XDMcV0EOAcb//7jRAYrQR5nyyqZ+d2l6Mbb8Z9BO+iLVz5vYwbcm8lvJ9EdzZDxU\n" +
        "uD/Di7ijxwKBgG0e/tpMtjn8c90/F5EsA4Svp9ZtgbTjIht2rMI4zkkAXKN9dLSg\n" +
        "tvD9ymo60/oIz4dN5KK6ejk5CpUx+wqvFFJmR6/6+RoVQr4TC09oxqtXiLm4PF5b\n" +
        "ApMufYQkgOYNq+F94CzylvYs8xh8dGBEK2XPduUE/DBdShZPUmqTqlxBAoGBAOw6\n" +
        "RiYhfskpYR492Jh86uSqxDE5yaIn3jRnppTWBdGQGvMZbocIHfn76kkzJWlG8bwt\n" +
        "DArpW2ZzPXis7Q3R0A+Fvbo0BogjU8KzALcdbjJDFUEweWxxvmerg6qgUo5vw4by\n" +
        "stVyNCDnvdEAX393xBnYoBRJYuRdzlkeiDkKFt69AoGASzN298OJl0w2fkE8dd/W\n" +
        "+pPJPwtJbWdOLEw3vyPvrhbcESZO6oZbV7QAk1UA/J0/iFEHLptl396kAOGb9ttx\n" +
        "hz1LmjcvZjhcL0YAMOLt/UZulAgIYtVRNk0nPohNV2+A1dJk/byXe7M+JygEmzOM\n" +
        "TiUk0iLoAHmetgjKreSWVhc=\n" +
        "-----END PRIVATE KEY-----";

    /// <summary>
    /// Cria uma <see cref="RsaSecurityKey"/> a partir da chave privada embutida, pronta para assinar
    /// tokens de teste com a kid que o Keycloak publicará no JWKS.
    /// </summary>
    public static RsaSecurityKey CreateSigningKey()
    {
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(PrivateKeyPem);
        return new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: true))
        {
            KeyId = KeyId,
        };
    }
}
