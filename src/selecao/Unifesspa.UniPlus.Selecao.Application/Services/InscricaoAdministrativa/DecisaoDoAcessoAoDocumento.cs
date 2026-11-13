namespace Unifesspa.UniPlus.Selecao.Application.Services.InscricaoAdministrativa;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// A decisão de acesso de quem opera o processo ao documento de uma inscrição (ADR-0136, ADR-0153). A ordem: o arquivo e o
/// vínculo da inscrição, por leituras que não decifram nada; a inscrição alcançada por consultar as inscrições na unidade
/// dona do processo; e então a permissão que a classificação congelada da exigência pede. O que não existe, é de outra
/// inscrição ou processo, ou é de inscrição em rascunho recebe a recusa sem decisão gravada, pois não há recurso sobre o
/// qual decidir. A recusa por falta de permissão cobre numa mesma resposta o que não existe e o que a pessoa não pode ler.
/// </summary>
public sealed class DecisaoDoAcessoAoDocumento
{
    private readonly IVerificadorDeAcesso _verificador;
    private readonly IPortaDePermissaoPorPar _porta;
    private readonly IProcessoSeletivoRepository _processos;
    private readonly IInscricaoRepository _inscricoes;
    private readonly IDocumentoDaInscricaoRepository _documentos;
    private readonly ClassificacaoDaExigenciaDoArquivo _classificacao;

    public DecisaoDoAcessoAoDocumento(
        IVerificadorDeAcesso verificador,
        IPortaDePermissaoPorPar porta,
        IProcessoSeletivoRepository processos,
        IInscricaoRepository inscricoes,
        IDocumentoDaInscricaoRepository documentos,
        ClassificacaoDaExigenciaDoArquivo classificacao)
    {
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(porta);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(inscricoes);
        ArgumentNullException.ThrowIfNull(documentos);
        ArgumentNullException.ThrowIfNull(classificacao);

        _verificador = verificador;
        _porta = porta;
        _processos = processos;
        _inscricoes = inscricoes;
        _documentos = documentos;
        _classificacao = classificacao;
    }

    /// <summary>O arquivo, quando a pessoa pode abri-lo; senão o erro com que a rota responde.</summary>
    public async Task<Result<ArquivoDaApresentacao>> DecidirAsync(
        Guid processoId, Guid inscricaoId, Guid arquivoId, CancellationToken cancellationToken)
    {
        ArquivoDaApresentacao? arquivo = await _documentos.ObterArquivoAsync(arquivoId, cancellationToken).ConfigureAwait(false);
        if (arquivo is null
            || arquivo.InscricaoId != inscricaoId
            || await AcessoAInscricao
                .ConferirVinculoAsync(_processos, _inscricoes, processoId, inscricaoId, cancellationToken)
                .ConfigureAwait(false) is not Vinculo conferido)
        {
            return Result<ArquivoDaApresentacao>.Failure(AcessoAoDocumentoDaInscricao.ArquivoNaoEncontrado);
        }

        return await DecidirDoArquivoAsync(arquivo, conferido, processoId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ArquivoDaApresentacao>> DecidirDoArquivoAsync(
        ArquivoDaApresentacao arquivo, Vinculo conferido, Guid processoId, CancellationToken cancellationToken)
    {
        DomainError? recusa = await RecusaDaInscricaoAsync(arquivo, conferido, processoId, cancellationToken).ConfigureAwait(false)
            ?? await RecusaDaClasseAsync(arquivo, conferido, processoId, cancellationToken).ConfigureAwait(false);

        return recusa is null ? Result<ArquivoDaApresentacao>.Success(arquivo) : Result<ArquivoDaApresentacao>.Failure(recusa);
    }

    private async Task<DomainError?> RecusaDaInscricaoAsync(
        ArquivoDaApresentacao arquivo, Vinculo conferido, Guid processoId, CancellationToken cancellationToken)
    {
        Result<InscricaoAlcancada> alcancada = await AcessoDoOperadorAInscricao
            .AlcancarAsync(
                _verificador, conferido, processoId, arquivo.InscricaoId, AcessoAoDocumentoDaInscricao.ArquivoNaoEncontrado, cancellationToken)
            .ConfigureAwait(false);

        return alcancada.Error;
    }

    private async Task<DomainError?> RecusaDaClasseAsync(
        ArquivoDaApresentacao arquivo, Vinculo conferido, Guid processoId, CancellationToken cancellationToken)
    {
        ClassificacaoDeProtecao classificacao = await _classificacao.ResolverAsync(arquivo, conferido.Inscricao, cancellationToken).ConfigureAwait(false);
        if (_porta.Requisito(NaturezaDoItem.Arquivo, classificacao) is not { } requisito)
        {
            return AcessoAoDocumentoDaInscricao.ArquivoNaoEncontrado;
        }

        ResourceContext resource = AcessoAoProcesso.ResourceDoDocumento(
            conferido.UnidadeId, processoId, arquivo.InscricaoId, arquivo.Id, classificacao);
        return await AcessoAInscricao.DecidirAsync(_verificador, conferido, requisito, resource, cancellationToken).ConfigureAwait(false) is Recusada recusada
            ? AcessoAInscricao.Erro(recusada, AcessoAoDocumentoDaInscricao.ArquivoNaoEncontrado)
            : null;
    }
}
