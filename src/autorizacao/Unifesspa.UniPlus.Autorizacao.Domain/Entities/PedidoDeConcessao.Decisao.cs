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

        if (Andamento.EtapaAtual is not { } etapa)
        {
            return Result.Failure(SituacaoInvalida("O pedido não aguarda decisão."));
        }

        string? motivoNormalizado = MotivosDaVersao.Normalizar(motivo);
        List<FieldError> erros = ErrosDaRecusa(decisor, etapa, motivoNormalizado);
        return erros.Count > 0
            ? Result.ValidationFailure(erros)
            : RegistrarRecusa(decisor, etapa, motivoNormalizado, perfil, instante);
    }

    private Result<DesfechoDaAprovacao> AprovarNaEtapa(AprovacaoNaEtapa aprovacao)
    {
        List<FieldError> erros = ErrosDaAprovacao(aprovacao);
        if (erros.Count > 0)
        {
            return Result<DesfechoDaAprovacao>.ValidationFailure(erros);
        }

        if (RecusaPeloTermo(aprovacao) is { } recusa)
        {
            return Result<DesfechoDaAprovacao>.Failure(recusa);
        }

        return DevolverAoGestorSeMudou(aprovacao.Decisor, aprovacao.Etapa, aprovacao.Perfil, aprovacao.Instante) is { } devolucao
            ? Result<DesfechoDaAprovacao>.Failure(devolucao)
            : RegistrarAprovacao(aprovacao);
    }

    private List<FieldError> ErrosDaAprovacao(AprovacaoNaEtapa aprovacao)
    {
        List<FieldError> erros = [];
        Segregacao.Conferir(aprovacao.Decisor, aprovacao.Etapa, erros);

        if (!aprovacao.Perfil.Ativo)
        {
            erros.Add(new("perfilId", new DomainError(
                PedidoErrorCodes.PerfilInativo,
                "O perfil de acesso está desativado e não aceita aprovação.")));
        }

        ValidadeDoPedido.ConferirNaEtapa(aprovacao, Andamento.ValidaAte, erros);
        return erros;
    }

    private List<FieldError> ErrosDaRecusa(Identidade decisor, EtapaDoPedido etapa, string? motivoNormalizado)
    {
        List<FieldError> erros = [];
        Segregacao.Conferir(decisor, etapa, erros);

        if (motivoNormalizado is null)
        {
            erros.Add(new("motivo", new DomainError(
                PedidoErrorCodes.MotivoInvalido,
                $"O motivo da recusa é obrigatório e deve ter até {MotivosDaVersao.MaxLength} caracteres.")));
        }

        return erros;
    }

    private Result RegistrarRecusa(
        Identidade decisor, EtapaDoPedido etapa, string? motivo, PerfilAvaliado perfil, DateTimeOffset instante)
    {
        _decisoes.Add(DecisaoDoPedido.Registrar(Id, etapa, new AtoDoAgente(decisor, instante), ResultadoDaDecisao.Recusado, motivo, perfil, null));
        Andamento = Andamento.Encerrado(SituacaoDoPedido.Recusado, instante);
        return Result.Success();
    }

    // Nenhuma etapa concede o que a anterior não avaliou: o gestor decide de novo sobre o perfil de agora, e o prazo que
    // venceu volta ao gestor porque só ele o define. A tentativa fica registrada com quem a fez e o perfil que ele viu.
    private DomainError? DevolverAoGestorSeMudou(Identidade decisor, EtapaDoPedido etapa, PerfilAvaliado perfil, DateTimeOffset instante)
    {
        if (etapa != EtapaDoPedido.ProtecaoDeDados || MotivoDaDevolucao(perfil, instante) is not { } motivo)
        {
            return null;
        }

        return DevolverAoGestor(decisor, etapa, perfil, instante, motivo.Resultado, motivo.Erro);
    }

    private (ResultadoDaDecisao Resultado, DomainError Erro)? MotivoDaDevolucao(PerfilAvaliado perfil, DateTimeOffset instante)
    {
        if (!_decisoes.MesmoPerfilQueOGestorAvaliou(perfil))
        {
            return (ResultadoDaDecisao.PerfilAlterado, new DomainError(
                PedidoErrorCodes.PerfilAlterado,
                "O perfil mudou depois da aprovação do gestor; o pedido voltou a aguardar o gestor."));
        }

        return Andamento.ValidaAte is { } fim && fim <= instante
            ? (ResultadoDaDecisao.PrazoVencido, new DomainError(
                PedidoErrorCodes.PrazoVencido,
                "O prazo definido pelo gestor venceu antes desta aprovação; o pedido voltou ao gestor para definir outro."))
            : null;
    }

    private DomainError DevolverAoGestor(
        Identidade decisor, EtapaDoPedido etapa, PerfilAvaliado perfil, DateTimeOffset instante, ResultadoDaDecisao resultado, DomainError erro)
    {
        _decisoes.Add(DecisaoDoPedido.Registrar(Id, etapa, new AtoDoAgente(decisor, instante), resultado, null, perfil, null));
        Andamento = Andamento.DeVoltaAoGestor();

        return erro;
    }

    private Result<DesfechoDaAprovacao> RegistrarAprovacao(AprovacaoNaEtapa aprovacao)
    {
        bool naEtapaDoGestor = aprovacao.Etapa == EtapaDoPedido.Gestor;
        string? dispensa = naEtapaDoGestor ? aprovacao.Contexto.MotivoDaDispensa(aprovacao.Perfil) : null;
        _decisoes.Add(DecisaoDoPedido.Registrar(
            Id, aprovacao.Etapa, new AtoDoAgente(aprovacao.Decisor, aprovacao.Instante), ResultadoDaDecisao.Aprovado, null, aprovacao.Perfil, dispensa));

        if (naEtapaDoGestor)
        {
            Andamento = Andamento.ComPrazo(aprovacao.ValidaAte);
        }

        bool concluido = !naEtapaDoGestor || dispensa is not null || !aprovacao.Perfil.ExigeAprovacaoDeProtecaoDeDados;
        return concluido ? Concluir(aprovacao.Instante) : AguardarProtecaoDeDados();
    }

    private Result<DesfechoDaAprovacao> Concluir(DateTimeOffset instante)
    {
        Andamento = Andamento.Encerrado(SituacaoDoPedido.Aprovado, instante);
        return Result<DesfechoDaAprovacao>.Success(DesfechoDaAprovacao.Concluido);
    }

    private Result<DesfechoDaAprovacao> AguardarProtecaoDeDados()
    {
        Andamento = Andamento.Em(SituacaoDoPedido.AguardandoProtecaoDeDados);
        return Result<DesfechoDaAprovacao>.Success(DesfechoDaAprovacao.AguardaProtecaoDeDados);
    }
}
