namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;

using DTOs;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

using Wolverine.Attributes;

public sealed record ObterIndicadoresDoProcessoQuery(Guid ProcessoSeletivoId) : IQuery<Result<IndicadoresDoProcessoDto>>;

public interface IObterIndicadoresDoProcesso
{
    Task<Result<IndicadoresDoProcessoDto>> ExecutarAsync(
        ObterIndicadoresDoProcessoQuery query,
        CancellationToken cancellationToken);
}

public static class ObterIndicadoresDoProcessoQueryHandler
{
    [NonTransactional]
    public static async Task<Result<IndicadoresDoProcessoDto>> Handle(
        ObterIndicadoresDoProcessoQuery query,
        IObterIndicadoresDoProcesso obterIndicadores,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(obterIndicadores);
        return await obterIndicadores.ExecutarAsync(query, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class ObterIndicadoresDoProcesso : IObterIndicadoresDoProcesso
{
    private const string DescricaoPadrao = "Inscrições ainda não enviadas";
    private const string CodigoProcessoNaoEncontrado = "Indicadores.ProcessoNaoEncontrado";

    private readonly IProcessoSeletivoRepository _processos;
    private readonly ICertameDivulgadoRepository _divulgado;
    private readonly IIndicadoresDoProcessoReader _reader;
    private readonly IVerificadorDeAcesso _verificador;
    private readonly IRegistroCodecsEnvelope _codecs;
    private readonly TimeProvider _timeProvider;

    public ObterIndicadoresDoProcesso(
        IProcessoSeletivoRepository processos,
        ICertameDivulgadoRepository divulgado,
        IIndicadoresDoProcessoReader reader,
        IVerificadorDeAcesso verificador,
        IRegistroCodecsEnvelope codecs,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(divulgado);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(codecs);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _processos = processos;
        _divulgado = divulgado;
        _reader = reader;
        _verificador = verificador;
        _codecs = codecs;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IndicadoresDoProcessoDto>> ExecutarAsync(
        ObterIndicadoresDoProcessoQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        Result<EstadoDoProcesso> estadoResult = await ObterEstadoPermitidoAsync(query, cancellationToken).ConfigureAwait(false);
        if (estadoResult.IsFailure)
            return Result<IndicadoresDoProcessoDto>.Failure(estadoResult.Error!);

        DateTimeOffset consultadoEm = _timeProvider.GetUtcNow();
        return await MontarRespostaAsync(query, estadoResult.Value!, consultadoEm, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<EstadoDoProcesso>> ObterEstadoPermitidoAsync(
        ObterIndicadoresDoProcessoQuery query,
        CancellationToken cancellationToken)
    {
        EstadoDoProcesso? estado = await _processos.ObterEstadoAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (estado is null)
            return Result<EstadoDoProcesso>.Failure(ErroNaoEncontrado());

        ResultadoDoAcesso acesso = await AutorizarAsync(estado, query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (acesso != ResultadoDoAcesso.Permitido)
        {
            DomainError erro = acesso is ResultadoDoAcesso.Impedido or ResultadoDoAcesso.IdentidadeIncompleta
                ? AcessoAoProcesso.Recusa(acesso)
                : ErroNaoEncontrado();
            return Result<EstadoDoProcesso>.Failure(erro);
        }

        return Result<EstadoDoProcesso>.Success(estado);
    }

    private async Task<Result<IndicadoresDoProcessoDto>> MontarRespostaAsync(
        ObterIndicadoresDoProcessoQuery query,
        EstadoDoProcesso estado,
        DateTimeOffset consultadoEm,
        CancellationToken cancellationToken)
    {
        ContagensDoProcesso contagens = estado.Status == StatusProcesso.Rascunho
            ? new ContagensDoProcesso(0, 0, 0)
            : await _reader.LerAsync(query.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);

        Result<string> descricao = await DescricaoDosRascunhosAsync(query.ProcessoSeletivoId, estado, consultadoEm, cancellationToken).ConfigureAwait(false);
        if (descricao.IsFailure)
            return Result<IndicadoresDoProcessoDto>.Failure(descricao.Error!);

        return Result<IndicadoresDoProcessoDto>.Success(CriarDto(query, consultadoEm, contagens, descricao.Value!));
    }

    private static IndicadoresDoProcessoDto CriarDto(
        ObterIndicadoresDoProcessoQuery query,
        DateTimeOffset consultadoEm,
        ContagensDoProcesso contagens,
        string descricaoRascunhos)
    {
        IndicadorDoProcessoDto[] itens =
        [
            new(CodigoIndicadorDoProcesso.Inscricoes, contagens.Inscricoes, "Inscrições", "Submetidas e não canceladas"),
            new(CodigoIndicadorDoProcesso.Inscritos, contagens.Inscritos, "Inscritos", "Pessoas distintas com inscrição submetida e não cancelada"),
            new(CodigoIndicadorDoProcesso.Rascunhos, contagens.Rascunhos, "Rascunhos", descricaoRascunhos),
        ];
        string raiz = $"/api/selecao/processos-seletivos/{query.ProcessoSeletivoId:D}";
        return new(query.ProcessoSeletivoId, consultadoEm, itens,
            new IndicadoresDoProcessoLinksDto($"{raiz}/indicadores", raiz));
    }

    private async Task<ResultadoDoAcesso> AutorizarAsync(
        EstadoDoProcesso estado,
        Guid processoSeletivoId,
        CancellationToken cancellationToken) => estado.Status == StatusProcesso.Rascunho
            ? await AcessoAoProcesso.VerificarLeituraAsync(_verificador, _processos, processoSeletivoId, cancellationToken).ConfigureAwait(false)
            : await AcessoAoProcesso.ExigirNaUnidadeAsync(_verificador, estado.UnidadeId, PermissaoSelecao.ProcessosAcompanhar,
                unidade => AcessoAoProcesso.ResourceDoProcesso(unidade, processoSeletivoId), cancellationToken).ConfigureAwait(false);

    private async Task<Result<string>> DescricaoDosRascunhosAsync(
        Guid processoSeletivoId,
        EstadoDoProcesso estado,
        DateTimeOffset consultadoEm,
        CancellationToken cancellationToken)
    {
        if (estado.Status == StatusProcesso.Rascunho)
            return Result<string>.Success(DescricaoPadrao);

        Result<VersaoDivulgada> versao = await VersaoDivulgadaResolver
            .ResolverAsync(processoSeletivoId, _divulgado, _processos, cancellationToken).ConfigureAwait(false);
        if (versao.IsFailure)
        {
            return versao.Error!.Code == "ProcessoSeletivo.NaoEncontrado"
                ? Result<string>.Success(DescricaoPadrao)
                : Result<string>.Failure(versao.Error);
        }

        return DescricaoDaVersao(versao.Value!.Versao, consultadoEm);
    }

    private Result<string> DescricaoDaVersao(VersaoConfiguracao versao, DateTimeOffset consultadoEm)
    {
        Result<JanelaDaInscricao> janela = PeriodoDaInscricaoResolver.JanelaDa(versao, _codecs);
        if (janela.IsFailure)
        {
            return janela.Error!.Code == "Inscricao.ProcessoSemInscricao"
                ? Result<string>.Success(DescricaoPadrao)
                : Result<string>.Failure(janela.Error);
        }

        string descricao = consultadoEm < janela.Value!.Inicio
            ? DescricaoPadrao
            : consultadoEm <= janela.Value.Fim ? "Em preenchimento" : "Não enviados";
        return Result<string>.Success(descricao);
    }

    private static DomainError ErroNaoEncontrado() =>
        new(CodigoProcessoNaoEncontrado, "Processo não encontrado.");
}
