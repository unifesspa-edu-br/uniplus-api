namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A aprovação que não se completou e devolve o pedido: o resultado que fica registrado e a recusa que o decisor recebe.</summary>
internal sealed record DevolucaoNaAprovacao(ResultadoDaDecisao Resultado, DomainError Erro)
{
    /// <summary>O pedido volta ao gestor; no outro caso, é devolvido ao solicitante.</summary>
    internal bool VoltaAoGestor => Resultado != ResultadoDaDecisao.TermoExigido;

    internal static DevolucaoNaAprovacao PerfilAlterado() => new(
        ResultadoDaDecisao.PerfilAlterado,
        new DomainError(PedidoErrorCodes.PerfilAlterado, "O perfil mudou depois da aprovação do gestor; o pedido voltou a aguardar o gestor."));

    internal static DevolucaoNaAprovacao PrazoVencido() => new(
        ResultadoDaDecisao.PrazoVencido,
        new DomainError(PedidoErrorCodes.PrazoVencido, "O prazo definido pelo gestor venceu antes desta aprovação; o pedido voltou ao gestor para definir outro."));

    internal static DevolucaoNaAprovacao TermoExigido() => new(
        ResultadoDaDecisao.TermoExigido,
        new DomainError(
            PedidoErrorCodes.TermoExigido,
            "O perfil passou a dar acesso a dado pessoal ou sensível depois da criação do pedido, que não tem termo de responsabilidade fixado. O pedido foi devolvido; refaça-o para aceitar o termo."));
}
