namespace Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;

using System.ComponentModel;

/// <summary>
/// O comprovante de um envio da inscrição, pronto para exibir: o texto (rótulos, seções, nomes de termos e de cursos) é o da
/// versão do formulário em que a inscrição foi enviada, e os valores são os que aquele envio gravou. Não traz número de
/// documento, classificação de proteção nem base legal; o CPF sai sempre mascarado.
/// </summary>
public sealed record ComprovanteDaInscricaoDto
{
    /// <summary>O titular da inscrição.</summary>
    public required CandidatoDoComprovanteDto Candidato { get; init; }

    /// <summary>Os dados da inscrição e do envio.</summary>
    public required InscricaoNoComprovanteDto Inscricao { get; init; }

    /// <summary>Os dados do processo seletivo.</summary>
    public required ProcessoNoComprovanteDto Processo { get; init; }

    /// <summary>As escolhas de curso do envio.</summary>
    public required EscolhasDeCursoNoComprovanteDto EscolhasDeCurso { get; init; }

    /// <summary>Os dados do formulário enviado.</summary>
    public required FormularioNoComprovanteDto Formulario { get; init; }

}

/// <summary>O envio que o comprovante registra: o número da inscrição, o instante, a zona em que ele se lê e o código de verificação.</summary>
/// <param name="NumeroInscricao">O número da inscrição, gerado no primeiro envio.</param>
/// <param name="EnviadoEm">O instante do envio.</param>
/// <param name="FusoHorario">A zona em que <paramref name="EnviadoEm"/> se lê.</param>
/// <param name="HashIntegridade">O código de verificação, o SHA-256 do que foi gravado no envio.</param>
public sealed record EnvioDoComprovanteDto(string NumeroInscricao, DateTimeOffset EnviadoEm, FusoHorarioDto FusoHorario, string HashIntegridade);

/// <summary>O cancelamento que o comprovante da inscrição cancelada informa.</summary>
/// <param name="CanceladaEm">O instante do cancelamento.</param>
/// <param name="Motivo">O motivo informado; nulo quando não há.</param>
/// <param name="Aviso">O texto da tarja, pronto para exibir: diz que o documento não tem validade porque a inscrição foi cancelada e quando, com data e hora até o segundo na zona institucional, sem o motivo.</param>
public sealed record CancelamentoNoComprovanteDto(DateTimeOffset CanceladaEm, string? Motivo, string Aviso);

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
