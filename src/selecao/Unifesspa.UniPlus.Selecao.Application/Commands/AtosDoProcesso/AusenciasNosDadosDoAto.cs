namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// As recusas de campo obrigatório ausente do pedido de publicação de ato, uma por campo: o campo e a mensagem que a
/// resposta traz. A forma do pedido as junta na ordem dos campos, junto das demais causas.
/// </summary>
internal static class AusenciasNosDadosDoAto
{
    public const string CampoDoTipo = "tipoAtoCodigo";
    public const string CampoDaData = "dataDoAto";
    public const string CampoDoAssinante = "assinante";
    public const string CampoDoDocumentoPrincipal = "documentoPrincipalId";

    public static FieldError Tipo() => Obrigatorio(CampoDoTipo, "Informe o tipo do ato.");

    public static FieldError Data() => Obrigatorio(CampoDaData, "Informe a data do ato declarada pelo documento.");

    public static FieldError Assinante() => Obrigatorio(CampoDoAssinante, "Informe quem assina o ato.");

    public static FieldError DocumentoPrincipal() => Obrigatorio(CampoDoDocumentoPrincipal, "Informe o documento principal do ato.");

    public static FieldError DocumentoDoAnexo(int posicao) =>
        Obrigatorio($"anexos[{posicao}].documentoId", "Informe o documento do anexo.");

    private static FieldError Obrigatorio(string campo, string mensagem) =>
        new(campo, new DomainError(CodigosDeValidacao.Obrigatorio, mensagem));
}
