namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que a entrada em vigor de uma versão nova do perfil fez ao pedido que o seguia.</summary>
public enum DesfechoDaTrocaDeVersao
{
    /// <summary>O pedido não estava ativo: nada mudou e nenhuma troca se grava.</summary>
    [CodigoDeContrato("INALTERADO", "Inalterado")]
    Inalterado = 1,

    /// <summary>O pedido passou para a versão nova e segue as mesmas etapas.</summary>
    [CodigoDeContrato("VERSAO_ATUALIZADA", "Versão atualizada")]
    VersaoAtualizada = 2,

    /// <summary>A aprovação do gestor perdeu o efeito: o pedido voltou ao gestor, na versão nova.</summary>
    [CodigoDeContrato("VOLTOU_AO_GESTOR", "Voltou ao gestor")]
    VoltouAoGestor = 3,

    /// <summary>A versão nova acrescenta permissão: o pedido foi encerrado e o solicitante o refaz depois.</summary>
    [CodigoDeContrato("DEVOLVIDO", "Devolvido")]
    Devolvido = 4,
}
