namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Quantos corretores distintos a regra de consolidação congelada de cada etapa exige. O número é
/// dado da versão da regra no rol e chega pronto da Application: o agregado guarda só a referência
/// à regra, e ler o rol é I/O (ADR-0042).
/// </summary>
public sealed record CorretoresDasEtapas
{
    private readonly Dictionary<Guid, int> _porEtapa;

    /// <summary>Nenhuma etapa exige corretor: o mínimo de integrantes fica sendo o quórum.</summary>
    public static CorretoresDasEtapas Nenhum { get; } = new(new Dictionary<Guid, int>());

    public CorretoresDasEtapas(IReadOnlyDictionary<Guid, int> porEtapa)
    {
        ArgumentNullException.ThrowIfNull(porEtapa);
        _porEtapa = new Dictionary<Guid, int>(porEtapa);
    }

    /// <summary>Etapa sem regra de consolidação não exige corretor.</summary>
    public int De(EtapaProcesso etapa)
    {
        ArgumentNullException.ThrowIfNull(etapa);
        return _porEtapa.GetValueOrDefault(etapa.Id);
    }
}
