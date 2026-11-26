namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;

using Unifesspa.UniPlus.Kernel.Results;

public sealed class DescricaoDosRascunhos
{
    private const string DescricaoPadrao = "Inscrições ainda não enviadas";
    private readonly ICertameDivulgadoRepository _divulgado;
    private readonly IProcessoSeletivoRepository _processos;
    private readonly IRegistroCodecsEnvelope _codecs;

    public DescricaoDosRascunhos(
        ICertameDivulgadoRepository divulgado,
        IProcessoSeletivoRepository processos,
        IRegistroCodecsEnvelope codecs)
    {
        ArgumentNullException.ThrowIfNull(divulgado);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(codecs);
        _divulgado = divulgado;
        _processos = processos;
        _codecs = codecs;
    }

    public async Task<Result<string>> ObterAsync(
        Guid processoSeletivoId,
        bool estaEmRascunho,
        DateTimeOffset consultadoEm,
        CancellationToken cancellationToken)
    {
        if (estaEmRascunho)
            return Result<string>.Success(DescricaoPadrao);

        Result<VersaoDivulgada> versao = await VersaoDivulgadaResolver
            .ResolverAsync(processoSeletivoId, _divulgado, _processos, cancellationToken).ConfigureAwait(false);
        if (versao.IsFailure)
            return DescricaoDeVersaoAusente(versao.Error!);

        return DescricaoDaVersao(versao.Value!.Versao, consultadoEm);
    }

    private static Result<string> DescricaoDeVersaoAusente(DomainError erro) =>
        erro.Code == AcessoAoProcesso.NaoEncontrado().Code
            ? Result<string>.Success(DescricaoPadrao)
            : Result<string>.Failure(erro);

    private Result<string> DescricaoDaVersao(VersaoConfiguracao versao, DateTimeOffset consultadoEm)
    {
        Result<JanelaDaInscricao> janela = PeriodoDaInscricaoResolver.JanelaDa(versao, _codecs);
        if (janela.IsFailure)
            return DescricaoDeJanelaAusente(janela.Error!);

        string descricao = consultadoEm < janela.Value!.Inicio
            ? DescricaoPadrao
            : consultadoEm <= janela.Value.Fim ? "Em preenchimento" : "Não enviados";
        return Result<string>.Success(descricao);
    }

    private static Result<string> DescricaoDeJanelaAusente(DomainError erro) =>
        erro.Code == InscricaoErrorCodes.ProcessoSemInscricao
            ? Result<string>.Success(DescricaoPadrao)
            : Result<string>.Failure(erro);
}
