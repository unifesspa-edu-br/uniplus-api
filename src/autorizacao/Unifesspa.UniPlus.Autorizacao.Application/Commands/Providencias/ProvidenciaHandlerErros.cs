namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Erros que os handlers da providência produzem por si: os do repositório, do token e da gravação.</summary>
internal static class ProvidenciaHandlerErros
{
    public static DomainError NaoEncontrada() =>
        new(ProvidenciaErrorCodes.NaoEncontrada, "Providência não encontrada.");

    public static DomainError AutorNaoIdentificado() =>
        new(ProvidenciaErrorCodes.AutorNaoIdentificado, "Não foi possível identificar quem executa a operação; nada foi registrado.");

    public static DomainError ConflitoDeConcorrencia() =>
        new(ProvidenciaErrorCodes.ConflitoDeConcorrencia,
            "A providência foi alterada por outra operação. Consulte o estado atual antes de repetir.");
}
