namespace Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

/// <summary>Um termo aceito no envio, com o nome da versão da inscrição e a versão e o hash que o candidato aceitou.</summary>
public sealed record TermoAceitoDoComprovanteDto(string Codigo, string Nome, Guid VersaoId, string HashVersao);

/// <summary>
/// Uma exigência documental no envio: a obrigatoriedade, a situação já em texto (<c>Atendido</c>, <c>Pendente</c>,
/// <c>Dispensado</c>...), a consequência escrita de não atender e, de cada arquivo, só o que o candidato precisa guardar.
/// </summary>
/// <param name="Nome">O nome do documento exigido.</param>
/// <param name="Integrante">Quem apresenta o documento, <c>Você (candidato)</c> ou <c>Integrante n</c>, quando a exigência é de uma ocorrência de grupo.</param>
/// <param name="Obrigatorio">Se a exigência é obrigatória.</param>
/// <param name="Situacao">A situação da exigência no envio, em texto.</param>
/// <param name="Consequencia">O que acontece a quem não atende; nula quando a exigência não declara consequência.</param>
/// <param name="Arquivos">Os arquivos enviados para a exigência.</param>
public sealed record DocumentoDoComprovanteDto(
    string Nome,
    string? Integrante,
    bool Obrigatorio,
    string Situacao,
    string? Consequencia,
    IReadOnlyList<ArquivoDoComprovanteDto> Arquivos);

/// <summary>Um arquivo no comprovante: o SHA-256 do conteúdo, o tamanho e o formato (<c>PDF</c>, <c>JPEG</c> ou <c>PNG</c>); nunca o nome nem o local de armazenamento.</summary>
public sealed record ArquivoDoComprovanteDto(string HashSha256, long TamanhoBytes, string Formato);
