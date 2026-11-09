namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que a entrada em vigor de uma versão nova do perfil fez ao pedido que o seguia.</summary>
public enum DesfechoDaTrocaDeVersao
{
    /// <summary>O pedido não estava ativo: nada mudou e nenhuma troca se grava.</summary>
    Inalterado = 1,

    /// <summary>O pedido passou para a versão nova e segue as mesmas etapas.</summary>
    VersaoAtualizada = 2,

    /// <summary>A aprovação do gestor perdeu o efeito: o pedido voltou ao gestor, na versão nova.</summary>
    VoltouAoGestor = 3,

    /// <summary>A versão nova acrescenta permissão: o pedido foi encerrado e o solicitante o refaz depois.</summary>
    Devolvido = 4,
}

/// <summary>Código textual canônico de <see cref="DesfechoDaTrocaDeVersao"/>, o valor gravado e exposto.</summary>
public static class DesfechoDaTrocaDeVersaoCodigo
{
    public const string VersaoAtualizada = "versao_atualizada";
    public const string VoltouAoGestor = "voltou_ao_gestor";
    public const string Devolvido = "devolvido";

    public static string ToCodigo(this DesfechoDaTrocaDeVersao desfecho) => desfecho switch
    {
        DesfechoDaTrocaDeVersao.VersaoAtualizada => VersaoAtualizada,
        DesfechoDaTrocaDeVersao.VoltouAoGestor => VoltouAoGestor,
        DesfechoDaTrocaDeVersao.Devolvido => Devolvido,
        _ => throw new ArgumentOutOfRangeException(nameof(desfecho), desfecho, null),
    };

    public static DesfechoDaTrocaDeVersao FromCodigo(string codigo) => codigo switch
    {
        VersaoAtualizada => DesfechoDaTrocaDeVersao.VersaoAtualizada,
        VoltouAoGestor => DesfechoDaTrocaDeVersao.VoltouAoGestor,
        Devolvido => DesfechoDaTrocaDeVersao.Devolvido,
        _ => throw new ArgumentOutOfRangeException(nameof(codigo), codigo, null),
    };
}
