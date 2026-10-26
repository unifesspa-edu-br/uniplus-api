namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Cryptography;

using System.Collections.Concurrent;
using System.Text;

using Unifesspa.UniPlus.Infrastructure.Core.Cryptography;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Único ponto que sabe como o CPF vira índice cego: HMAC do texto <c>cpf:</c> seguido dos onze
/// dígitos, com a chave de identificadores. Quem grava a inscrição e quem cruza arquivos externos
/// com ela precisam produzir exatamente o mesmo valor, senão o CPF nunca casa.
/// </summary>
public sealed class CalculadorDoIndiceCegoDoCpf : IIndiceCegoDoCpf
{
    private const int GrauDeParalelismo = 16;

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

    /// <summary>
    /// Cada índice é uma chamada ao cofre de chaves, sem versão em lote: o paralelismo é limitado
    /// para não saturá-lo.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, IndiceCegoDoCpf>> CalcularAsync(
        IReadOnlyCollection<string> cpfs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cpfs);

        ConcurrentDictionary<string, IndiceCegoDoCpf> indices = new(StringComparer.Ordinal);
        await Parallel.ForEachAsync(
            cpfs.Distinct(StringComparer.Ordinal),
            new ParallelOptions { MaxDegreeOfParallelism = GrauDeParalelismo, CancellationToken = cancellationToken },
            async (cpf, ct) => indices[cpf] = IndiceCegoDoCpf.De(await ComputarAsync(cpf, ct).ConfigureAwait(false)))
            .ConfigureAwait(false);

        return indices;
    }
}
