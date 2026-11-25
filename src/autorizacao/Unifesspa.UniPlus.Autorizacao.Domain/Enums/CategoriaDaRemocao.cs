namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Por que as permissões foram removidas do perfil, quando quem remove quer dizer.</summary>
public enum CategoriaDaRemocao
{
    [CodigoDeContrato("INCIDENTE", "Incidente")]
    Incidente = 1,

    [CodigoDeContrato("ERRO_DE_COMPOSICAO", "Erro de composição")]
    ErroDeComposicao = 2,

    [CodigoDeContrato("MUDANCA_DE_FUNCAO", "Mudança de função")]
    MudancaDeFuncao = 3,
}
