namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

/// <summary>
/// A visão do catálogo dos fatos citados pelo processo (UNI-REQ-0078), tal como a cópia congelada com ele:
/// o código da fase do cronograma em que cada fato fica conhecido, os fatos declarados, que só existem se
/// algum formulário os coleta, e, para cada fato derivado, os fatos de que o valor dele depende. O fato que
/// não é declarado nem tem formulário produtor é resolvido pelo sistema na fase do catálogo. Alterar o cadastro de fatos depois da cópia não muda esta visão.
/// </summary>
public sealed record CatalogoDosFatosCitados(
    IReadOnlyDictionary<string, string> FaseDoCatalogoPorFato,
    IReadOnlyCollection<string> FatosDeclarados,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> DependenciasDosDerivados);
