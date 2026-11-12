namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// A trava da pessoa do beneficiário antes de mexer numa concessão de perfil de governança. Só a governança participa da
/// incompatibilidade dos papéis; o perfil comum não trava a pessoa, o que só acrescentaria disputa.
/// </summary>
internal static class TravaDoBeneficiario
{
    public static Task TravarBeneficiarioAsync(
        this IIncompatibilidadeDosPapeis papeis, Concessao concessao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(papeis);
        ArgumentNullException.ThrowIfNull(concessao);

        return PerfisDeSistema.EhDeGovernanca(concessao.PerfilId)
            ? papeis.TravarAsync([new Identidade(concessao.BeneficiarioEmissor, concessao.BeneficiarioSub)], cancellationToken)
            : Task.CompletedTask;
    }
}
