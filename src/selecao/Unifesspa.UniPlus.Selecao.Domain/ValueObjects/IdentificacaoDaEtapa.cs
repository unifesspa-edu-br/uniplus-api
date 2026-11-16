namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Enums;

/// <summary>
/// Quem a <see cref="Entities.EtapaProcesso"/> é no processo: o nome editorial, o caráter, o tipo de
/// etapa já resolvido contra o cadastro, a ordem e o código da fase onde acontece. É entrada de
/// fábrica de vida curta, superficialmente imutável: carrega a instância de
/// <see cref="TipoEtapaSnapshot"/> que a etapa passa a possuir e nunca é guardada pela entidade, que
/// copia os valores. Só existe depois de o tipo ser resolvido; a conferência da forma, que a
/// Application faz antes disso, recebe os valores soltos.
/// </summary>
public sealed record IdentificacaoDaEtapa(
    string Nome,
    CaraterEtapa Carater,
    TipoEtapaSnapshot TipoEtapa,
    int? Ordem,
    string? FaseCodigo)
{
    /// <summary>A identificação como a etapa a guarda: nome aparado e código da fase normalizado.</summary>
    public IdentificacaoDaEtapa Normalizada() =>
        this with { Nome = Nome.Trim(), FaseCodigo = CodigoDeFase.Normalizado(FaseCodigo) };
}
