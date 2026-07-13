namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Implementação de <see cref="IRestauradorDeConfiguracao"/>: decodifica, repõe e
/// <b>prova</b> — nesta ordem, numa operação só.
/// </summary>
public sealed class RestauradorDeConfiguracao(IRegistroCodecsEnvelope registro) : IRestauradorDeConfiguracao
{
    /// <summary>
    /// A prova falhou: o agregado reposto não recanonicaliza nos bytes congelados. Alguma
    /// coisa se perdeu entre os bytes e as entidades — e é <b>exatamente</b> o caso em que
    /// prosseguir gravaria uma configuração empobrecida sem deixar rastro.
    /// </summary>
    public const string RoundTripDivergente = "EnvelopeCodec.RoundTripDivergente";

    public Result Restaurar(ProcessoSeletivo processo, VersaoConfiguracao versao)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(versao);

        Result<EnvelopeReidratado> reidratado = registro.Reidratar(versao);
        if (reidratado.IsFailure)
        {
            return Result.Failure(reidratado.Error!);
        }

        EnvelopeReidratado envelope = reidratado.Value!;

        // PROVA PRIMEIRO, APLICA DEPOIS — e a inversão não é estilo, é a diferença entre
        // uma garantia e uma esperança. Repor na raiz VIVA para só então recanonicalizar
        // deixaria o agregado *tracked* empobrecido quando a prova falhasse; qualquer
        // SaveChanges adiante, no mesmo escopo, gravaria o estrago. A atomicidade passaria a
        // depender de o handler lembrar de não salvar — exatamente o tipo de disciplina
        // informal que esta Feature existe para não precisar.
        //
        // A sombra tem a MESMA identidade (é o Id que as filhas recebem em VincularProcesso)
        // e nunca entra no change tracker.
        ProcessoSeletivo sombra = processo.SombraParaVerificacao();

        Result ensaio = sombra.RestaurarConfiguracaoCongelada(versao, envelope.Grafo);
        if (ensaio.IsFailure)
        {
            return ensaio;
        }

        // Recanonicaliza com o encoder DAQUELA versão — não com o corrente: no dia da 1.2, o
        // corrente emitiria bytes de 1.2, e a comparação com uma 1.1 congelada acusaria uma
        // divergência que nada tem a ver com a fidelidade da reidratação.
        //
        // A RetificacaoInfo é a ORIGINAL, recuperada do próprio bloco `retificacao`: ela não
        // vem do agregado (é parâmetro externo da canonicalização), e sem ela a versão N>1
        // recanonicalizaria com 17 blocos em vez de 18.
        Result<SnapshotCanonico> recodificado = registro.Recodificar(
            versao.SchemaVersion,
            new EntradaCanonicalizacao(sombra, envelope.Dados, envelope.HashDocumento, envelope.Retificacao));

        if (recodificado.IsFailure)
        {
            return Result.Failure(recodificado.Error!);
        }

        if (!recodificado.Value!.Bytes.AsSpan().SequenceEqual(versao.ConfiguracaoCongeladaCanonica))
        {
            return Result.Failure(new DomainError(
                RoundTripDivergente,
                $"A configuração reidratada da versão {versao.NumeroVersao} não reproduz os bytes congelados — " +
                "algum dado se perdeu na reconstrução. A restauração é recusada: repor uma configuração empobrecida " +
                "faria o certame publicado divergir do documento que o publicou, sem que nada acusasse."));
        }

        // Provado. Só agora a raiz viva é tocada — e esta chamada não tem como falhar por
        // outro motivo que a sombra não tenha encontrado: as duas validam o MESMO grafo,
        // contra o mesmo Tipo e o mesmo Status.
        return processo.RestaurarConfiguracaoCongelada(versao, envelope.Grafo);
    }
}
