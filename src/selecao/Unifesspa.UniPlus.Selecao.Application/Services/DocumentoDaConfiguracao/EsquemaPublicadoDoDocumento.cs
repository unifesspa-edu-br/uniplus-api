namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>O JSON Schema publicado que descreve o documento de configuração, e o componente da especificação OpenAPI que o publica.</summary>
public static class EsquemaPublicadoDoDocumento
{
    /// <summary>O nome do componente da OpenAPI que traz o esquema do documento; os componentes das demais partes do esquema levam este nome como prefixo.</summary>
    public const string Componente = "DocumentoDaConfiguracao";

    public const string Identificador = "https://uniplus.unifesspa.edu.br/contracts/selecao/configuracao/v1/documento.schema.json";
}
