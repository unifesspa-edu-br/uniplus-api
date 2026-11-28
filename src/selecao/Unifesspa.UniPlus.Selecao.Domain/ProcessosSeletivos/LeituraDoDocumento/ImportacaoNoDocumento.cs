namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>
/// A importação do resultado como o documento a traz, copiada sem conferir regra: os relatórios, cada um com o código e a versão do
/// leiaute como foram escritos, e a chave da primeira fase depois da importação, que cita a fase do cronograma do mesmo documento.
/// </summary>
public sealed record ImportacaoNoDocumento(IReadOnlyList<RelatorioInformado> Relatorios, string? PrimeiraFaseDepoisDaImportacaoChave);
