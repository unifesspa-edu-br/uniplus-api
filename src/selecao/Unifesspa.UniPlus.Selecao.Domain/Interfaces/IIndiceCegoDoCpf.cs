namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using ValueObjects;

/// <summary>Calcula o índice cego dos CPFs, o mesmo com que a inscrição foi gravada.</summary>
public interface IIndiceCegoDoCpf
{
    /// <summary>
    /// O índice de cada CPF distinto informado (onze dígitos). O cálculo é uma chamada ao cofre de
    /// chaves por CPF, por isso é feito em paralelo limitado e antes de qualquer trava de banco.
    /// </summary>
    Task<IReadOnlyDictionary<string, IndiceCegoDoCpf>> CalcularAsync(
        IReadOnlyCollection<string> cpfs, CancellationToken cancellationToken = default);
}
