namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

/// <summary>
/// Os prefixos dos códigos dos tipos de documento que o processo do cenário exige, um por classificação de proteção.
/// O código do tipo ganha um sufixo único, e o prefixo identifica a exigência entre as congeladas na publicação.
/// </summary>
internal static class TiposDeDocumentoDoCenario
{
    public const string Pessoal = "DOCUMENTO_PESSOAL";
    public const string Sensivel = "LAUDO_MEDICO";
    public const string Publico = "ARTIGO_PUBLICADO";
    public const string Interno = "ATA_INTERNA";
    public const string Identificador = "RG_IDENTIFICACAO";
}
