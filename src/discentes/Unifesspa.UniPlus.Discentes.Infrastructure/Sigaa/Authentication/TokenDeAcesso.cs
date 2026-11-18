namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Authentication;

using System;
using System.Buffers.Text;
using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O token que a API do SIGAA entrega na autenticação: um segredo opaco, repassado como veio.
/// Só se recusa o que a origem não entregou; não se exige que seja um JWT.
/// </summary>
/// <remarks>
/// O segredo só sai daqui para montar o cabeçalho de autorização. Nenhuma representação em
/// texto o expõe.
/// </remarks>
internal sealed record TokenDeAcesso
{
    private TokenDeAcesso(string segredo) => Segredo = segredo;

    public string Segredo { get; }

    public static Result<TokenDeAcesso> Criar(string? texto) =>
        string.IsNullOrWhiteSpace(texto)
            ? Result<TokenDeAcesso>.Failure(new DomainError("TokenDeAcesso.Vazio", "O token de acesso é obrigatório."))
            : Result<TokenDeAcesso>.Success(new TokenDeAcesso(texto));

    /// <summary>
    /// Lê os instantes de expiração e emissão declarados no token, sem verificar a
    /// assinatura — a verificação é da origem, e o que se busca é só o momento de renovar.
    /// Devolve falso quando o token não os declara de forma legível, ou quando não cabem na
    /// faixa de datas representáveis; a emissão pode faltar mesmo quando a leitura vale.
    /// </summary>
    /// <remarks>
    /// Os instantes vêm de fora e podem estar em qualquer escala — uma expiração gravada em
    /// milissegundos, por exemplo, estoura a faixa de datas. Isso é mais um caso de validade
    /// ilegível, e não pode impedir o token de chegar à origem: quem o valida é ela. Instante
    /// com tipo JSON que não é número, ou corpo que não é objeto, lança
    /// <see cref="InvalidOperationException"/>.
    /// </remarks>
    public bool TentarLerInstantesDeclarados(out DateTimeOffset expiraEm, out DateTimeOffset? emitidoEm)
    {
        if (LerInstantes(Segredo) is { } instantes && instantes.EstaNaFaixaDeDatas())
        {
            expiraEm = DateTimeOffset.FromUnixTimeSeconds(instantes.ExpiraEm);
            emitidoEm = instantes.EmitidoEm is { } emissao ? DateTimeOffset.FromUnixTimeSeconds(emissao) : null;
            return true;
        }

        expiraEm = default;
        emitidoEm = null;
        return false;
    }

    public override string ToString() => "[TokenDeAcesso]";

    private static InstantesDoToken? LerInstantes(string token) =>
        token.Split('.') is [_, { Length: > 0 } corpo, ..] ? DecodificarInstantes(corpo) : null;

    /// <summary>
    /// Decodifica o trecho do meio do token. Apesar do nome, a decodificação lança quando
    /// encontra caractere fora do alfabeto — o valor de retorno cobre só o caso de destino
    /// pequeno demais. Sem esta captura, um token que não seja um dos três trechos
    /// separados por ponto derrubaria a autenticação inteira, em vez de apenas deixar a
    /// validade indisponível.
    /// </summary>
    private static InstantesDoToken? DecodificarInstantes(string corpo)
    {
        try
        {
            return ExtrairInstantes(corpo);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static InstantesDoToken? ExtrairInstantes(string corpo)
    {
        byte[] bytes = new byte[Base64Url.GetMaxDecodedLength(corpo.Length)];

        if (!Base64Url.TryDecodeFromChars(corpo, bytes, out int escritos))
        {
            return null;
        }

        using JsonDocument documento = JsonDocument.Parse(bytes.AsMemory(0, escritos));
        JsonElement raiz = documento.RootElement;

        return raiz.TryGetProperty("exp", out JsonElement expiracao) && expiracao.TryGetInt64(out long expiraEm)
            ? new InstantesDoToken(expiraEm, LerEmissao(raiz))
            : null;
    }

    private static long? LerEmissao(JsonElement raiz) =>
        raiz.TryGetProperty("iat", out JsonElement emissao) && emissao.TryGetInt64(out long emitidoEm)
            ? emitidoEm
            : null;

    /// <summary>
    /// Os instantes que o token declara, em segundos desde a época; a emissão pode faltar.
    /// </summary>
    private readonly record struct InstantesDoToken(long ExpiraEm, long? EmitidoEm)
    {
        /// <summary>
        /// Diz se os instantes declarados cabem na faixa de datas que se consegue
        /// representar.
        /// </summary>
        public bool EstaNaFaixaDeDatas() =>
            CabeNaFaixa(ExpiraEm) && (EmitidoEm is not { } emissao || CabeNaFaixa(emissao));

        private static bool CabeNaFaixa(long segundosDesdeAEpoca) =>
            segundosDesdeAEpoca >= DateTimeOffset.MinValue.ToUnixTimeSeconds()
            && segundosDesdeAEpoca <= DateTimeOffset.MaxValue.ToUnixTimeSeconds();
    }
}
