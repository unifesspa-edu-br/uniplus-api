namespace Unifesspa.UniPlus.Selecao.Infrastructure.Antivirus;

using System.Security.Cryptography;

using Domain.Entities;

using Persistence.Cryptography;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// A proteção do arquivo do candidato: o clamd verifica o conteúdo em claro e a chave de dado
/// sensível já existente (<see cref="SelecaoEncryptionKeys.SensiveisAesGcm"/>, parecer das encarregadas
/// de dados de 13/10/2026) cifra o que vai ao armazenamento selado. Sem chave por arquivo.
/// </summary>
public sealed class ProtecaoDoConteudoSelado : IProtecaoDoConteudoSelado
{
    private readonly ClamdClient _antivirus;
    private readonly IUniPlusEncryptionService _encryption;

    public ProtecaoDoConteudoSelado(ClamdClient antivirus, IUniPlusEncryptionService encryption)
    {
        ArgumentNullException.ThrowIfNull(antivirus);
        ArgumentNullException.ThrowIfNull(encryption);

        _antivirus = antivirus;
        _encryption = encryption;
    }

    public async Task<Result> VerificarAsync(byte[] conteudo, CancellationToken cancellationToken = default) =>
        await _antivirus.VerificarAsync(conteudo, cancellationToken).ConfigureAwait(false) switch
        {
            VeredictoDoAntivirus.Limpo => Result.Success(),
            VeredictoDoAntivirus.Infectado => Result.Failure(ArquivoDaApresentacao.ArquivoInfectado),
            _ => Result.Failure(ArquivoDaApresentacao.AntivirusIndisponivel),
        };

    public Task<byte[]> SelarAsync(byte[] conteudo, CancellationToken cancellationToken = default) =>
        _encryption.EncryptAsync(SelecaoEncryptionKeys.SensiveisAesGcm, conteudo, cancellationToken);

    public async Task<Result<byte[]>> AbrirAsync(byte[] selado, string hashSha256, CancellationToken cancellationToken = default)
    {
        byte[] claro;
        try
        {
            claro = await _encryption
                .DecryptAsync(SelecaoEncryptionKeys.SensiveisAesGcm, selado, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (EncryptionFailureException)
        {
            // A etiqueta de autenticação não confere: o objeto foi alterado no armazenamento.
            return Result<byte[]>.Failure(ArquivoDaApresentacao.ConteudoAdulterado);
        }

        // O envelope autenticado prova que o conteúdo é nosso, mas não que é deste arquivo: o hash gravado
        // na confirmação acusa o objeto trocado por outro.
        string calculado = Convert.ToHexStringLower(SHA256.HashData(claro));
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(calculado), System.Text.Encoding.ASCII.GetBytes(hashSha256))
                ? Result<byte[]>.Success(claro)
                : Result<byte[]>.Failure(ArquivoDaApresentacao.ConteudoAdulterado);
    }
}
