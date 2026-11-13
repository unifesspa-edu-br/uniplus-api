namespace Unifesspa.UniPlus.Selecao.Application.Services.AcessoPelaBanca;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A decisão de acesso à revelação de um identificador por quem atua por uma banca (ADR-0153): a avaliação da inscrição na
/// etapa, que confere a designação à banca; a classe em que o identificador aparece, que mostra o valor mascarado; e a
/// permissão de revelar, nessa ordem. Consultar a avaliação ou a classe sozinhas nunca revela. Os vínculos da etapa com a
/// fase e do processo com a inscrição são conferidos contra a versão vigente antes de qualquer decisão, e o que não se
/// confere responde o mesmo erro do campo que não existe, sem registrar decisão sobre um resource inexistente.
/// </summary>
public sealed class AcessoARevelacaoPelaBanca
{
    private readonly IVerificadorDeAcesso _verificador;
    private readonly IProcessoSeletivoRepository _processos;
    private readonly IInscricaoRepository _inscricoes;
    private readonly IUnidadeDaFaseNaVersaoVigente _fases;

    public AcessoARevelacaoPelaBanca(
        IVerificadorDeAcesso verificador,
        IProcessoSeletivoRepository processos,
        IInscricaoRepository inscricoes,
        IUnidadeDaFaseNaVersaoVigente fases)
    {
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(inscricoes);
        ArgumentNullException.ThrowIfNull(fases);

        _verificador = verificador;
        _processos = processos;
        _inscricoes = inscricoes;
        _fases = fases;
    }

    /// <summary>
    /// O vínculo da inscrição, quando a pessoa pode revelar o campo; senão o erro com que a rota responde: o 404 do campo
    /// para o que não alcança, e a identidade incompleta e o impedimento pelo que são. Registra cada decisão tomada.
    /// </summary>
    public async Task<Result<VinculoDaInscricao>> DecidirAsync(AlvoDaRevelacaoPelaBanca alvo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alvo);

        Alcance? alcance = await ConferirAsync(alvo, cancellationToken).ConfigureAwait(false);
        if (alcance is null)
        {
            return Result<VinculoDaInscricao>.Failure(ErrosDoCampoDaInscricao.NaoEncontrado);
        }

        DomainError? recusa = await RecusaAsync(alvo, alcance, cancellationToken).ConfigureAwait(false);

        return recusa is null
            ? Result<VinculoDaInscricao>.Success(alcance.Vinculo.Inscricao)
            : Result<VinculoDaInscricao>.Failure(recusa);
    }

    /// <summary>
    /// Se a pessoa poderia revelar o campo pelo caminho da banca, com as mesmas decisões e sem registrar nada: serve à
    /// indicação de que o valor completo pode ser pedido, e quem entrega o valor decide por <see cref="DecidirAsync"/>.
    /// </summary>
    public async Task<bool> PodeAsync(AlvoDaRevelacaoPelaBanca alvo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alvo);

        Alcance? alcance = await ConferirAsync(alvo, cancellationToken).ConfigureAwait(false);

        return alcance is not null
            && await _verificador.PodeAsync(PermissaoSelecao.AvaliacoesConsultar.Requisito, alcance.Avaliacao, cancellationToken)
                .ConfigureAwait(false) == ResultadoDoAcesso.Permitido
            && await DecisaoDoIdentificador.PodeAsync(_verificador, alcance.Campo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DomainError?> RecusaAsync(AlvoDaRevelacaoPelaBanca alvo, Alcance alcance, CancellationToken cancellationToken)
    {
        AcessoNaInscricao daAvaliacao = await AcessoAInscricao
            .DecidirAsync(_verificador, alcance.Vinculo, PermissaoSelecao.AvaliacoesConsultar.Requisito, alcance.Avaliacao, cancellationToken)
            .ConfigureAwait(false);

        return daAvaliacao is Recusada recusada
            ? AcessoAInscricao.Erro(recusada, ErrosDoCampoDaInscricao.NaoEncontrado)
            : await DecisaoDoIdentificador
                .RecusaAsync(_verificador, alcance.Vinculo, alcance.Campo, ErrosDoCampoDaInscricao.NaoEncontrado, cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task<Alcance?> ConferirAsync(AlvoDaRevelacaoPelaBanca alvo, CancellationToken cancellationToken)
    {
        UnidadeDaFase? fase = await _fases.DaEtapaAsync(alvo.ProcessoId, alvo.EtapaId, cancellationToken).ConfigureAwait(false);
        if (fase is null || fase.FaseId != alvo.FaseId)
        {
            return null;
        }

        ConferenciaDoVinculo conferencia = await AcessoAInscricao
            .ConferirVinculoAsync(_processos, _inscricoes, alvo.ProcessoId, alvo.InscricaoId, cancellationToken)
            .ConfigureAwait(false);

        return conferencia is Vinculo vinculo ? Alcancar(alvo, fase, vinculo) : null;
    }

    private static Alcance? Alcancar(AlvoDaRevelacaoPelaBanca alvo, UnidadeDaFase fase, Vinculo vinculo)
    {
        if (string.IsNullOrWhiteSpace(alvo.FatoCodigo) || (alvo.Ocorrencia is not null && string.IsNullOrWhiteSpace(alvo.Ocorrencia)))
        {
            return null;
        }

        Result<ResourceContext> avaliacao = AcessoAoProcesso.AvaliacaoDaInscricao(
            fase, alvo.ProcessoId, alvo.EtapaId, alvo.BancaId, alvo.InscricaoId);
        ResourceContext campo = AcessoAoProcesso.ResourceDoCampo(
            fase.UnidadeId, alvo.ProcessoId, alvo.InscricaoId, alvo.FatoCodigo, alvo.Ocorrencia);

        return avaliacao.IsSuccess ? new Alcance(vinculo, avaliacao.Value!, campo) : null;
    }

    private sealed record Alcance(Vinculo Vinculo, ResourceContext Avaliacao, ResourceContext Campo);
}
