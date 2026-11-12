namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Uma concessão a rever. Não copia nada da concessão: perfil, beneficiário e escopo são lidos dela. Quem decide não se
/// fixa na abertura: é avaliado no instante da decisão (ADR-0162). O resultado e o tipo de governança com que foi decidido
/// são escritos uma vez; sem eles o item está pendente.
/// </summary>
public sealed class ItemDaRevisao : EntityBase
{
    public Guid RevisaoId { get; private set; }

    public Guid ConcessaoId { get; private set; }

    /// <summary>A governança com que o item foi decidido; nula enquanto o item está pendente.</summary>
    public TipoDeGovernanca? DecididoComo { get; private set; }

    /// <summary>Nulo enquanto o item está pendente.</summary>
    public ResultadoDoItemDaRevisao? Resultado { get; private set; }

    public string? DecididoPorEmissor { get; private set; }

    public string? DecididoPorSub { get; private set; }

    public string? DecididoPorNome { get; private set; }

    public DateTimeOffset? DecididoEm { get; private set; }

    public bool Pendente => Resultado is null;

    public Identidade? DecididoPor =>
        DecididoPorEmissor is null || DecididoPorSub is null ? null : new(DecididoPorEmissor, DecididoPorSub, DecididoPorNome);

    // Construtor de materialização do EF Core.
    private ItemDaRevisao()
    {
    }

    /// <summary>
    /// Quem decide o item de um beneficiário: os gestores de acessos, quando ele é Representante do controlador no
    /// instante da decisão; o Representante, nos demais casos.
    /// </summary>
    public static TipoDeGovernanca QuemDecide(bool beneficiarioERepresentante) =>
        beneficiarioERepresentante ? TipoDeGovernanca.GestorDeAcessos : TipoDeGovernanca.RepresentanteDoControlador;

    internal static ItemDaRevisao Para(Guid revisaoId, Guid concessaoId) =>
        new()
        {
            RevisaoId = revisaoId,
            ConcessaoId = concessaoId,
        };

    internal void Decidir(ResultadoDoItemDaRevisao resultado, Identidade decisor, TipoDeGovernanca como, DateTimeOffset instante)
    {
        Resultado = resultado;
        DecididoComo = como;
        DecididoPorEmissor = decisor.Emissor;
        DecididoPorSub = decisor.Sub;
        DecididoPorNome = decisor.Nome;
        DecididoEm = instante;
    }
}
