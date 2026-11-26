namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalDoFada;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As recusas de negócio que os comandos da base legal do FADA decidem depois de consultar o cadastro.</summary>
internal static class RecusasDaBaseLegalDoFada
{
    public static DomainError NaoEncontrada() => new(
        BaseLegalDoFadaErrorCodes.NaoEncontrado,
        "Base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) não encontrada.");

    public static DomainError CodigoJaExiste(BaseLegalDoFada baseLegal) => new(
        BaseLegalDoFadaErrorCodes.CodigoJaExiste,
        $"Já existe uma base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) com o código {baseLegal.Codigo.Valor}: o mesmo ato já está cadastrado.");
}
