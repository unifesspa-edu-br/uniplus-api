namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Revisoes;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Erros que os handlers da revisão produzem por si: os que decorrem do estado do repositório, do token e do acesso.</summary>
internal static class RevisaoHandlerErros
{
    public static DomainError NaoEncontrada() =>
        new(RevisaoErrorCodes.NaoEncontrada, "Revisão de acessos não encontrada.");

    public static DomainError ItemNaoEncontrado() =>
        new(RevisaoErrorCodes.ItemNaoEncontrado, "Item da revisão não encontrado.");

    public static DomainError AutorNaoIdentificado() =>
        new(RevisaoErrorCodes.AutorNaoIdentificado,
            "Não foi possível identificar quem executa a operação; nada foi registrado.");

    public static DomainError IdentidadeIncompleta() =>
        new(RevisaoErrorCodes.IdentidadeIncompleta,
            "O token não identifica o solicitante por completo; autentique-se de novo.");

    public static DomainError SemPermissao() =>
        new(RevisaoErrorCodes.SemPermissao, "Token autenticado, mas sem permissão para esta operação.");

    public static DomainError JaHaEmAndamento() =>
        new(RevisaoErrorCodes.JaHaEmAndamento,
            "Já existe revisão em andamento; conclua as decisões dela antes de abrir outra.");

    public static DomainError DecisorNaoDecideOItem() =>
        new(RevisaoErrorCodes.DecisorNaoDecideOItem,
            "O item cabe à outra governança: a proteção de dados decide os acessos comuns, e os gestores de acessos, os da proteção de dados.");

    public static DomainError ConflitoDeConcorrencia() =>
        new(RevisaoErrorCodes.ConflitoDeConcorrencia,
            "A revisão foi alterada por outra operação. Consulte o estado atual antes de repetir.");
}
