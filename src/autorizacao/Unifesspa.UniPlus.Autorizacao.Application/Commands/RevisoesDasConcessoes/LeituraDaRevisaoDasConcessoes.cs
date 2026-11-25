namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.RevisoesDasConcessoes;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>A revisão das concessões lida sem rastrear nem travar: a tela dela e o resumo que o perfil mostra.</summary>
public sealed class LeituraDaRevisaoDasConcessoes
{
    private readonly IRevisaoDasConcessoesRepository _revisoes;
    private readonly LeitorDaMigracao _leitor;
    private readonly TimeProvider _clock;

    public LeituraDaRevisaoDasConcessoes(IRevisaoDasConcessoesRepository revisoes, LeitorDaMigracao leitor, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(revisoes);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(clock);

        _revisoes = revisoes;
        _leitor = leitor;
        _clock = clock;
    }

    /// <summary>A revisão mais recente do perfil com a lista de concessões; nula quando o perfil nunca teve revisão.</summary>
    public async Task<RevisaoDasConcessoesDto?> ObterAsync(Perfil perfil, Identidade? leitor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        if (await _revisoes.ObterUltimaParaLeituraAsync(perfil.Id, cancellationToken).ConfigureAwait(false) is not { } revisao)
        {
            return null;
        }

        ContextoDaRevisao contexto = await _leitor.LerParaLeituraAsync(perfil, revisao, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        return RevisaoDasConcessoesMapping.ToDto(revisao, perfil, contexto, leitor);
    }

    /// <summary>O resumo da revisão não encerrada do perfil, para o perfil mostrá-la; nulo quando não há.</summary>
    public async Task<RevisaoDoPerfilDto?> ResumirAsync(Guid perfilId, CancellationToken cancellationToken)
    {
        RevisaoDasConcessoesDoPerfil? revisao = await _revisoes.ObterUltimaParaLeituraAsync(perfilId, cancellationToken).ConfigureAwait(false);
        if (revisao is null || revisao.Encerrada)
        {
            return null;
        }

        bool aberta = await _revisoes.TemRevisaoAbertaAsync(perfilId, _clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        return new RevisaoDoPerfilDto(revisao.Id, revisao.TerminaEm, CodigoComRotulo<SituacaoDaRevisaoDasConcessoes>.De(aberta ? SituacaoDaRevisaoDasConcessoes.Aberta : SituacaoDaRevisaoDasConcessoes.Encerrando));
    }
}
