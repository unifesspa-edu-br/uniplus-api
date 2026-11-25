namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Events;

/// <summary>A versão congelada e o evento que a anuncia.</summary>
internal sealed record VersaoEmitida(VersaoConfiguracao Versao, ProcessoPublicadoEvent Evento);

/// <summary>
/// A conferência do estado do processo nas transições que congelam uma versão da configuração, e as próprias transições: publicar e
/// retificar. O estado (situação, motivo, versão anterior) é conflito e precede toda leitura e toda conferência da
/// configuração; a transição a reaplica como guarda e emite a versão que a raiz registra.
/// </summary>
public static class TransicoesDaVersao
{
    private const string CodigoDoMotivoObrigatorio = "ProcessoSeletivo.MotivoRetificacaoObrigatorio";

    /// <summary>O que impede publicar: só se publica o processo em rascunho (UNI-REQ-0019).</summary>
    public static Result ConferirEstadoParaPublicar(SituacaoDoProcessoSeletivo situacao) =>
        ComoResultado(RecusaDaPublicacao(situacao));

    /// <summary>O que impede retificar (ADR-0101/0103): a situação, o motivo e a versão corrente.</summary>
    public static Result ConferirEstadoParaRetificar(Guid processoId, SituacaoDoProcessoSeletivo situacao, VersaoConfiguracao versaoAtual, string motivo)
    {
        ArgumentNullException.ThrowIfNull(versaoAtual);

        return ComoResultado(RecusaDaRetificacao(processoId, situacao, versaoAtual, motivo));
    }

    internal static Result<VersaoEmitida> Publicar(Guid processoId, SituacaoDoProcessoSeletivo situacao, PedidoDeCongelamento pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ExigirConfiguracaoDoMesmoProcesso(processoId, pedido);

        if (RecusaDaPublicacao(situacao) is { } recusa)
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
        Guid processoId, SituacaoDoProcessoSeletivo situacao, PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, string motivo)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(versaoAtual);
        ExigirConfiguracaoDoMesmoProcesso(processoId, pedido);

        return RecusaDaRetificacao(processoId, situacao, versaoAtual, motivo) is { } recusa
            ? Result<VersaoEmitida>.Failure(recusa)
            : Suceder(processoId, pedido, versaoAtual);
    }

    private static Result<VersaoEmitida> Suceder(Guid processoId, PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual)
    {
        (VersaoConfiguracao versao, ProcessoPublicadoEvent evento) = EmissaoDeVersao.Suceder(processoId, versaoAtual, pedido);

        return Result<VersaoEmitida>.Success(new VersaoEmitida(versao, evento));
    }

    private static DomainError? RecusaDaPublicacao(SituacaoDoProcessoSeletivo situacao) =>
        situacao != SituacaoDoProcessoSeletivo.Rascunho
            ? TransicaoInvalida($"Só é possível publicar um processo em rascunho — situação atual: {situacao.Rotulo()}.")
            : null;

    private static DomainError? RecusaDaRetificacao(Guid processoId, SituacaoDoProcessoSeletivo situacao, VersaoConfiguracao versaoAtual, string motivo)
    {
        if (situacao != SituacaoDoProcessoSeletivo.Publicado)
        {
            return TransicaoDeRetificacaoInvalida(situacao);
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return new DomainError(CodigoDoMotivoObrigatorio, "O motivo da retificação é obrigatório.");
        }

        // A versão corrente vem do repositório do próprio processo; outra é erro de programação.
        ArgumentOutOfRangeException.ThrowIfNotEqual(versaoAtual.ProcessoSeletivoId, processoId, nameof(versaoAtual));
        return null;
    }

    // A configuração apta nasce da conferência deste processo; a de outro é erro de programação, não recusa de negócio.
    private static void ExigirConfiguracaoDoMesmoProcesso(Guid processoId, PedidoDeCongelamento pedido)
    {
        if (pedido.ConfiguracaoApta.ProcessoId != processoId)
        {
            throw new InvalidOperationException("A configuração conferida pertence a outro processo.");
        }
    }

    private static DomainError TransicaoDeRetificacaoInvalida(SituacaoDoProcessoSeletivo situacao) =>
        TransicaoInvalida($"Só é possível retificar um processo publicado — situação atual: {situacao.Rotulo()}.");

    private static DomainError TransicaoInvalida(string mensagem) => new("ProcessoSeletivo.TransicaoInvalida", mensagem);

    private static Result ComoResultado(DomainError? recusa) => recusa is null ? Result.Success() : Result.Failure(recusa);
}
