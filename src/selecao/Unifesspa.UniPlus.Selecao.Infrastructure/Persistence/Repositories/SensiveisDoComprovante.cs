namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;

/// <summary>
/// A proteção das respostas de cifra individual do comprovante: cifra ao gravar e, ao ler, decifra, confere que o envelope é
/// deste comprovante (HMAC de id e valores) e que os fatos restaurados são os que o hash dele cobre.
/// </summary>
internal sealed class SensiveisDoComprovante
{
    private readonly IUniPlusEncryptionService _encryption;
    private readonly IUniPlusBlindIndexService _blindIndex;

    public SensiveisDoComprovante(IUniPlusEncryptionService encryption, IUniPlusBlindIndexService blindIndex)
    {
        ArgumentNullException.ThrowIfNull(encryption);
        ArgumentNullException.ThrowIfNull(blindIndex);

        _encryption = encryption;
        _blindIndex = blindIndex;
    }

    /// <summary>Cifra as respostas sensíveis do comprovante emitido e guarda o envelope com o HMAC que o prende a ele.</summary>
    public async Task GuardarAsync(ComprovanteInscricao comprovante, CancellationToken cancellationToken)
    {
        if (comprovante.RespostasSensiveis.Count == 0)
        {
            return;
        }

        byte[] plano = JsonSerializer.SerializeToUtf8Bytes(comprovante.RespostasSensiveis);
        byte[] cifrados = await _encryption
            .EncryptAsync(SelecaoEncryptionKeys.SensiveisAesGcm, plano, cancellationToken).ConfigureAwait(false);
        comprovante.GuardarSensiveisCifrados(cifrados, await HmacAsync(comprovante.Id, plano, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Devolve ao comprovante lido as respostas sensíveis decifradas; falha alto se o envelope não for dele.</summary>
    public async Task RestaurarAsync(ComprovanteInscricao comprovante, CancellationToken cancellationToken)
    {
        if (comprovante.SensiveisCifrados is null && comprovante.SensiveisHmac is null)
        {
            return;
        }

        if (comprovante.SensiveisCifrados is not { } cifrados || comprovante.SensiveisHmac is not { } hmac)
        {
            throw new InvalidOperationException($"O comprovante {comprovante.Id} tem só uma das colunas dos dados sensíveis: o envelope cifrado e o HMAC andam juntos.");
        }

        byte[] plano = await _encryption.DecryptAsync(SelecaoEncryptionKeys.SensiveisAesGcm, cifrados, cancellationToken).ConfigureAwait(false);
        await ConferirVinculoAsync(comprovante.Id, plano, hmac, cancellationToken).ConfigureAwait(false);
        comprovante.RestaurarSensiveis(Ler(comprovante.Id, plano));
        if (!comprovante.IntegridadeConfere())
        {
            throw new InvalidOperationException($"Os fatos sensíveis restaurados do comprovante {comprovante.Id} não são os que o hash dele cobre.");
        }
    }

    private async Task ConferirVinculoAsync(Guid comprovanteId, byte[] plano, byte[] gravado, CancellationToken cancellationToken)
    {
        byte[] esperado = await HmacAsync(comprovanteId, plano, cancellationToken).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(gravado, esperado))
        {
            throw new InvalidOperationException($"As respostas sensíveis do comprovante {comprovanteId} não pertencem a ele: o vínculo não confere.");
        }
    }

    private static Dictionary<string, JsonElement> Ler(Guid comprovanteId, byte[] plano) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(plano)
        ?? throw new InvalidOperationException($"As respostas sensíveis do comprovante {comprovanteId} decifradas não são um objeto JSON.");

    /// <summary>O HMAC do id do comprovante e dos valores sensíveis, exatamente como foram cifrados.</summary>
    private Task<byte[]> HmacAsync(Guid comprovanteId, byte[] valores, CancellationToken cancellationToken) =>
        _blindIndex.ComputarAsync(
            SelecaoEncryptionKeys.SensiveisHmac,
            (byte[])[.. Encoding.UTF8.GetBytes($"comprovante:{comprovanteId:D}:"), .. valores],
            cancellationToken);
}
