namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>A importação do resultado declarada pelo processo: os relatórios, cada um pelo leiaute, na ordem declarada, e a primeira fase depois da importação, nula enquanto não declarada.</summary>
public sealed record ImportacaoDoResultadoDto(IReadOnlyList<RelatorioDaImportacaoDto> Relatorios, Guid? PrimeiraFaseDepoisDaImportacaoId);

public sealed record RelatorioDaImportacaoDto(string LeiauteCodigo, string LeiauteVersao);
