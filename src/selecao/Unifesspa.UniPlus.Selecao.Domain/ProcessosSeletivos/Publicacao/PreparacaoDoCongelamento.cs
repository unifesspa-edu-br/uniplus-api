namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a Application já conferiu e o agregado refaz antes de canonicalizar a versão: as cópias que os
/// itens tiram do catálogo, a sigla e o nome das unidades responsáveis das fases e a proteção vigente das
/// exigências. A canonicalização e os gates leem a cópia já refeita.
/// </summary>
/// <param name="CopiasDoCatalogo">A cópia que o catálogo de hoje tem de cada fato, por código.</param>
/// <param name="FatosJaColetados">Os fatos que alguma versão publicada coletou, por código ordinal.</param>
/// <param name="UnidadesVivas">A unidade responsável viva de cada unidade citada pelas fases.</param>
public sealed record PreparacaoDoCongelamento(
    IReadOnlyDictionary<string, CopiaDoItemNoCatalogo> CopiasDoCatalogo,
    IReadOnlySet<string> FatosJaColetados,
    IReadOnlyDictionary<Guid, UnidadeResponsavelSnapshot> UnidadesVivas,
    ProtecaoVigenteDasExigencias ProtecaoVigente);
