namespace Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

/// <summary>Dados de formulário no comprovante.</summary>
public sealed record FormularioNoComprovanteDto
{
    /// <summary>As respostas do formulário agrupadas por seção, na ordem de coleta.</summary>
    public required IReadOnlyList<SecaoDoComprovanteDto> Secoes { get; init; }

    /// <summary>O que o sistema calculou das respostas neste envio.</summary>
    public required IReadOnlyList<ItemDoComprovanteDto> Derivados { get; init; }

    /// <summary>Os termos que o candidato aceitou neste envio.</summary>
    public required IReadOnlyList<TermoAceitoDoComprovanteDto> TermosAceitos { get; init; }

    /// <summary>As exigências documentais neste envio, com o que o candidato precisa guardar de cada arquivo.</summary>
    public required IReadOnlyList<DocumentoDoComprovanteDto> Documentos { get; init; }
}
