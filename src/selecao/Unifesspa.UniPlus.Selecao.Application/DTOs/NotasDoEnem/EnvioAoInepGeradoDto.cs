namespace Unifesspa.UniPlus.Selecao.Application.DTOs.NotasDoEnem;

/// <summary>O arquivo de envio ao INEP recém-gerado, com o nome sugerido para o download.</summary>
public sealed record EnvioAoInepGeradoDto(byte[] Conteudo, string NomeSugerido, Guid GeracaoId, int TotalDeCpfs);

/// <summary>Uma geração do arquivo de envio, como o histórico a mostra. Sem o arquivo nem os CPFs.</summary>
public sealed record GeracaoDeEnvioDto(
    Guid Id,
    string Edicao,
    string LeiauteCodigo,
    string LeiauteVersao,
    string GeradoPor,
    DateTimeOffset GeradoEm,
    int TotalDeCpfs);
