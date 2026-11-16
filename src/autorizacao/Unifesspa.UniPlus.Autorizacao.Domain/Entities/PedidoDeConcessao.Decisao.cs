namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>As etapas de decisão do pedido: aprovação, recusa, conferências e dispensa da segunda aprovação.</summary>
public sealed partial class PedidoDeConcessao
{
    /// <summary>
    /// Aprova a etapa corrente. A primeira etapa é sempre a do gestor: se o perfil, naquele instante, reúne permissão
    /// pessoal ou sensível o pedido segue para o Representante do controlador, senão a aprovação conclui. A segunda
    /// aprovação só é dispensada na designação do primeiro Representante, quando não há outro vigente que a decida
    /// (ADR-0162); a decisão do gestor registra a dispensa e o motivo. O pedido protegido de quem já é Representante
    /// passa pela segunda etapa, decidida por outro Representante.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O fim da vigência é definido pelo gestor, na etapa dele; o Representante não o informa e a concessão herda o que o
    /// gestor gravou. No perfil que não admite prazo, o prazo gravado antes da regra recusa a segunda etapa.
    /// </para>
    /// <para>
    /// O perfil que mudou entre as etapas, ou o prazo que venceu antes da segunda etapa, devolve o pedido ao gestor e
    /// responde <see cref="PedidoErrorCodes.PerfilAlterado"/> ou <see cref="PedidoErrorCodes.PrazoVencido"/>: o pedido
    /// muda de estado mesmo na falha, e quem chama precisa gravá-lo.
    /// </para>
    /// </remarks>
    public Result<DesfechoDaAprovacao> Aprovar(
        Identidade decisor,
        PerfilAvaliado perfil,
        ContextoDaAprovacao contexto,
        DateTimeOffset instante,
        DateTimeOffset? validaAte = null)
    {
        ArgumentNullException.ThrowIfNull(decisor);
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(contexto);

        return Andamento.EtapaAtual is { } etapa
            ? AprovarNaEtapa(new AprovacaoNaEtapa(decisor, etapa, perfil, contexto, instante, validaAte))
            : Result<DesfechoDaAprovacao>.Failure(SituacaoInvalida("O pedido não aguarda aprovação."));
    }

    /// <summary>Recusa o pedido na etapa corrente, com motivo. Encerra o pedido, sem concessão.</summary>
    public Result Recusar(Identidade decisor, string? motivo, PerfilAvaliado perfil, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(decisor);
        ArgumentNullException.ThrowIfNull(perfil);

        return Andamento.EtapaAtual is { } etapa
            ? RecusarNaEtapa(new RecusaNaEtapa(decisor, etapa, MotivosDaVersao.Normalizar(motivo), perfil, instante))
            : Result.Failure(SituacaoInvalida("O pedido não aguarda decisão."));
    }

    private Result<DesfechoDaAprovacao> AprovarNaEtapa(AprovacaoNaEtapa aprovacao)
    {
        List<FieldError> erros = aprovacao.Recusas(Segregacao, Andamento.ValidaAte);
        if (erros.Count > 0)
        {
            return Result<DesfechoDaAprovacao>.ValidationFailure(erros);
        }

        // Defesa contra estado corrompido: a máquina de estados não leva ao gestor o pedido com termo e sem aceite.
        if (Termo is not null && AceiteDoTermo is null)
        {
            return Result<DesfechoDaAprovacao>.Failure(SituacaoInvalida("O pedido com termo fixado só se decide depois do aceite do beneficiário."));
        }

        return aprovacao.Devolucao(_decisoes, Andamento, Termo) is { } devolucao
            ? Result<DesfechoDaAprovacao>.Failure(Devolver(aprovacao, devolucao))
            : RegistrarAprovacao(aprovacao);
    }

    private Result RecusarNaEtapa(RecusaNaEtapa recusa)
    {
        List<FieldError> erros = recusa.Recusas(Segregacao);
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        _decisoes.Add(DecisaoDoPedido.Registrar(Id, recusa.Etapa, recusa.Ato, ResultadoDaDecisao.Recusado, recusa.Motivo, recusa.Perfil, null));
        Andamento = Andamento.Encerrado(SituacaoDoPedido.Recusado, recusa.Instante);

        return Result.Success();
    }

    // A tentativa fica registrada com quem a fez e o perfil que ele viu. O pedido volta ao gestor, ou, no perfil que passou a
    // exigir o termo depois da criação, é devolvido: nenhuma concessão protegida nasce sem o aceite do termo (ADR-0162).
    private DomainError Devolver(AprovacaoNaEtapa aprovacao, DevolucaoNaAprovacao devolucao)
    {
        _decisoes.Add(DecisaoDoPedido.Registrar(Id, aprovacao.Etapa, aprovacao.Ato, devolucao.Resultado, null, aprovacao.Perfil, null));
        Andamento = devolucao.VoltaAoGestor ? Andamento.DeVoltaAoGestor() : Andamento.Encerrado(SituacaoDoPedido.Devolvido, aprovacao.Instante);

        return devolucao.Erro;
    }

    private Result<DesfechoDaAprovacao> RegistrarAprovacao(AprovacaoNaEtapa aprovacao)
    {
        _decisoes.Add(DecisaoDoPedido.Registrar(
            Id, aprovacao.Etapa, aprovacao.Ato, ResultadoDaDecisao.Aprovado, null, aprovacao.Perfil, aprovacao.Dispensa));

        if (aprovacao.Etapa == EtapaDoPedido.Gestor)
        {
            Andamento = Andamento.ComPrazo(aprovacao.ValidaAte);
        }

        Andamento = aprovacao.Conclui ? Andamento.Encerrado(SituacaoDoPedido.Aprovado, aprovacao.Instante) : Andamento.Em(SituacaoDoPedido.AguardandoProtecaoDeDados);

        return Result<DesfechoDaAprovacao>.Success(aprovacao.Desfecho);
    }
}
