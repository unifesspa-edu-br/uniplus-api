namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ProvaPresencial;

/// <summary>
/// A prova presencial como o documento a traz, copiada sem conferir regra: se o processo a tem e os municípios na ordem enviada,
/// cada um com o código do IBGE e a UF como foram escritos. O nome do município é só de leitura e não entra.
/// </summary>
public sealed record ProvaPresencialNoDocumento(bool TemProvaPresencial, IReadOnlyList<MunicipioDeProvaInformado> Municipios);
