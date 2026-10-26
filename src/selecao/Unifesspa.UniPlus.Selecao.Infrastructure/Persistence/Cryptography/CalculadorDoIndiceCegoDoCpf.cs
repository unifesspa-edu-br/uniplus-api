namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;

using System.Text;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;

/// <summary>
/// Único ponto que sabe como o CPF vira índice cego: HMAC do texto <c>cpf:</c> seguido dos onze
/// dígitos, com a chave de identificadores. Quem grava a inscrição e quem cruza arquivos externos
/// com ela precisam produzir exatamente o mesmo valor, senão o CPF nunca casa.
/// </summary>
public sealed class CalculadorDoIndiceCegoDoCpf
{
    private readonly IUniPlusBlindIndexService _blindIndex;

    public CalculadorDoIndiceCegoDoCpf(IUniPlusBlindIndexService blindIndex)
    {
        ArgumentNullException.ThrowIfNull(blindIndex);

        _blindIndex = blindIndex;
    }

    /// <summary>Calcula o índice cego do CPF, informado como os onze dígitos.</summary>
    public Task<byte[]> ComputarAsync(string cpf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cpf);

        return _blindIndex.ComputarAsync(
            SelecaoEncryptionKeys.IdentificadoresHmac, Encoding.UTF8.GetBytes($"cpf:{cpf}"), cancellationToken);
    }
}
