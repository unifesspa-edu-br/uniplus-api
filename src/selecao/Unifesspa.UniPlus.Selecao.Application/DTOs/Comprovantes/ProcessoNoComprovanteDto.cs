namespace Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

/// <summary>Dados de processo seletivo no comprovante.</summary>
public sealed record ProcessoNoComprovanteDto
{
    /// <summary>O processo seletivo da inscrição.</summary>
    public required Guid ProcessoSeletivoId { get; init; }

    /// <summary>O nome do processo seletivo.</summary>
    public required string NomeDoProcesso { get; init; }

    /// <summary>O identificador legível do processo (o do endereço público do certame), o congelado na versão em que a inscrição foi enviada; nulo se a versão não o declara.</summary>
    public string? IdentificadorDoProcesso { get; init; }

    /// <summary>O nome da unidade administradora do processo, o que ficou congelado na criação do processo.</summary>
    public required string UnidadeAdministradora { get; init; }
}
