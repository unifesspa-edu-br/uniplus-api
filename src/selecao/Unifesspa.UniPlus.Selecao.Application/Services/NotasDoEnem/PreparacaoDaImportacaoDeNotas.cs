namespace Unifesspa.UniPlus.Selecao.Application.Services.NotasDoEnem;

using System.Security.Cryptography;

using Commands.NotasDoEnem;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Leiautes;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

/// <summary>Confere o escopo e o retorno do INEP e calcula os índices antes do bloqueio do processo.</summary>
public sealed class PreparacaoDaImportacaoDeNotas(ResolvedorDoEscopoDeNotasDoEnem escopos,
    IIndiceCegoDoCpf indices, IResolvedorFusoInstitucional fuso, TimeProvider relogio)
{
    public async Task<Result<ImportacaoDeNotasPreparada>> PrepararAsync(ImportarNotasDoEnemCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Result<EscopoDeNotasDoEnem> escopo = await escopos.ResolverAsync(
            command.ProcessoSeletivoId, command.EtapaProcessoId, command.Edicao, true, cancellationToken).ConfigureAwait(false);
        if (escopo.IsFailure)
        {
            return Result<ImportacaoDeNotasPreparada>.Failure(escopo.Error!);
        }
        DomainError? recusa = ResolvedorDoEscopoDeNotasDoEnem.ConferirInscricoesEncerradas(escopo.Value!, relogio.GetUtcNow(), fuso);
        return recusa is not null ? Result<ImportacaoDeNotasPreparada>.Failure(recusa)
            : await PrepararArquivoAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ImportacaoDeNotasPreparada>> PrepararArquivoAsync(ImportarNotasDoEnemCommand command, CancellationToken cancellationToken)
    {
        Result<ILeiauteDeArquivoDoInep> leiaute = ResolvedorDoEscopoDeNotasDoEnem.ObterLeiaute(
            command.LeiauteCodigo, command.LeiauteVersao, TipoDeLeiauteDoInep.Retorno);
        if (leiaute.IsFailure)
        {
            return Result<ImportacaoDeNotasPreparada>.Failure(leiaute.Error!);
        }
        LeiauteDeRetornoDoInep retorno = (LeiauteDeRetornoDoInep)leiaute.Value!;
        Result<ArquivoDeRetornoLido> lido = Ler(command.Conteudo, retorno);
        return lido.IsFailure ? Result<ImportacaoDeNotasPreparada>.Failure(lido.Error!)
            : await CalcularIndicesAsync(command.Conteudo, retorno, lido.Value!.Linhas, cancellationToken).ConfigureAwait(false);
    }

    private static Result<ArquivoDeRetornoLido> Ler(byte[] conteudo, LeiauteDeRetornoDoInep leiaute)
    {
        if (conteudo.Length == 0)
        {
            return Result<ArquivoDeRetornoLido>.Failure(RecusasDoArquivoDoInep.Vazio.Error!);
        }
        return conteudo.LongLength > LimitesDoArquivoDoInep.TamanhoMaximoEmBytes
            ? Result<ArquivoDeRetornoLido>.Failure(RecusasDoArquivoDoInep.GrandeDemais.Error!)
            : LeitorDeArquivoDoInep.Ler(conteudo, leiaute);
    }

    private async Task<Result<ImportacaoDeNotasPreparada>> CalcularIndicesAsync(byte[] conteudo, LeiauteDeRetornoDoInep leiaute,
        IReadOnlyList<LinhaDeRetorno> linhas, CancellationToken cancellationToken)
    {
        string hash = Convert.ToHexStringLower(SHA256.HashData(conteudo));
        IReadOnlyDictionary<string, IndiceCegoDoCpf> calculados = await indices.CalcularAsync(
            [.. linhas.Select(static linha => linha.Cpf).Distinct(StringComparer.Ordinal)], cancellationToken).ConfigureAwait(false);
        return Result<ImportacaoDeNotasPreparada>.Success(new(leiaute, hash, linhas, calculados));
    }
}
