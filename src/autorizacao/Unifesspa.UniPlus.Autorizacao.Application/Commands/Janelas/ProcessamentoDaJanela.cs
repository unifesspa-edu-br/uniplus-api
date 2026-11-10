namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O trabalho da rotina sobre a janela de um perfil, sob a trava dele: o prazo vencido e a falta de pendência
/// materializam o fim, e a janela que segue aberta recebe o lembrete de 24 ou de 48 horas, uma vez cada. Com os dois
/// marcos já passados sai só o de 24 horas, que grava os dois marcadores. O marcador e a mensagem vão no mesmo
/// commit. Roda sem chave de idempotência, na rotina e na materialização prévia: o conflito de concorrência propaga
/// (ADR-0119).
/// </summary>
public sealed class ProcessamentoDaJanela
{
    private static readonly TimeSpan MarcoDe24Horas = TimeSpan.FromHours(24);
    private static readonly TimeSpan MarcoDe48Horas = TimeSpan.FromHours(48);

    private readonly MigracaoSobreOPerfil _migracao;
    private readonly AvisosDaJanela _avisos;

    public ProcessamentoDaJanela(MigracaoSobreOPerfil migracao, AvisosDaJanela avisos)
    {
        ArgumentNullException.ThrowIfNull(migracao);
        ArgumentNullException.ThrowIfNull(avisos);

        _migracao = migracao;
        _avisos = avisos;
    }

    public async Task<GravacaoDaOperacao<bool>> ProcessarAsync(Guid perfilId, CancellationToken cancellationToken)
    {
        GravacaoDaOperacao<bool> gravacao = await _migracao
            .ExecutarSemChaveAsync(perfilId, j => LembrarAsync(j, cancellationToken), cancellationToken)
            .ConfigureAwait(false);

        return gravacao.Resultado.IsFailure ? ComoTrabalhoDaRotina(gravacao) : gravacao;
    }

    // O fim materializado pela conferência do estado é o trabalho da rotina, e a janela inexistente não tem o que fazer.
    private static GravacaoDaOperacao<bool> ComoTrabalhoDaRotina(GravacaoDaOperacao<bool> gravacao) =>
        gravacao.Resultado.Error!.Code switch
        {
            MigracaoErrorCodes.PrazoEncerrado or MigracaoErrorCodes.JanelaEncerrada => new(Result<bool>.Success(true), gravacao.Avisos),
            MigracaoErrorCodes.JanelaNaoEncontrada => new(Result<bool>.Success(false), gravacao.Avisos),
            _ => gravacao,
        };

    private async Task<DesfechoDaOperacao<bool>> LembrarAsync(JanelaDaOperacao janela, CancellationToken cancellationToken)
    {
        int? horas = LembreteDevido(janela.Janela, janela.Contexto.Agora);
        if (horas is not { } devido)
        {
            return new DesfechoDaOperacao<bool>(Result<bool>.Success(false));
        }

        IReadOnlyList<EnviarEmail> avisos = await _avisos
            .DoLembreteAsync(janela.Perfil, janela.Janela, LembreteDe(janela, devido), cancellationToken)
            .ConfigureAwait(false);
        Marcar(janela.Janela, devido, janela.Contexto.Agora);

        return new DesfechoDaOperacao<bool>(Result<bool>.Success(true), Avisos: avisos);
    }

    private static LembreteDaJanela LembreteDe(JanelaDaOperacao janela, int horas)
    {
        IReadOnlyList<Concessao> pendentes = [.. janela.Contexto.Concessoes.Where(c => janela.Janela.Elegivel(c, janela.Contexto.Agora))];
        IReadOnlyList<Concessao> retidas = [.. pendentes.Where(c => janela.Janela.LinhaDe(c.Id)?.Situacao == SituacaoDaMigracao.Retida)];

        return new LembreteDaJanela(horas, pendentes, retidas, pendentes.Any(c => janela.Contexto.ComposicaoDe(c).ExigeProtecaoDeDados));
    }

    // O lembrete de 24 horas vence o de 48: quem chega atrasado recebe só o mais próximo do prazo.
    private static int? LembreteDevido(JanelaDeMigracao janela, DateTimeOffset agora)
    {
        if (agora >= janela.Prazo - MarcoDe24Horas && janela.Aviso24hEm is null)
        {
            return 24;
        }

        return agora >= janela.Prazo - MarcoDe48Horas && janela.Aviso48hEm is null && janela.Aviso24hEm is null ? 48 : null;
    }

    private static void Marcar(JanelaDeMigracao janela, int horas, DateTimeOffset agora)
    {
        if (horas == 24)
        {
            janela.RegistrarAvisoDe24Horas(agora);
        }
        else
        {
            janela.RegistrarAvisoDe48Horas(agora);
        }
    }
}
