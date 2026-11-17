namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Errors;

/// <summary>As recusas que mais de um handler da Instituição devolve.</summary>
internal static class InstituicaoHandlerErros
{
    internal static DomainError NaoEncontrada() =>
        new(InstituicaoErrorCodes.NaoEncontrada, "Instituição não encontrada.");

    internal static DomainError JaExisteInstituicaoViva() =>
        new(
            InstituicaoErrorCodes.JaExisteInstituicaoViva,
            "Já existe uma Instituição cadastrada — cada instância da plataforma atende uma única instituição.");
}
