namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Passa o registro do ato do processo a registrado quando o Publicações confirma o ato.
/// </summary>
/// <remarks>
/// <para>
/// <b>Handler próprio, e não um ramo de <c>DivulgarCertameAoRegistrarAtoHandler</c>.</b> Aquele parte
/// da versão que o ato criou e sai cedo quando não há versão; a transição vale para todo ato do
/// processo. O Wolverine processa a confirmação na mesma cadeia dos dois handlers: se a divulgação
/// falha, a cadeia é reprocessada inteira e esta transição é reaplicada junto.
/// </para>
/// <para>
/// <b>Idempotente.</b> A confirmação é entregue pelo menos uma vez; a repetição encontra o registro
/// já confirmado e não grava nada. Ato sem registro — o de outro domínio — é ignorado: o registro
/// central serve a mais de um emissor.
/// </para>
/// </remarks>
public static class ConfirmarRegistroDoAtoDoProcessoHandler
{
    public static async Task Handle(
        AtoNormativoRegistrado mensagem,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        AtoDoProcesso? ato = await atoDoProcessoRepository
            .ObterParaMutacaoAsync(mensagem.AtoId, cancellationToken)
            .ConfigureAwait(false);

        if (ato is null || !ato.ConfirmarRegistro(timeProvider))
        {
            return;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
    }
}
