namespace Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

/// <summary>Dados de escolhas de curso no comprovante.</summary>
public sealed record EscolhasDeCursoNoComprovanteDto
{
    /// <summary>Quantas opções de curso a versão da inscrição admite, para dizer que a 2ª não foi declarada.</summary>
    public required int NumeroDeOpcoesDeCurso { get; init; }

    /// <summary>Os cursos escolhidos neste envio, em ordem, com as modalidades em que o candidato concorre.</summary>
    public required IReadOnlyList<OpcaoDeCursoDoComprovanteDto> OpcoesDeCurso { get; init; }
}
