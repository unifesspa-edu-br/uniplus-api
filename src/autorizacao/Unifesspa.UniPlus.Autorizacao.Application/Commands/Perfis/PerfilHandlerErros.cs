namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Erros que os handlers produzem por si: os que decorrem do estado do repositório.</summary>
internal static class PerfilHandlerErros
{
    public static DomainError NaoEncontrado(Guid id) =>
        new(PerfilErrorCodes.NaoEncontrado, $"Perfil de acesso {id} não encontrado.");

    public static DomainError NomeDuplicado() =>
        new(PerfilErrorCodes.NomeDuplicado, "Já existe um perfil de acesso com o nome informado.");

    public static DomainError ConflitoDeConcorrencia() =>
        new(PerfilErrorCodes.ConflitoDeConcorrencia,
            "O perfil de acesso foi alterado por outra operação. Consulte o estado atual antes de repetir.");
}
