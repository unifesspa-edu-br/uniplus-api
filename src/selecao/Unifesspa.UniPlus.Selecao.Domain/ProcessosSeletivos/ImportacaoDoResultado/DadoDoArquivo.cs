namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

/// <summary>Um dado que o leiaute declara trazer no arquivo: o fato do catálogo e a garantia de que vem em toda linha.</summary>
public sealed record DadoDoArquivo(string FatoCodigo, PresencaDoDadoNoArquivo Presenca);
