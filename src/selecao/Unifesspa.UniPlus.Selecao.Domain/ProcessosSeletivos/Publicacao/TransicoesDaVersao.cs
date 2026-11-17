namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>A versão congelada, o evento que a anuncia e se a transição encerra a sessão editorial.</summary>
internal sealed record VersaoEmitida(VersaoConfiguracao Versao, ProcessoPublicadoEvent Evento, bool EncerraSessao);

/// <summary>
/// A conferência do estado das transições que congelam uma versão da configuração, e as próprias transições: publicar,
/// retificar de uma vez e fechar a sessão editorial. O estado (status, motivo, versão anterior, rascunho aberto) é conflito e
/// precede toda leitura e toda conferência da configuração; a transição a reaplica como guarda e emite a versão que a raiz
/// registra. Nenhuma incrementa a revisão da sessão.
/// </summary>
public static class TransicoesDaVersao
{
    private const string CodigoDoMotivoObrigatorio = "ProcessoSeletivo.MotivoRetificacaoObrigatorio";

    /// <summary>O que impede publicar: só se publica o processo em rascunho (UNI-REQ-0019), que não tem sessão.</summary>
    public static Result ConferirEstadoParaPublicar(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return ComoResultado(RecusaDaPublicacao(estado));
    }

    /// <summary>O que impede retificar de uma vez (ADR-0101/0103): o status, a sessão aberta, o motivo e a versão corrente.</summary>
    public static Result ConferirEstadoParaRetificar(EstadoDaConfiguracao estado, VersaoConfiguracao versaoAtual, string motivo)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(versaoAtual);

        return ComoResultado(RecusaDaRetificacao(estado, versaoAtual, motivo));
    }

    /// <summary>O que impede fechar a sessão: o status, o motivo da sessão e a versão corrente.</summary>
    public static Result ConferirEstadoParaFechar(EstadoDaConfiguracao estado, VersaoConfiguracao versaoAtual, RascunhoRetificacao sessao)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(versaoAtual);
        ArgumentNullException.ThrowIfNull(sessao);

        return ComoResultado(RecusaDaVersaoSeguinte(estado, versaoAtual, sessao.Motivo));
    }

    internal static Result<VersaoEmitida> Publicar(EstadoDaConfiguracao estado, PedidoDeCongelamento pedido)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(pedido);
        ExigirConfiguracaoDoMesmoProcesso(estado, pedido);

        if (RecusaDaPublicacao(estado) is { } recusa)
        {
            return Result<VersaoEmitida>.Failure(recusa);
        }

        (VersaoConfiguracao versao, ProcessoPublicadoEvent evento) = EmissaoDeVersao.Abrir(estado.ProcessoId, pedido);
        return Result<VersaoEmitida>.Success(new VersaoEmitida(versao, evento, EncerraSessao: false));
    }

    /// <summary>
    /// Retifica de uma vez um processo publicado, sem sessão editorial (ADR-0101/0103). Os argumentos são conferidos antes
    /// da conferência do estado: antepor a recusa de estado faria um pedido nulo deixar de lançar.
    /// </summary>
    internal static Result<VersaoEmitida> Retificar(
        EstadoDaConfiguracao estado, PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, string motivo)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(versaoAtual);
        ExigirConfiguracaoDoMesmoProcesso(estado, pedido);

        return RecusaDaRetificacao(estado, versaoAtual, motivo) is { } recusa
            ? Result<VersaoEmitida>.Failure(recusa)
            : Suceder(estado, pedido, versaoAtual, encerraSessao: false);
    }

    /// <summary>
    /// Fecha a sessão editorial: congela a versão seguinte com a configuração editada e o motivo do rascunho, onde foi
    /// normalizado uma única vez. A sessão só encerra depois de a versão estar decidida; se o congelamento for recusado, ela
    /// segue aberta para o administrador corrigir.
    /// </summary>
    internal static Result<VersaoEmitida> Fechar(
        EstadoDaConfiguracao estado, PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, RascunhoRetificacao sessao)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(versaoAtual);
        ArgumentNullException.ThrowIfNull(sessao);
        ExigirConfiguracaoDoMesmoProcesso(estado, pedido);

        return RecusaDaVersaoSeguinte(estado, versaoAtual, sessao.Motivo) is { } recusa
            ? Result<VersaoEmitida>.Failure(recusa)
            : Suceder(estado, pedido, versaoAtual, encerraSessao: true);
    }

    private static Result<VersaoEmitida> Suceder(
        EstadoDaConfiguracao estado, PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, bool encerraSessao)
    {
        (VersaoConfiguracao versao, ProcessoPublicadoEvent evento) = EmissaoDeVersao.Suceder(estado.ProcessoId, versaoAtual, pedido);

        return Result<VersaoEmitida>.Success(new VersaoEmitida(versao, evento, encerraSessao));
    }

    private static DomainError? RecusaDaPublicacao(EstadoDaConfiguracao estado) =>
        estado.Status != StatusProcesso.Rascunho
            ? TransicaoInvalida($"Só é possível publicar um processo em rascunho — status atual: {estado.Status}.")
            : null;

    // O atalho e a sessão retificam o mesmo ato; correndo juntos, a versão seguinte sairia da configuração viva que a sessão
    // está editando (ADR-0110 D7).
    private static DomainError? RecusaDaRetificacao(EstadoDaConfiguracao estado, VersaoConfiguracao versaoAtual, string motivo) =>
        estado.Status != StatusProcesso.Publicado
            ? TransicaoDeRetificacaoInvalida(estado)
            : estado.Rascunho is not null
                ? RascunhoRetificacao.JaAberta()
                : RecusaDaVersaoSeguinte(estado, versaoAtual, motivo);

    private static DomainError? RecusaDaVersaoSeguinte(EstadoDaConfiguracao estado, VersaoConfiguracao versaoAtual, string motivo)
    {
        if (estado.Status != StatusProcesso.Publicado)
        {
            return TransicaoDeRetificacaoInvalida(estado);
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return new DomainError(CodigoDoMotivoObrigatorio, "O motivo da retificação é obrigatório.");
        }

        // A cadeia de versões não atravessa certames: a numeração deriva dela.
        return versaoAtual.ProcessoSeletivoId != estado.ProcessoId
            ? new DomainError("VersaoConfiguracao.VersaoAnteriorDeOutroProcesso", "A versão corrente informada pertence a outro Processo Seletivo.")
            : null;
    }

    // A configuração apta nasce da conferência deste processo; a de outro é erro de programação, não recusa de negócio.
    private static void ExigirConfiguracaoDoMesmoProcesso(EstadoDaConfiguracao estado, PedidoDeCongelamento pedido)
    {
        if (pedido.ConfiguracaoApta.ProcessoId != estado.ProcessoId)
        {
            throw new InvalidOperationException("A configuração conferida pertence a outro processo.");
        }
    }

    private static DomainError TransicaoDeRetificacaoInvalida(EstadoDaConfiguracao estado) =>
        TransicaoInvalida($"Só é possível retificar um processo publicado — status atual: {estado.Status}.");

    private static DomainError TransicaoInvalida(string mensagem) => new("ProcessoSeletivo.TransicaoInvalida", mensagem);

    private static Result ComoResultado(DomainError? recusa) => recusa is null ? Result.Success() : Result.Failure(recusa);
}
