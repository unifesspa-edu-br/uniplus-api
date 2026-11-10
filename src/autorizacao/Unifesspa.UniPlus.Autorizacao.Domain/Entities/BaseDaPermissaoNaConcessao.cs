namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// A base de uma permissão numa concessão num instante da história dela. A última linha do par (concessão, código) é a
/// base em vigor, salvo se for de saída: então a base é o retrato da versão atual da concessão. Só se acrescenta: é
/// evidência forense (ADR-0063).
/// </summary>
public sealed class BaseDaPermissaoNaConcessao : IForensicEntity, IRegistroDaTrilhaDeContencao
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid PerfilId { get; private init; }

    public Guid ConcessaoId { get; private init; }

    public string Codigo { get; private init; } = string.Empty;

    /// <summary>Nula só na saída, que encerra a base.</summary>
    public ClassificacaoDeProtecao? Classificacao { get; private init; }

    /// <summary>Nulo só na saída, que encerra a base.</summary>
    public bool? DeUnidade { get; private init; }

    public OrigemDaBase Origem { get; private init; }

    /// <summary>A versão-alvo na migração e na saída, a reclassificação na liberação e o catálogo na conciliação.</summary>
    public Guid ReferenciaId { get; private init; }

    public int OrdinalLocal { get; private init; }

    public long OrdemNaTrilha { get; private set; }

    // Construtor de materialização do EF Core.
    private BaseDaPermissaoNaConcessao()
    {
    }

    public static BaseDaPermissaoNaConcessao Registrar(
        Guid perfilId, Guid concessaoId, string codigo, BaseDaConcessao baseNova, OrigemDaBase origem, Guid referenciaId, int ordinal)
    {
        ArgumentNullException.ThrowIfNull(baseNova);

        return Nova(perfilId, concessaoId, codigo, new Referencia(origem, referenciaId, ordinal), baseNova);
    }

    /// <summary>A saída da permissão da composição da concessão: encerra a base dela.</summary>
    public static BaseDaPermissaoNaConcessao DaSaida(Guid perfilId, Guid concessaoId, string codigo, Guid versaoAlvoId, int ordinal) =>
        Nova(perfilId, concessaoId, codigo, new Referencia(OrigemDaBase.Saida, versaoAlvoId, ordinal), null);

    private static BaseDaPermissaoNaConcessao Nova(Guid perfilId, Guid concessaoId, string codigo, Referencia referencia, BaseDaConcessao? valores) =>
        new()
        {
            PerfilId = perfilId,
            ConcessaoId = concessaoId,
            Codigo = codigo,
            Classificacao = valores?.Classificacao,
            DeUnidade = valores?.DeUnidade,
            Origem = referencia.Origem,
            ReferenciaId = referencia.Id,
            OrdinalLocal = referencia.Ordinal,
        };

    private sealed record Referencia(OrigemDaBase Origem, Guid Id, int Ordinal);
}
