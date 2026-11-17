namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Events;

/// <summary>A versão congelada e o evento que a anuncia.</summary>
internal sealed record VersaoEmitida(VersaoConfiguracao Versao, ProcessoPublicadoEvent Evento);

/// <summary>
/// A conferência do estado do processo nas transições que congelam uma versão da configuração, e as próprias transições: publicar e
/// retificar. O estado (status, motivo, versão anterior) é conflito e precede toda leitura e toda conferência da
/// configuração; a transição a reaplica como guarda e emite a versão que a raiz registra.
/// </summary>
public static class TransicoesDaVersao
{
    private const string CodigoDoMotivoObrigatorio = "ProcessoSeletivo.MotivoRetificacaoObrigatorio";

    /// <summary>O que impede publicar: só se publica o processo em rascunho (UNI-REQ-0019).</summary>
    public static Result ConferirEstadoParaPublicar(StatusProcesso status) =>
        ComoResultado(RecusaDaPublicacao(status));

    /// <summary>O que impede retificar (ADR-0101/0103): o status, o motivo e a versão corrente.</summary>
    public static Result ConferirEstadoParaRetificar(Guid processoId, StatusProcesso status, VersaoConfiguracao versaoAtual, string motivo)
    {
        ArgumentNullException.ThrowIfNull(versaoAtual);

        return ComoResultado(RecusaDaRetificacao(processoId, status, versaoAtual, motivo));
    }

    internal static Result<VersaoEmitida> Publicar(Guid processoId, StatusProcesso status, PedidoDeCongelamento pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ExigirConfiguracaoDoMesmoProcesso(processoId, pedido);

        if (RecusaDaPublicacao(status) is { } recusa)
        {
            return Result<VersaoEmitida>.Failure(recusa);
        }

        (VersaoConfiguracao versao, ProcessoPublicadoEvent evento) = EmissaoDeVersao.Abrir(processoId, pedido);
        return Result<VersaoEmitida>.Success(new VersaoEmitida(versao, evento));
    }

    /// <summary>
    /// Retifica um processo publicado (ADR-0101/0103). Os argumentos são conferidos antes
    /// da conferência do estado: antepor a recusa de estado faria um pedido nulo deixar de lançar.
    /// </summary>
    internal static Result<VersaoEmitida> Retificar(
        Guid processoId, StatusProcesso status, PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, string motivo)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(versaoAtual);
        ExigirConfiguracaoDoMesmoProcesso(processoId, pedido);

        return RecusaDaRetificacao(processoId, status, versaoAtual, motivo) is { } recusa
            ? Result<VersaoEmitida>.Failure(recusa)
            : Suceder(processoId, pedido, versaoAtual);
    }

    private static Result<VersaoEmitida> Suceder(Guid processoId, PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual)
    {
        (VersaoConfiguracao versao, ProcessoPublicadoEvent evento) = EmissaoDeVersao.Suceder(processoId, versaoAtual, pedido);

        return Result<VersaoEmitida>.Success(new VersaoEmitida(versao, evento));
    }

    private static DomainError? RecusaDaPublicacao(StatusProcesso status) =>
        status != StatusProcesso.Rascunho
            ? TransicaoInvalida($"Só é possível publicar um processo em rascunho — status atual: {status}.")
            : null;

    private static DomainError? RecusaDaRetificacao(Guid processoId, StatusProcesso status, VersaoConfiguracao versaoAtual, string motivo)
    {
        if (status != StatusProcesso.Publicado)
        {
            return TransicaoDeRetificacaoInvalida(status);
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return new DomainError(CodigoDoMotivoObrigatorio, "O motivo da retificação é obrigatório.");
        }

        // A cadeia de versões não atravessa certames: a numeração deriva dela.
        return versaoAtual.ProcessoSeletivoId != processoId
            ? new DomainError("VersaoConfiguracao.VersaoAnteriorDeOutroProcesso", "A versão corrente informada pertence a outro Processo Seletivo.")
            : null;
    }

    // A configuração apta nasce da conferência deste processo; a de outro é erro de programação, não recusa de negócio.
    private static void ExigirConfiguracaoDoMesmoProcesso(Guid processoId, PedidoDeCongelamento pedido)
    {
        if (pedido.ConfiguracaoApta.ProcessoId != processoId)
        {
            throw new InvalidOperationException("A configuração conferida pertence a outro processo.");
        }
    }

    private static DomainError TransicaoDeRetificacaoInvalida(StatusProcesso status) =>
        TransicaoInvalida($"Só é possível retificar um processo publicado — status atual: {status}.");

    private static DomainError TransicaoInvalida(string mensagem) => new("ProcessoSeletivo.TransicaoInvalida", mensagem);

    private static Result ComoResultado(DomainError? recusa) => recusa is null ? Result.Success() : Result.Failure(recusa);
}
