namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As recusas de negócio que os comandos do tipo de conceito decidem depois de consultar o cadastro.</summary>
internal static class RecusasDoTipoConceito
{
    public static DomainError NaoEncontrado() => new(TipoConceitoErrorCodes.NaoEncontrado, "Tipo de conceito não encontrado.");

    public static DomainError ConflitoDeConcorrencia() => new(
        TipoConceitoErrorCodes.ConflitoDeConcorrencia,
        "Este tipo de conceito foi alterado concorrentemente. Tente novamente.");

    public static DomainError CodigoJaExiste() => new(
        TipoConceitoErrorCodes.CodigoJaExiste,
        "Já existe um tipo de conceito com o código informado.");
}
