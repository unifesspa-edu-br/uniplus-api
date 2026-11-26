namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;

using DTOs;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

using Wolverine.Attributes;

public sealed record ObterIndicadoresDoProcessoQuery(Guid ProcessoSeletivoId) : IQuery<Result<IndicadoresDoProcessoDto>>;

public static class ObterIndicadoresDoProcessoQueryHandler
{
    [NonTransactional]
    public static async Task<Result<IndicadoresDoProcessoDto>> Handle(
        ObterIndicadoresDoProcessoQuery consulta,
        IProcessoSeletivoRepository processos,
        IIndicadoresDoProcessoReader leitor,
        IVerificadorDeAcesso verificador,
        DescricaoDosRascunhos descricaoDosRascunhos,
        TimeProvider relogio,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        ValidarDependencias(processos, leitor, verificador, descricaoDosRascunhos, relogio);

        Result<EstadoDoProcesso> estado = await ObterEstadoPermitidoAsync(
            consulta.ProcessoSeletivoId, processos, verificador, cancellationToken).ConfigureAwait(false);
        if (estado.IsFailure)
            return Result<IndicadoresDoProcessoDto>.Failure(estado.Error!);

        return await MontarRespostaAsync(
            consulta.ProcessoSeletivoId, estado.Value!, leitor, descricaoDosRascunhos, relogio.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
    }

    private static void ValidarDependencias(
        IProcessoSeletivoRepository processos,
        IIndicadoresDoProcessoReader leitor,
        IVerificadorDeAcesso verificador,
        DescricaoDosRascunhos descricaoDosRascunhos,
        TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(descricaoDosRascunhos);
        ArgumentNullException.ThrowIfNull(relogio);
    }

    private static async Task<Result<EstadoDoProcesso>> ObterEstadoPermitidoAsync(
        Guid processoSeletivoId,
        IProcessoSeletivoRepository processos,
        IVerificadorDeAcesso verificador,
        CancellationToken cancellationToken)
    {
        EstadoDoProcesso? estado = await processos.ObterEstadoAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);
        if (estado is null)
            return Result<EstadoDoProcesso>.Failure(AcessoAoProcesso.NaoEncontrado());

        ResultadoDoAcesso acesso = await AutorizarAsync(estado, processoSeletivoId, verificador, cancellationToken)
            .ConfigureAwait(false);
        return acesso == ResultadoDoAcesso.Permitido
            ? Result<EstadoDoProcesso>.Success(estado)
            : Result<EstadoDoProcesso>.Failure(ErroDoAcesso(acesso));
    }

    private static async Task<ResultadoDoAcesso> AutorizarAsync(
        EstadoDoProcesso estado,
        Guid processoSeletivoId,
        IVerificadorDeAcesso verificador,
        CancellationToken cancellationToken) =>
        EstaEmRascunho(estado)
            ? await AcessoAoProcesso.ExigirNaUnidadeAsync(
                verificador,
                estado.UnidadeId,
                PermissaoSelecao.ProcessosConfigurar,
                unidade => AcessoAoProcesso.ResourceDoProcesso(unidade, processoSeletivoId),
                cancellationToken)
                .ConfigureAwait(false)
            : await AcessoAoProcesso.ExigirNaUnidadeAsync(
                verificador,
                estado.UnidadeId,
                PermissaoSelecao.ProcessosAcompanhar,
                unidade => AcessoAoProcesso.ResourceDoProcesso(unidade, processoSeletivoId),
                cancellationToken).ConfigureAwait(false);

    private static DomainError ErroDoAcesso(ResultadoDoAcesso acesso) =>
        acesso is ResultadoDoAcesso.Impedido or ResultadoDoAcesso.IdentidadeIncompleta
            ? AcessoAoProcesso.Recusa(acesso)
            : AcessoAoProcesso.NaoEncontrado();

    private static async Task<Result<IndicadoresDoProcessoDto>> MontarRespostaAsync(
        Guid processoSeletivoId,
        EstadoDoProcesso estado,
        IIndicadoresDoProcessoReader leitor,
        DescricaoDosRascunhos descricaoDosRascunhos,
        DateTimeOffset consultadoEm,
        CancellationToken cancellationToken)
    {
        ContagensDoProcesso contagens = EstaEmRascunho(estado)
            ? new ContagensDoProcesso(0, 0, 0)
            : await leitor.LerAsync(processoSeletivoId, cancellationToken).ConfigureAwait(false);

        Result<string> descricao = await descricaoDosRascunhos
            .ObterAsync(processoSeletivoId, EstaEmRascunho(estado), consultadoEm, cancellationToken).ConfigureAwait(false);
        return descricao.IsFailure
            ? Result<IndicadoresDoProcessoDto>.Failure(descricao.Error!)
            : Result<IndicadoresDoProcessoDto>.Success(CriarDto(processoSeletivoId, consultadoEm, contagens, descricao.Value!));
    }

    private static IndicadoresDoProcessoDto CriarDto(
        Guid processoSeletivoId,
        DateTimeOffset consultadoEm,
        ContagensDoProcesso contagens,
        string descricaoRascunhos)
    {
        IndicadorDoProcessoDto[] itens =
        [
            CriarIndicador(CodigoIndicadorDoProcesso.Inscricoes, contagens.Inscricoes),
            CriarIndicador(CodigoIndicadorDoProcesso.Inscritos, contagens.Inscritos),
            CriarIndicador(CodigoIndicadorDoProcesso.Rascunhos, contagens.Rascunhos, descricaoRascunhos),
        ];
        return new(processoSeletivoId, consultadoEm, itens, null!);
    }

    private static IndicadorDoProcessoDto CriarIndicador(CodigoIndicadorDoProcesso indicador, long valor, string? descricao = null) =>
        new(indicador.Codigo(), valor, indicador.Rotulo(), descricao ?? indicador.Descricao()!);

    private static bool EstaEmRascunho(EstadoDoProcesso estado) => estado.Situacao == SituacaoDoProcessoSeletivo.Rascunho;
}
