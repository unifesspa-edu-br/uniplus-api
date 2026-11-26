namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Um documento do conjunto padrão que o modelo exige do processo (UNI-REQ-0026): o tipo do catálogo e
/// a fase canônica pelos códigos, que o processo resolve ao aplicar o modelo, as condições sobre fatos
/// que o tornam exigido (ausentes, vale para todo candidato) e o indeferimento, que diz se é obrigatório,
/// a consequência de o candidato não o apresentar e as bases legais, e os requisitos, que dizem o prazo de
/// emissão e a validade escolhidos para ele. É sempre do formulário do próprio modelo. Com <paramref name="PorOpcao"/>,
/// o processo recebe uma exigência por opção ofertada, cada uma condicionada à declaração dela.
/// </summary>
public sealed record DocumentoExigidoDoModelo(
    string FaseCodigo,
    string TipoDocumentoCodigo,
    PredicadoDnf? Quando,
    IndeferimentoDoDocumentoDoModelo Indeferimento,
    RequisitosDoDocumentoDoModelo Requisitos,
    ExigenciaPorOpcao? PorOpcao = null)
{
    /// <summary>Os fatos que as condições do documento citam, inclusive o das opções que o multiplicam.</summary>
    public IReadOnlyCollection<string> FatosCitados =>
        PorOpcao is null
            ? Quando?.FatosCitados ?? []
            : [.. (Quando?.FatosCitados ?? []).Append(PorOpcao.FatoCodigo).Distinct(StringComparer.Ordinal)];

    /// <summary>A condição que torna o documento exigido, com o caminho do campo dela na entrada.</summary>
    public IReadOnlyList<PredicadoNaEntrada> Predicados() => [new("quando", Quando)];
}
