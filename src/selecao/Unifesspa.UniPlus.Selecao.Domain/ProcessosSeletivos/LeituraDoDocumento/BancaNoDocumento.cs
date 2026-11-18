namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Uma banca que a fase requer, com a posição dela na lista enviada. O tipo, obrigatório no esquema, e as categorias do recorte vão pelo código de catálogo
/// como foi escrito; a regra de deliberação ausente é nula.
/// </summary>
public sealed record BancaNoDocumento(
    int PosicaoNoDocumento,
    string Chave,
    string TipoBancaCodigo,
    IReadOnlyList<string> CategoriasDocumentoCodigos,
    DeclaracaoDaDeliberacao? RegraDeDeliberacao);
