using System.Security.Cryptography;
using System.Text;
using Marten;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application;
using Unifesspa.UniPlus.Spikes.EventSourcing.Application.Portas;
using Unifesspa.UniPlus.Spikes.EventSourcing.Domain;
using Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Persistencia;

namespace Unifesspa.UniPlus.Spikes.EventSourcing.Infrastructure.Pii;

/// <summary>
/// Protetor de PII com AES-256-GCM + crypto-shredding. A chave por titular vive na
/// sessão Marten ambiente — quando usado dentro de um handler, a criação da chave e
/// o append do evento commitam na mesma transação. Esquecer = apagar a chave.
/// </summary>
internal sealed class ProtetorPiiAesGcm(IDocumentSession session)
    : IProtetorPii, IServicoEsquecimento
{
    private const int TamanhoChaveBytes = 32; // AES-256
    private const int TamanhoNonceBytes = 12; // GCM padrão
    private const int TamanhoTagBytes = 16;
    private const char Separador = '\u001F'; // Unit Separator: não ocorre em nome/CPF

    public async Task<AtorCifrado> ProtegerAsync(Ator ator, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ator);

        (Guid chaveId, byte[] chave) = await ObterOuCriarChaveAsync(ator.SujeitoId, cancellationToken)
            .ConfigureAwait(false);

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
        ChaveTitular? chave = await session.LoadAsync<ChaveTitular>(cifrado.ChaveId, cancellationToken)
            .ConfigureAwait(false);
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

    public Task EsquecerAsync(Guid sujeitoId, CancellationToken cancellationToken = default)
    {
        // Apaga TODAS as chaves do titular (idempotente); efetiva no SaveChanges.
        // Eventos antigos passam a referenciar ChaveIds inexistentes → irrecuperáveis.
        session.DeleteWhere<ChaveTitular>(k => k.SujeitoId == sujeitoId);
        return Task.CompletedTask;
    }

    private async Task<(Guid ChaveId, byte[] Chave)> ObterOuCriarChaveAsync(
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
