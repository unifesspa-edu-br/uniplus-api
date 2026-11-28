namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Errors;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// O identificador legível que o pedido declara, e a recusa de quando outro processo já o usa. A ausência é erro
/// nomeado, e não regra do validator, pelo mesmo motivo da localidade: o consumidor precisa da página que explica a
/// causa. Toda causa aponta o campo do pedido e nenhuma mensagem repete o valor recebido.
/// </summary>
internal static class IdentificadorLegivelDeclarado
{
    internal const string Campo = "identificadorLegivel";

    internal static Result<IdentificadorLegivel> Conferir(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Result<IdentificadorLegivel>.ValidationFailure([NoCampo(new DomainError(
                ProcessoSeletivoErrorCodes.IdentificadorLegivelAusente,
                "O identificador legível é obrigatório."))]);
        }

        Result<IdentificadorLegivel> criado = IdentificadorLegivel.Criar(valor);

        return criado.IsFailure
            ? Result<IdentificadorLegivel>.ValidationFailure([.. criado.Errors.Select(static causa => causa with { Field = Campo })])
            : criado;
    }

    internal static DomainError EmUso() => new(
        ProcessoSeletivoErrorCodes.IdentificadorLegivelEmUso,
        "O identificador legível já é usado por outro processo seletivo.");

    internal static FieldError RecusaDoUso() => NoCampo(EmUso());

    private static FieldError NoCampo(DomainError erro) => new(Campo, erro);
}
