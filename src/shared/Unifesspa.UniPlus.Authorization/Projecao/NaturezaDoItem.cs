namespace Unifesspa.UniPlus.Authorization.Projecao;

/// <summary>
/// De que natureza é o item que uma permissão de consulta libera: o dado (campo e resposta) ou o arquivo (documento
/// enviado). Dado e arquivo têm sempre permissões à parte (ADR-0153).
/// </summary>
public enum NaturezaDoItem
{
    /// <summary>A permissão não participa da projeção: é uma ação decidida no próprio endpoint.</summary>
    Nenhuma = 0,

    /// <summary>Campo ou resposta.</summary>
    Dado = 1,

    /// <summary>Documento enviado.</summary>
    Arquivo = 2,
}
