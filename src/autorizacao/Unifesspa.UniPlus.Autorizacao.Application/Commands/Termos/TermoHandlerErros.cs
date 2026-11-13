namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Termos;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Erros que os handlers do termo de responsabilidade produzem por si: os do repositório, do token e da gravação.</summary>
internal static class TermoHandlerErros
{
    public static DomainError SemVersaoVigente() =>
        new(TermoErrorCodes.SemVersaoVigente, "Nenhuma versão do termo de responsabilidade foi publicada até agora.");

    public static DomainError VersaoNaoEncontrada() =>
        new(TermoErrorCodes.VersaoNaoEncontrada, "A versão do termo de responsabilidade informada não existe.");

    public static DomainError AutorNaoIdentificado() =>
        new(TermoErrorCodes.AutorNaoIdentificado, "Não foi possível identificar quem publica o termo; nada foi registrado.");

    public static DomainError ConflitoDeConcorrencia() =>
        new(TermoErrorCodes.ConflitoDeConcorrencia, "O termo foi alterado por outra operação ao mesmo tempo; repita a requisição.");
}
