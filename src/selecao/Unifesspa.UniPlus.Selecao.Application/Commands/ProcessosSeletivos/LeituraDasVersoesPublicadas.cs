namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Lê as versões publicadas do processo e o que elas congelaram de cada fato coletado, uma vez por retificação
/// (UNI-REQ-0144). Sem versão, não há o que ler. Toda versão tem de ser reidratável: a que o sistema não conhece ou
/// não sabe reidratar recusa, porque sem lê-la não há como saber que fatos cada formulário coletou.
/// </summary>
public sealed class LeituraDasVersoesPublicadas(IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope registroCodecs)
{
    internal async Task<Result<FatosDasVersoesPublicadas?>> LerAsync(Guid processoSeletivoId, CancellationToken cancellationToken)
    {
        IReadOnlyList<VersaoConfiguracao> versoes = await processos
            .ObterVersoesAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (versoes.Count == 0)
        {
            return Result<FatosDasVersoesPublicadas?>.Success(null);
        }

        Result<FatosDasVersoesPublicadas> fatos = Reidratar(versoes);
        return fatos.IsFailure
            ? Result<FatosDasVersoesPublicadas?>.Failure(fatos.Error!)
            : Result<FatosDasVersoesPublicadas?>.Success(fatos.Value);
    }

    // As versões vão da primeira à base, que é a última.
    private Result<FatosDasVersoesPublicadas> Reidratar(IReadOnlyList<VersaoConfiguracao> versoes)
    {
        List<EnvelopeReidratado> reidratadas = new(versoes.Count);
        foreach (VersaoConfiguracao versao in versoes)
        {
            Result<EnvelopeReidratado> reidratada = ReidratarVersao(versao);
            if (reidratada.IsFailure)
            {
                return Result<FatosDasVersoesPublicadas>.Failure(reidratada.Error!);
            }

            reidratadas.Add(reidratada.Value!);
        }

        return Result<FatosDasVersoesPublicadas>.Success(Fatos(reidratadas));
    }

    private static FatosDasVersoesPublicadas Fatos(List<EnvelopeReidratado> reidratadas) => FatosDasVersoesPublicadas.De(
        [.. reidratadas.Select(static r => r.Configuracao)],
        [.. reidratadas.Select(ConferenciaDosEixosDosFatosColetados.DaVersao)]);

    private Result<EnvelopeReidratado> ReidratarVersao(VersaoConfiguracao versao) =>
        PendenciaDeReidratacao(versao) is { } pendencia
            ? Result<EnvelopeReidratado>.Failure(pendencia)
            : registroCodecs.Reidratar(versao);

    private DomainError? PendenciaDeReidratacao(VersaoConfiguracao versao) =>
        registroCodecs.LeAVersao(versao.SchemaVersion) ? null : new DomainError(
            ErrosCodecEnvelope.VersaoDesconhecida,
            $"A versão {versao.SchemaVersion} do envelope congelado não é reconhecida — {Consequencia(versao)}.");

    private static string Consequencia(VersaoConfiguracao versao) =>
        $"sem ler a versão {versao.NumeroVersao}, não há como saber que fatos cada formulário coletou em todas as versões publicadas";
}
