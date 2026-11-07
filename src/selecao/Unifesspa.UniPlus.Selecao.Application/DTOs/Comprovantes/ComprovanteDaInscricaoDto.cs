namespace Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

using System.ComponentModel;

/// <summary>
/// O comprovante de um envio da inscrição, pronto para exibir: o texto (rótulos, seções, nomes de termos e de cursos) é o da
/// versão do formulário em que a inscrição foi enviada, e os valores são os que aquele envio gravou. Não traz número de
/// documento, classificação de proteção nem base legal; o CPF sai sempre mascarado.
/// </summary>
public sealed record ComprovanteDaInscricaoDto
{
    /// <summary>A inscrição a que o comprovante pertence.</summary>
    public required Guid InscricaoId { get; init; }

    /// <summary>A posição do comprovante na ordem de emissão, a partir de 1: o número com que se lê em <c>GET .../comprovantes/{numero}</c>.</summary>
    public required int Numero { get; init; }

    /// <summary>Quantos comprovantes a inscrição emitiu.</summary>
    public required int Total { get; init; }

    /// <summary>Se é o último emitido, o que vale; os anteriores foram substituídos por correção.</summary>
    public required bool Vale { get; init; }

    /// <summary>O número do comprovante anterior; nulo no primeiro.</summary>
    public int? NumeroAnterior { get; init; }

    /// <summary>O número do comprovante seguinte; nulo no último.</summary>
    public int? NumeroSeguinte { get; init; }

    /// <summary>O processo seletivo da inscrição.</summary>
    public required Guid ProcessoSeletivoId { get; init; }

    /// <summary>O nome do processo seletivo.</summary>
    public required string NomeDoProcesso { get; init; }

    /// <summary>O envio que o comprovante registra.</summary>
    public required EnvioDoComprovanteDto Envio { get; init; }

    /// <summary>O titular da inscrição.</summary>
    public required CandidatoDoComprovanteDto Candidato { get; init; }

    /// <summary>Até quando o candidato pode corrigir a inscrição; nulo quando este não é o comprovante que vale ou o prazo não pôde ser lido.</summary>
    public PrazoDaCorrecaoDto? PrazoDaCorrecao { get; init; }

    /// <summary>Quantas opções de curso a versão da inscrição admite, para dizer que a 2ª não foi declarada.</summary>
    public required int NumeroDeOpcoesDeCurso { get; init; }

    /// <summary>Os cursos escolhidos neste envio, em ordem, com as modalidades em que o candidato concorre.</summary>
    public required IReadOnlyList<OpcaoDeCursoDoComprovanteDto> OpcoesDeCurso { get; init; }

    /// <summary>As respostas do formulário agrupadas por seção, na ordem de coleta.</summary>
    public required IReadOnlyList<SecaoDoComprovanteDto> Secoes { get; init; }

    /// <summary>O que o sistema calculou das respostas neste envio.</summary>
    public required IReadOnlyList<ItemDoComprovanteDto> Derivados { get; init; }

    /// <summary>Os termos que o candidato aceitou neste envio.</summary>
    public required IReadOnlyList<TermoAceitoDoComprovanteDto> TermosAceitos { get; init; }

    /// <summary>As exigências documentais neste envio, com o que o candidato precisa guardar de cada arquivo.</summary>
    public required IReadOnlyList<DocumentoDoComprovanteDto> Documentos { get; init; }
}

/// <summary>O envio que o comprovante registra: o número da inscrição, o instante, a zona em que ele se lê e o código de verificação.</summary>
/// <param name="NumeroInscricao">O número da inscrição, gerado no primeiro envio.</param>
/// <param name="EnviadoEm">O instante do envio.</param>
/// <param name="FusoHorario">A zona em que <paramref name="EnviadoEm"/> se lê.</param>
/// <param name="HashIntegridade">O código de verificação, o SHA-256 do que foi gravado no envio.</param>
public sealed record EnvioDoComprovanteDto(string NumeroInscricao, DateTimeOffset EnviadoEm, FusoHorarioDto FusoHorario, string HashIntegridade);

/// <summary>O titular no comprovante: o nome civil, o nome social quando o candidato optou por ele, e o CPF mascarado.</summary>
/// <param name="NomeCivil">O nome civil informado no envio.</param>
/// <param name="NomeSocial">O nome social; nulo quando o candidato não optou por usá-lo.</param>
/// <param name="UsaNomeSocial">Se o candidato optou por ser identificado pelo nome social.</param>
/// <param name="CpfMascarado">O CPF no formato <c>***.999.999-**</c>.</param>
public sealed record CandidatoDoComprovanteDto(string NomeCivil, string? NomeSocial, bool UsaNomeSocial, string CpfMascarado);

/// <summary>O prazo para corrigir a inscrição: o fim do período de inscrição divulgado e se a correção ainda é possível agora.</summary>
/// <param name="Fim">O último instante do período de inscrição, o da versão divulgada, com as prorrogações.</param>
/// <param name="FusoHorario">A zona em que <paramref name="Fim"/> se lê.</param>
/// <param name="Aberto">Se o candidato pode corrigir a inscrição agora, a mesma regra de <c>acoes.podeCorrigir</c> da lista de inscrições.</param>
public sealed record PrazoDaCorrecaoDto(
    DateTimeOffset Fim,
    FusoHorarioDto FusoHorario,
    [property: Description("Se o candidato pode corrigir a inscrição agora: inscrição enviada e período de inscrição aberto.")]
    bool Aberto);

/// <summary>Um curso escolhido no envio, com o nome que a versão da inscrição lhe dá (curso, grau, unidade e turnos).</summary>
/// <param name="Ordem">A opção: 1 para a primeira, 2 para a segunda.</param>
/// <param name="Nome">O nome da oferta; nulo quando a versão não a nomeia.</param>
/// <param name="Modalidades">As modalidades em que o candidato concorre na opção; nulas quando a versão não deriva a modalidade.</param>
public sealed record OpcaoDeCursoDoComprovanteDto(int Ordem, string? Nome, IReadOnlyList<ModalidadeDaInscricaoDto>? Modalidades);
