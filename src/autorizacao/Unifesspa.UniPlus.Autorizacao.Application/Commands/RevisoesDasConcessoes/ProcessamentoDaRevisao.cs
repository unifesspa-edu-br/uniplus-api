namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;

using Unifesspa.UniPlus.Application.Abstractions.Email;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O trabalho da rotina sobre a revisão de um perfil, sob a trava dele: o prazo vencido e a falta de pendência
/// materializam o fim, e a revisão que segue aberta recebe o lembrete de 24 ou de 48 horas, uma vez cada. Com os dois
/// marcos já passados sai só o de 24 horas, que grava os dois marcadores. O marcador e a mensagem vão no mesmo
/// commit. Roda sem chave de idempotência, na rotina e na materialização prévia: o conflito de concorrência propaga
/// (ADR-0119).
/// </summary>
public sealed class ProcessamentoDaRevisao
{
    private static readonly TimeSpan MarcoDe24Horas = TimeSpan.FromHours(24);
    private static readonly TimeSpan MarcoDe48Horas = TimeSpan.FromHours(48);

    private readonly MigracaoSobreOPerfil _migracao;
    private readonly AvisosDaRevisao _avisos;

    public ProcessamentoDaRevisao(MigracaoSobreOPerfil migracao, AvisosDaRevisao avisos)
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

    // O fim materializado pela conferência do estado é o trabalho da rotina, e a revisão inexistente não tem o que fazer.
    private static GravacaoDaOperacao<bool> ComoTrabalhoDaRotina(GravacaoDaOperacao<bool> gravacao) =>
        gravacao.Resultado.Error!.Code switch
        {
            RevisaoDasConcessoesErrorCodes.PrazoEncerrado or RevisaoDasConcessoesErrorCodes.RevisaoEncerrada => new(Result<bool>.Success(true), gravacao.Avisos),
            RevisaoDasConcessoesErrorCodes.RevisaoNaoEncontrada => new(Result<bool>.Success(false), gravacao.Avisos),
            _ => gravacao,
        };

    private async Task<DesfechoDaOperacao<bool>> LembrarAsync(RevisaoDaOperacao revisao, CancellationToken cancellationToken)
    {
        int? horas = LembreteDevido(revisao.Revisao, revisao.Contexto.Agora);
        if (horas is not { } devido)
        {
            return new DesfechoDaOperacao<bool>(Result<bool>.Success(false));
        }

        IReadOnlyList<EnviarEmail> avisos = await _avisos
            .DoLembreteAsync(revisao.Perfil, revisao.Revisao, LembreteDe(revisao, devido), cancellationToken)
            .ConfigureAwait(false);
        Marcar(revisao.Revisao, devido, revisao.Contexto.Agora);

        return new DesfechoDaOperacao<bool>(Result<bool>.Success(true), Avisos: avisos);
    }

    private static LembreteDaRevisao LembreteDe(RevisaoDaOperacao revisao, int horas)
    {
        IReadOnlyList<Concessao> pendentes = [.. revisao.Contexto.Concessoes.Where(c => revisao.Revisao.Elegivel(c, revisao.Contexto.Agora))];
        IReadOnlyList<Concessao> retidas = [.. pendentes.Where(c => revisao.Revisao.LinhaDe(c.Id)?.Situacao == SituacaoDaMigracao.Retida)];

        return new LembreteDaRevisao(horas, pendentes, retidas, pendentes.Any(c => revisao.Contexto.ComposicaoDe(c).ExigeProtecaoDeDados));
    }

    // O lembrete de 24 horas vence o de 48: quem chega atrasado recebe só o mais próximo do prazo.
    private static int? LembreteDevido(RevisaoDasConcessoesDoPerfil revisao, DateTimeOffset agora)
    {
        if (agora >= revisao.TerminaEm - MarcoDe24Horas && revisao.Aviso24hEm is null)
        {
            return 24;
        }

        return agora >= revisao.TerminaEm - MarcoDe48Horas && revisao.Aviso48hEm is null && revisao.Aviso24hEm is null ? 48 : null;
    }

    private static void Marcar(RevisaoDasConcessoesDoPerfil revisao, int horas, DateTimeOffset agora)
    {
        if (horas == 24)
        {
            revisao.RegistrarAvisoDe24Horas(agora);
        }
        else
        {
            revisao.RegistrarAvisoDe48Horas(agora);
        }
    }
}
