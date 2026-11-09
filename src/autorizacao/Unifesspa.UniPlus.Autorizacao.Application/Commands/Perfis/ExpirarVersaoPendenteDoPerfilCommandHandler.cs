namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Expira a pendente vencida de um perfil sob a trava dele, na transação do handler: quem chega depois da aprovação, ou
/// antes dela, encontra o estado que a outra deixou. O prazo é materializado por <see cref="OperacaoSobreOPerfil"/>.
/// </summary>
public static class ExpirarVersaoPendenteDoPerfilCommandHandler
{
    public static Task<Result<bool>> Handle(
        ExpirarVersaoPendenteDoPerfilCommand command, OperacaoSobreOPerfil operacao, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(operacao);

        return operacao.ExecutarSemChaveAsync(
            command.PerfilId,
            null,
            static (_, contexto) => Task.FromResult(Result<bool>.Success(contexto.VersaoExpirada is not null)),
            cancellationToken);
    }
}
