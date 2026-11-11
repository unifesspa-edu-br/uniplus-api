namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O desfecho de uma avaliação de acesso que também distingue o token que não identifica a pessoa. O valor padrão é
/// <see cref="Negado"/>, para que um desfecho não avaliado nunca conceda acesso.
/// </summary>
internal enum AcessoAvaliado
{
    Negado = 0,
    Permitido,
    SemAutorIdentificado,
}

internal static class AcessoAvaliadoExtensions
{
    /// <summary>A recusa correspondente ao desfecho; nula quando o acesso é permitido.</summary>
    public static DomainError? Recusa(this AcessoAvaliado acesso, Func<DomainError> negado)
    {
        ArgumentNullException.ThrowIfNull(negado);

        return acesso switch
        {
            AcessoAvaliado.Permitido => null,
            AcessoAvaliado.SemAutorIdentificado => PerfilHandlerErros.AutorNaoIdentificado(),
            _ => negado(),
        };
    }
}
