namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Uma concessão a rever. Não copia nada da concessão: perfil, beneficiário e escopo são lidos dela. O resultado é
/// escrito uma vez; sem ele o item está pendente.
/// </summary>
public sealed class ItemDaRevisao : EntityBase
{
    public Guid RevisaoId { get; private set; }

    public Guid ConcessaoId { get; private set; }

    /// <summary>Quem decide o item: a proteção de dados, ou os gestores de acessos quando o beneficiário é a própria proteção de dados.</summary>
    public TipoDeGovernanca Decide { get; private set; }

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

    internal static ItemDaRevisao Para(Guid revisaoId, ConcessaoARever concessao) =>
        new()
        {
            RevisaoId = revisaoId,
            ConcessaoId = concessao.ConcessaoId,
            Decide = concessao.BeneficiarioTemProtecaoDeDados
                ? TipoDeGovernanca.GestorDeAcessos
                : TipoDeGovernanca.ProtecaoDeDados,
        };

    internal void Decidir(ResultadoDoItemDaRevisao resultado, Identidade decisor, DateTimeOffset instante)
    {
        Resultado = resultado;
        DecididoPorEmissor = decisor.Emissor;
        DecididoPorSub = decisor.Sub;
        DecididoPorNome = decisor.Nome;
        DecididoEm = instante;
    }
}
