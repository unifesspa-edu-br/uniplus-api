namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>A versão congelada, o evento que a anuncia e se a transição encerra a sessão editorial.</summary>
internal sealed record VersaoEmitida(VersaoConfiguracao Versao, ProcessoPublicadoEvent Evento, bool EncerraSessao);

/// <summary>
/// As transições que congelam uma versão da configuração: publicar, retificar de uma vez e fechar a
/// sessão editorial. Cada uma confere os argumentos, o status e os portões de prontidão, nessa ordem, e
/// devolve a versão emitida para a raiz registrar. Nenhuma incrementa a revisão da sessão.
/// </summary>
internal static class TransicoesDaVersao
{
    /// <summary>Publica o processo em rascunho (UNI-REQ-0019). Não recebe precondição: o rascunho não tem sessão.</summary>
    internal static Result<VersaoEmitida> Publicar(EstadoDaConfiguracao estado, PedidoDeCongelamento pedido)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(pedido.Dados, "dados");
        ArgumentNullException.ThrowIfNull(pedido.Documento.Canonico, "configuracaoCongeladaCanonica");
        ArgumentNullException.ThrowIfNull(pedido.Clock, "clock");
        ArgumentNullException.ThrowIfNull(pedido.Contexto, "contexto");
        ArgumentNullException.ThrowIfNull(pedido.TiposDeAtoUnicos, "tiposDeAtoUnicos");

        if (estado.Status != StatusProcesso.Rascunho)
        {
            return TransicaoInvalida($"Só é possível publicar um processo em rascunho — status atual: {estado.Status}.");
        }

        if (ProntidaoParaCongelar.PrimeiraPendencia(ProntidaoParaCongelar.Portoes.Para(estado, pedido)) is { } pendencia)
        {
            return Result<VersaoEmitida>.Failure(pendencia);
        }

        (VersaoConfiguracao versao, ProcessoPublicadoEvent evento) = EmissaoDeVersao.Abrir(estado.ProcessoId, pedido);
        return Result<VersaoEmitida>.Success(new VersaoEmitida(versao, evento, EncerraSessao: false));
    }

    /// <summary>
    /// Retifica de uma vez um processo publicado, sem sessão editorial (ADR-0101/0103). Os argumentos e o
    /// status são conferidos antes da sessão: antepor a recusa de sessão aberta faria um pedido nulo
    /// deixar de lançar e um estado inválido deixar de acusar a transição.
    /// </summary>
    internal static Result<VersaoEmitida> Retificar(
        EstadoDaConfiguracao estado, PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, string motivo)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(pedido.Dados, "dados");
        ArgumentNullException.ThrowIfNull(versaoAtual);
        ArgumentNullException.ThrowIfNull(pedido.Documento.Canonico, "configuracaoCongeladaCanonica");
        ArgumentNullException.ThrowIfNull(pedido.Clock, "clock");

        if (estado.Status != StatusProcesso.Publicado)
        {
            return TransicaoInvalida($"Só é possível retificar um processo publicado — status atual: {estado.Status}.");
        }

        // O atalho e a sessão retificam o mesmo ato; correndo juntos, a versão seguinte sairia da
        // configuração viva que a sessão está editando (ADR-0110 D7).
        return estado.Rascunho is not null
            ? Result<VersaoEmitida>.Failure(RascunhoRetificacao.JaAberta())
            : Suceder(estado, pedido, versaoAtual, motivo, encerraSessao: false);
    }

    /// <summary>
    /// Fecha a sessão editorial: congela a versão seguinte com a configuração editada e o motivo do
    /// rascunho, onde foi normalizado uma única vez. A sessão só encerra depois de a versão estar
    /// decidida; se o congelamento for recusado, ela segue aberta para o administrador corrigir.
    /// </summary>
    internal static Result<VersaoEmitida> Fechar(
        EstadoDaConfiguracao estado, PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, RascunhoRetificacao sessao)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(sessao);

        return Suceder(estado, pedido, versaoAtual, sessao.Motivo, encerraSessao: true);
    }

    private static Result<VersaoEmitida> Suceder(
        EstadoDaConfiguracao estado, PedidoDeCongelamento pedido, VersaoConfiguracao versaoAtual, string motivo, bool encerraSessao)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(pedido.Dados, "dados");
        ArgumentNullException.ThrowIfNull(versaoAtual);
        ArgumentNullException.ThrowIfNull(pedido.Contexto, "contexto");
        ArgumentNullException.ThrowIfNull(pedido.Documento.Canonico, "configuracaoCongeladaCanonica");
        ArgumentNullException.ThrowIfNull(pedido.Clock, "clock");
        ArgumentNullException.ThrowIfNull(pedido.TiposDeAtoUnicos, "tiposDeAtoUnicos");

        if (estado.Status != StatusProcesso.Publicado)
        {
            return TransicaoInvalida($"Só é possível retificar um processo publicado — status atual: {estado.Status}.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Falha("ProcessoSeletivo.MotivoRetificacaoObrigatorio", "O motivo da retificação é obrigatório.");
        }

        // A cadeia de versões não atravessa certames: a numeração deriva dela.
        if (versaoAtual.ProcessoSeletivoId != estado.ProcessoId)
        {
            return Falha("VersaoConfiguracao.VersaoAnteriorDeOutroProcesso", "A versão corrente informada pertence a outro Processo Seletivo.");
        }

        // Uma versão vinculante congelada com configuração incompleta é irreparável, como na publicação.
        if (ProntidaoParaCongelar.PrimeiraPendencia(ProntidaoParaCongelar.Portoes.Para(estado, pedido)) is { } pendencia)
        {
            return Result<VersaoEmitida>.Failure(pendencia);
        }

        (VersaoConfiguracao versao, ProcessoPublicadoEvent evento) = EmissaoDeVersao.Suceder(estado.ProcessoId, versaoAtual, pedido);
        return Result<VersaoEmitida>.Success(new VersaoEmitida(versao, evento, encerraSessao));
    }

    private static Result<VersaoEmitida> TransicaoInvalida(string mensagem) =>
        Falha("ProcessoSeletivo.TransicaoInvalida", mensagem);

    private static Result<VersaoEmitida> Falha(string codigo, string mensagem) =>
        Result<VersaoEmitida>.Failure(new DomainError(codigo, mensagem));
}
