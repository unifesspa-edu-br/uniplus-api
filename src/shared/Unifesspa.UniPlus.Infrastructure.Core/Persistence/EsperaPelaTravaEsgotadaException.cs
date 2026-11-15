namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>
/// A espera pela trava de uma linha passou do limite configurado porque outra operação a retém. O banco aborta a
/// transação da operação que esperava, então nada dela foi gravado, e a borda a traduz em indisponibilidade temporária.
/// </summary>
public sealed class EsperaPelaTravaEsgotadaException(Exception causa)
    : Exception("A trava continuou retida por outra operação além do limite de espera.", causa);
