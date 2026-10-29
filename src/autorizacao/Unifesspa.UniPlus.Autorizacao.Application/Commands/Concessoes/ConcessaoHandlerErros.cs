namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Erros que os handlers produzem por si: os que decorrem do estado do repositório.</summary>
internal static class ConcessaoHandlerErros
{
    public static DomainError NaoEncontrada(Guid id) =>
        new(ConcessaoErrorCodes.NaoEncontrada, $"Concessão de acesso {id} não encontrada.");

    public static DomainError AutorNaoIdentificado() =>
        new(ConcessaoErrorCodes.AutorNaoIdentificado,
            "Não foi possível identificar quem executa a operação; a revogação não foi registrada.");

    public static DomainError MinimoDeGestores() =>
        new(ConcessaoErrorCodes.MinimoDeGestores,
            "A revogação deixaria a plataforma com menos de dois gestores de acessos ativos. Designe outro gestor antes de revogar.");

    public static DomainError ConflitoDeConcorrencia() =>
        new(ConcessaoErrorCodes.ConflitoDeConcorrencia,
            "A concessão de acesso foi alterada por outra operação. Consulte o estado atual antes de repetir.");
}
