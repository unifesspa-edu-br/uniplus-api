namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Uma das duas pontas de uma <see cref="PrecedenciaFase"/>: o campo em que o código chega, os três códigos
/// de erro que a leitura pode recusar e o rótulo que a mensagem usa. Lê o código informado e o devolve sem
/// alterações, ou a recusa já ligada ao campo (ADR-0125).
/// </summary>
internal sealed record PontaDaPrecedencia(
    string Campo,
    string CodigoObrigatorio,
    string CodigoFormatoInvalido,
    string CodigoForaDoConjuntoCanonico,
    string Rotulo)
{
    internal static readonly PontaDaPrecedencia Antecessora = new(
        "antecessoraCodigo",
        PrecedenciaFaseErrorCodes.AntecessoraCodigoObrigatorio,
        PrecedenciaFaseErrorCodes.AntecessoraCodigoFormatoInvalido,
        PrecedenciaFaseErrorCodes.AntecessoraForaDoConjuntoCanonico,
        "Código da fase antecessora");

    internal static readonly PontaDaPrecedencia Sucessora = new(
        "sucessoraCodigo",
        PrecedenciaFaseErrorCodes.SucessoraCodigoObrigatorio,
        PrecedenciaFaseErrorCodes.SucessoraCodigoFormatoInvalido,
        PrecedenciaFaseErrorCodes.SucessoraForaDoConjuntoCanonico,
        "Código da fase sucessora");

    internal Result<string> Ler(string? valor)
    {
        string normalizado = valor ?? string.Empty;
        DomainError? recusa = Recusar(normalizado);
        return recusa is null
            ? Result<string>.Success(normalizado)
            : Result<string>.ValidationFailure([new FieldError(Campo, recusa)]);
    }

    // A mensagem de fora do conjunto canônico é genérica de propósito (ADR-0023): nunca ecoa o dado rejeitado.
    private DomainError? Recusar(string normalizado) => normalizado switch
    {
        { Length: 0 } => Recusa(CodigoObrigatorio, "é obrigatório."),
        _ when !CodigoFase.EhValido(normalizado) => Recusa(
            CodigoFormatoInvalido,
            "deve iniciar com letra maiúscula e conter letras maiúsculas, dígitos e sublinhados separados."),
        _ when !FaseCanonicaCatalogo.EhCanonico(normalizado) => Recusa(
            CodigoForaDoConjuntoCanonico,
            "não pertence ao conjunto canônico."),
        _ => null,
    };

    private DomainError Recusa(string codigo, string complemento) => new(codigo, $"{Rotulo} {complemento}");
}
