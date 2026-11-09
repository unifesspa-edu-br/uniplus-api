namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Janelas;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>A janela de migração lida sem rastrear nem travar: a tela dela e o resumo que o perfil mostra.</summary>
public sealed class LeituraDaJanela
{
    private readonly IJanelaDeMigracaoRepository _janelas;
    private readonly LeitorDaMigracao _leitor;
    private readonly TimeProvider _clock;

    public LeituraDaJanela(IJanelaDeMigracaoRepository janelas, LeitorDaMigracao leitor, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(janelas);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(clock);

        _janelas = janelas;
        _leitor = leitor;
        _clock = clock;
    }

    /// <summary>A janela mais recente do perfil com a lista de concessões; nula quando o perfil nunca teve janela.</summary>
    public async Task<JanelaDeMigracaoDto?> ObterAsync(Perfil perfil, Identidade? leitor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        if (await _janelas.ObterUltimaParaLeituraAsync(perfil.Id, cancellationToken).ConfigureAwait(false) is not { } janela)
        {
            return null;
        }

        ContextoDaMigracao contexto = await _leitor.LerParaLeituraAsync(perfil, janela, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        return JanelaMapping.ToDto(janela, perfil, contexto, leitor);
    }

    /// <summary>O resumo da janela não encerrada do perfil, para o perfil mostrá-la; nulo quando não há.</summary>
    public async Task<JanelaDoPerfilDto?> ResumirAsync(Guid perfilId, CancellationToken cancellationToken)
    {
        JanelaDeMigracao? janela = await _janelas.ObterUltimaParaLeituraAsync(perfilId, cancellationToken).ConfigureAwait(false);
        if (janela is null || janela.Encerrada)
        {
            return null;
        }

        bool aberta = await _janelas.TemJanelaAbertaAsync(perfilId, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        return new JanelaDoPerfilDto(janela.Id, janela.Prazo, aberta ? "aberta" : "encerrando");
    }
}
