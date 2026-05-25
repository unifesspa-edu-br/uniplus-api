using System.Security.Cryptography;
using System.Text;
using Marten;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Portas;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Persistencia;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Pii;

/// <summary>
/// Protetor de PII com AES-256-GCM + crypto-shredding.
/// <para>
/// As chaves são geridas num <b>unit-of-work próprio</b> (sessão dedicada via
/// <see cref="IDocumentStore"/>), desacopladas da transação do append de eventos.
/// Isso modela um cofre de chaves separado — a recomendação de produção é externalizar
/// as chaves (Vault/KMS), nunca guardá-las junto dos eventos. Uma chave órfã (cujo
/// evento sofreu rollback) é inócua; cada evento referencia a chave exata que o cifrou.
/// </para>
/// </summary>
internal sealed class ProtetorPiiAesGcm(IDocumentStore store)
    : IProtetorPii, IServicoEsquecimento
{
    private const int TamanhoChaveBytes = 32; // AES-256
    private const int TamanhoNonceBytes = 12; // GCM padrão
    private const int TamanhoTagBytes = 16;
    private const char Separador = '\u001F'; // Unit Separator: não ocorre em nome/CPF

    public async Task<AtorCifrado> ProtegerAsync(Ator ator, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ator);

        Guid chaveId;
        byte[] chave;
        IDocumentSession session = store.LightweightSession();
        await using (session.ConfigureAwait(false))
        {
            (chaveId, chave) = await ObterOuCriarChaveAsync(session, ator.SujeitoId, cancellationToken)
                .ConfigureAwait(false);
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        byte[] claro = Encoding.UTF8.GetBytes($"{ator.Nome}{Separador}{ator.Cpf}");
        byte[] nonce = RandomNumberGenerator.GetBytes(TamanhoNonceBytes);
        byte[] cifrado = new byte[claro.Length];
        byte[] tag = new byte[TamanhoTagBytes];

        using (AesGcm aes = new(chave, TamanhoTagBytes))
        {
            aes.Encrypt(nonce, claro, cifrado, tag);
        }

        byte[] combinado = [.. nonce, .. cifrado, .. tag];
        return new AtorCifrado(ator.SujeitoId, chaveId, Convert.ToBase64String(combinado));
    }

    public async Task<Ator?> TentarRevelarAsync(AtorCifrado cifrado, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cifrado);

        // Decifra com a chave EXATA do evento; nunca uma substituta. Se a chave foi
        // esquecida (crypto-shredding), o conteúdo é irrecuperável → null.
        ChaveTitular? chave;
        IQuerySession session = store.QuerySession();
        await using (session.ConfigureAwait(false))
        {
            chave = await session.LoadAsync<ChaveTitular>(cifrado.ChaveId, cancellationToken)
                .ConfigureAwait(false);
        }

        if (chave is null)
        {
            return null;
        }

        byte[] combinado = Convert.FromBase64String(cifrado.Conteudo);
        ReadOnlySpan<byte> span = combinado;
        ReadOnlySpan<byte> nonce = span[..TamanhoNonceBytes];
        ReadOnlySpan<byte> tag = span[^TamanhoTagBytes..];
        ReadOnlySpan<byte> texto = span[TamanhoNonceBytes..^TamanhoTagBytes];

        byte[] claro = new byte[texto.Length];
        using (AesGcm aes = new(Convert.FromBase64String(chave.Chave), TamanhoTagBytes))
        {
            aes.Decrypt(nonce, texto, tag, claro);
        }

        string[] partes = Encoding.UTF8.GetString(claro).Split(Separador);
        return new Ator(cifrado.SujeitoId, partes[0], partes[1]);
    }

    public async Task EsquecerAsync(Guid sujeitoId, CancellationToken cancellationToken = default)
    {
        // Apaga TODAS as chaves do titular (idempotente), no seu próprio commit.
        // Eventos antigos passam a referenciar ChaveIds inexistentes → irrecuperáveis.
        IDocumentSession session = store.LightweightSession();
        await using (session.ConfigureAwait(false))
        {
            session.DeleteWhere<ChaveTitular>(k => k.SujeitoId == sujeitoId);
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<(Guid ChaveId, byte[] Chave)> ObterOuCriarChaveAsync(
        IDocumentSession session,
        Guid sujeitoId,
        CancellationToken cancellationToken)
    {
        ChaveTitular? existente = await session.Query<ChaveTitular>()
            .Where(k => k.SujeitoId == sujeitoId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existente is not null)
        {
            return (existente.Id, Convert.FromBase64String(existente.Chave));
        }

        // Cada chave tem id próprio. Uma corrida de criação concorrente gera no
        // máximo chaves duplicadas (ids distintos), nunca um upsert que substitua a
        // chave de outro evento — cada evento referencia a chave que de fato o cifrou.
        Guid novoId = Guid.CreateVersion7();
        byte[] novaChave = RandomNumberGenerator.GetBytes(TamanhoChaveBytes);
        session.Store(new ChaveTitular
        {
            Id = novoId,
            SujeitoId = sujeitoId,
            Chave = Convert.ToBase64String(novaChave),
        });
        return (novoId, novaChave);
    }
}
