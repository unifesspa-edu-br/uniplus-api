namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Erros que os handlers do pedido produzem por si: os que decorrem do estado do repositório, do token e do acesso.</summary>
internal static class PedidoHandlerErros
{
    public static DomainError NaoEncontrado() =>
        new(PedidoErrorCodes.NaoEncontrado, "Pedido de concessão não encontrado.");

    public static DomainError AutorNaoIdentificado() =>
        new(PedidoErrorCodes.AutorNaoIdentificado,
            "Não foi possível identificar quem executa a operação; nada foi registrado.");

    public static DomainError IdentidadeIncompleta() =>
        new(PedidoErrorCodes.IdentidadeIncompleta,
            "O token não identifica o solicitante por completo; autentique-se de novo.");

    public static DomainError SemPermissao() =>
        new(PedidoErrorCodes.SemPermissao, "Token autenticado, mas sem permissão para esta operação.");

    public static DomainError SolicitanteNaoInstitucional() =>
        new(PedidoErrorCodes.SolicitanteNaoInstitucional,
            "Só quem tem e-mail institucional verificado, ou a permissão de manter perfis, lê os perfis que se podem pedir.");

    public static DomainError EtapaInvalida() =>
        new(PedidoErrorCodes.EtapaInvalida,
            "O pedido não aguarda a etapa que este aprovador decide.");

    public static DomainError UnidadeInexistente(Guid unidadeId) =>
        new(PedidoErrorCodes.UnidadeInexistente, $"A unidade {unidadeId} do escopo não existe.");

    public static DomainError PedidoDuplicado() =>
        new(PedidoErrorCodes.PedidoDuplicado,
            "Já existe pedido ativo igual para este beneficiário, perfil e escopo.");

    public static DomainError JaTemConcessao() =>
        new(PedidoErrorCodes.JaTemConcessao,
            "O beneficiário já tem concessão ativa deste perfil neste escopo.");

    public static DomainError AnexoNaoVerificado() =>
        new(PedidoErrorCodes.AnexoNaoVerificado, "O anexo só se lê depois de verificado.");

    public static DomainError ConflitoDeConcorrencia() =>
        new(PedidoErrorCodes.ConflitoDeConcorrencia,
            "O pedido foi alterado por outra operação. Consulte o estado atual antes de repetir.");
}
