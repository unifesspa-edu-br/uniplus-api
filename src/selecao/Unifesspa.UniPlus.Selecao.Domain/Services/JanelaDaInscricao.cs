namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Globalization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O período em que o candidato inicia, salva e envia a inscrição: o período de inscrição que o ato
/// publicou e que a retificação prorroga, o mesmo do edital e da vitrine. O instante é absoluto; o
/// fuso institucional só formata a mensagem.
/// </summary>
public sealed record JanelaDaInscricao(DateTimeOffset Inicio, DateTimeOffset Fim)
{
    /// <summary>
    /// A janela da versão publicada. Sem formulário de inscrição, o processo não recebe inscrição
    /// pelo sistema, como o de resultado importado.
    /// </summary>
    public static Result<JanelaDaInscricao> Da(GrafoConfiguracao grafo, DadosEdital dados)
    {
        ArgumentNullException.ThrowIfNull(grafo);
        ArgumentNullException.ThrowIfNull(dados);

        if (!grafo.Formularios.Any(static f => f.Finalidade == FinalidadeFormulario.Inscricao))
        {
            return Result<JanelaDaInscricao>.Failure(new DomainError(
                InscricaoErrorCodes.ProcessoSemInscricao,
                "Este processo seletivo não recebe inscrição pelo sistema."));
        }

        return Result<JanelaDaInscricao>.Success(new JanelaDaInscricao(dados.PeriodoInscricaoInicio, dados.PeriodoInscricaoFim));
    }

    /// <summary>Recusa o instante fora da janela, com início e fim inclusivos.</summary>
    public DomainError? Conferir(DateTimeOffset agora, TimeZoneInfo fuso)
    {
        ArgumentNullException.ThrowIfNull(fuso);

        if (agora < Inicio)
        {
            return new DomainError(
                InscricaoErrorCodes.ForaDoPeriodo,
                $"As inscrições deste processo seletivo começam em {Formatar(Inicio, fuso)} e vão até {Formatar(Fim, fuso)}, no horário de {FusoInstitucional.Nome}.");
        }

        if (agora > Fim)
        {
            return new DomainError(
                InscricaoErrorCodes.ForaDoPeriodo,
                $"As inscrições deste processo seletivo se encerraram em {Formatar(Fim, fuso)}, no horário de {FusoInstitucional.Nome}.");
        }

        return null;
    }

    private static string Formatar(DateTimeOffset instante, TimeZoneInfo fuso) =>
        TimeZoneInfo.ConvertTime(instante, fuso).ToString("dd/MM/yyyy 'às' HH'h'mm", CultureInfo.InvariantCulture);
}
