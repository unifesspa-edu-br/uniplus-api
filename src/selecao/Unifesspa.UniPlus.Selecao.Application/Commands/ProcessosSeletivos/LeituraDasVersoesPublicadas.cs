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

    // A recusa é nomeada: distingue a versão que o sistema não conhece da que ele conhece e não sabe reconstruir.
    private DomainError? PendenciaDeReidratacao(VersaoConfiguracao versao) => CapacidadeDa(versao) switch
    {
        null => new DomainError(
            "EnvelopeCodec.VersaoDesconhecida",
            $"A versão {versao.SchemaVersion} do envelope congelado não está no registro de codecs — não há como reconstruir esta configuração, e {Consequencia(versao)}."),
        { Reidratavel: false } capacidade => new DomainError(
            "EnvelopeCodec.VersaoNaoReidratavel",
            $"A versão {versao.SchemaVersion} do envelope é conhecida, mas não pode ser reidratada ({capacidade.MotivoDaRecusa}) — {Consequencia(versao)}."),
        _ => null,
    };

    private CapacidadeCodec? CapacidadeDa(VersaoConfiguracao versao) => registroCodecs.Capacidades
        .FirstOrDefault(c => string.Equals(c.SchemaVersion, versao.SchemaVersion, StringComparison.Ordinal));

    private static string Consequencia(VersaoConfiguracao versao) =>
        $"sem ler a versão {versao.NumeroVersao}, não há como saber que fatos cada formulário coletou em todas as versões publicadas";
}
