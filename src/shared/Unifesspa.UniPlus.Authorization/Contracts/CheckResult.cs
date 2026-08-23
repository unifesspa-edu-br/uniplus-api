namespace Unifesspa.UniPlus.Authorization.Contracts;

using System.ComponentModel;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Authorization.ValueObjects;

/// <summary>
/// Resultado de uma verificação isolada da decisão de autorização (ADR-0078):
/// passou ou não, o motivo estruturado quando não passou e — quando a
/// verificação é a que seleciona a concessão — qual concessão a sustentou.
/// </summary>
/// <remarks>
/// O invariante (aprovado nunca carrega motivo; reprovado nunca carrega
/// concessão) é garantido <b>por construção</b>: as duas fábricas são o único
/// caminho público, espelhando <see cref="AuthorizationDecision"/>.
/// </remarks>
public sealed record CheckResult
{
    /// <summary>A verificação passou.</summary>
    public bool Passou { get; }

    /// <summary>Motivo da reprovação — presente se, e somente se, reprovada.</summary>
    public MotivoNegativa? Motivo { get; }

    /// <summary>
    /// Concessão que sustentou a aprovação. Presente apenas na verificação que
    /// seleciona a concessão; as demais aprovam sem concessão a registrar.
    /// </summary>
    public EffectiveGrant? GrantSelecionado { get; }

    private CheckResult(bool passou, MotivoNegativa? motivo, EffectiveGrant? grantSelecionado)
    {
        Passou = passou;
        Motivo = motivo;
        GrantSelecionado = grantSelecionado;
    }

    /// <summary>
    /// Verificação aprovada, opcionalmente indicando a concessão selecionada.
    /// </summary>
    public static CheckResult Aprovado(EffectiveGrant? grantSelecionado = null)
        => new(passou: true, motivo: null, grantSelecionado: grantSelecionado);

    /// <summary>
    /// Verificação reprovada, com um motivo do conjunto fechado. Rejeita valor
    /// fora do conjunto (<i>cast</i> de inteiro arbitrário) pelo mesmo motivo de
    /// <see cref="DenyReason.De"/>: o motivo é produzido pelo motor de decisão,
    /// não por dado externo — um código desconhecido é violação de contrato de
    /// programação, não entrada inválida.
    /// </summary>
    public static CheckResult Reprovado(MotivoNegativa motivo)
    {
        if (!Enum.IsDefined(motivo))
        {
            throw new InvalidEnumArgumentException(nameof(motivo), (int)motivo, typeof(MotivoNegativa));
        }

        return new(passou: false, motivo: motivo, grantSelecionado: null);
    }
}
