namespace Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Forma serializável do relatório de uma carga do ETL DNE (Story #674): é tanto o
/// que se persiste em <c>geo_importacao_execucao.relatorio_json</c> quanto o que o
/// endpoint admin devolve. <strong>Reference data público — nunca carrega PII.</strong>
/// </summary>
public sealed record RelatorioImportacaoDto(
    string VersaoDataset,
    int Lidos,
    int Inseridos,
    int Atualizados,
    int Orfaos,
    int Degradados,
    IReadOnlyList<TabelaImportacaoDto> Tabelas);

/// <summary>Contadores e amostras de divergência (sem PII) de uma tabela na carga.</summary>
public sealed record TabelaImportacaoDto(
    string Tabela,
    int Lidos,
    int Inseridos,
    int Atualizados,
    int IgnoradosSemChave,
    int Orfaos,
    int Duplicados,
    int ParsesDegradados,
    IReadOnlyList<string> Amostras);
