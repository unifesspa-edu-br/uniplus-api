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

        return EtapaAtual is { } etapa
            ? AprovarNaEtapa(new AprovacaoNaEtapa(decisor, etapa, perfil, contexto, instante, validaAte))
            : Result<DesfechoDaAprovacao>.Failure(SituacaoInvalida("O pedido não aguarda aprovação."));
    }

    /// <summary>Recusa o pedido na etapa corrente, com motivo. Encerra o pedido, sem concessão.</summary>
    public Result Recusar(Identidade decisor, string? motivo, PerfilAvaliado perfil, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(decisor);
        ArgumentNullException.ThrowIfNull(perfil);

        if (EtapaAtual is not { } etapa)
        {
            return Result.Failure(SituacaoInvalida("O pedido não aguarda decisão."));
        }

        string? motivoNormalizado = NormalizarTexto(motivo, 1, MotivoMaxLength);
        List<FieldError> erros = ErrosDaRecusa(decisor, etapa, motivoNormalizado);
        return erros.Count > 0
            ? Result.ValidationFailure(erros)
            : RegistrarRecusa(decisor, etapa, motivoNormalizado, perfil, instante);
    }

    private DecisaoDoPedido? UltimaAprovacaoDoGestor => _decisoes
        .Where(d => d.Etapa == EtapaDoPedido.Gestor && d.Resultado == ResultadoDaDecisao.Aprovado)
        .MaxBy(d => d.Instante);

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
        ConferirPessoasDistintas(aprovacao.Decisor, aprovacao.Etapa, erros);

        if (!aprovacao.Perfil.Ativo)
        {
            erros.Add(new("perfilId", new DomainError(
                PedidoErrorCodes.PerfilInativo,
                "O perfil de acesso está desativado e não aceita aprovação.")));
        }

        ConferirValidadeDaEtapa(aprovacao, erros);
        return erros;
    }

    private void ConferirValidadeDaEtapa(AprovacaoNaEtapa aprovacao, List<FieldError> erros)
    {
        if (aprovacao.Etapa == EtapaDoPedido.Gestor)
        {
            ConferirValidade(aprovacao.ValidaAte, aprovacao.Perfil.AdmitePrazo, aprovacao.Instante, "validaAte", erros);
        }
        else if (RecusaDaValidadeNaSegundaEtapa(aprovacao) is { } recusa)
        {
            erros.Add(recusa);
        }
    }

    // Na segunda etapa o Representante não informa prazo. O pedido de perfil que não admite prazo e já traz o do gestor,
    // gravado antes de o perfil de Representante deixar de admiti-lo (UNI-REQ-0200), é recusado aqui, antes de virar
    // concessão: nada é gravado, e o pedido segue para ser recusado.
    private FieldError? RecusaDaValidadeNaSegundaEtapa(AprovacaoNaEtapa aprovacao)
    {
        if (aprovacao.ValidaAte is not null)
        {
            return new("validaAte", new DomainError(PedidoErrorCodes.ValidadeInvalida, "O fim da vigência é definido pelo gestor de acessos, na etapa dele."));
        }

        return ValidaAte is not null && !aprovacao.Perfil.AdmitePrazo
            ? new("validaAte", new DomainError(PedidoErrorCodes.ValidadeInvalida, MensagensDoPrazo.PerfilSemPrazo))
            : null;
    }

    private List<FieldError> ErrosDaRecusa(Identidade decisor, EtapaDoPedido etapa, string? motivoNormalizado)
    {
        List<FieldError> erros = [];
        ConferirPessoasDistintas(decisor, etapa, erros);

        if (motivoNormalizado is null)
        {
            erros.Add(new("motivo", new DomainError(
                PedidoErrorCodes.MotivoInvalido,
                $"O motivo da recusa é obrigatório e deve ter até {MotivoMaxLength} caracteres.")));
        }

        return erros;
    }

    private Result RegistrarRecusa(
        Identidade decisor, EtapaDoPedido etapa, string? motivo, PerfilAvaliado perfil, DateTimeOffset instante)
    {
        _decisoes.Add(DecisaoDoPedido.Registrar(
            Id, etapa, new AtoDoAgente(decisor, instante), ResultadoDaDecisao.Recusado, motivo, perfil, null));
        Encerrar(SituacaoDoPedido.Recusado, instante);
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
        if (!MesmoPerfilQueOGestorAvaliou(perfil))
        {
            return (ResultadoDaDecisao.PerfilAlterado, new DomainError(
                PedidoErrorCodes.PerfilAlterado,
                "O perfil mudou depois da aprovação do gestor; o pedido voltou a aguardar o gestor."));
        }

        return ValidaAte is { } fim && fim <= instante
            ? (ResultadoDaDecisao.PrazoVencido, new DomainError(
                PedidoErrorCodes.PrazoVencido,
                "O prazo definido pelo gestor venceu antes desta aprovação; o pedido voltou ao gestor para definir outro."))
            : null;
    }

    private DomainError DevolverAoGestor(
        Identidade decisor, EtapaDoPedido etapa, PerfilAvaliado perfil, DateTimeOffset instante, ResultadoDaDecisao resultado, DomainError erro)
    {
        _decisoes.Add(DecisaoDoPedido.Registrar(Id, etapa, new AtoDoAgente(decisor, instante), resultado, null, perfil, null));
        Situacao = SituacaoDoPedido.AguardandoGestor;
        ValidaAte = null;

        return erro;
    }

    private Result<DesfechoDaAprovacao> RegistrarAprovacao(AprovacaoNaEtapa aprovacao)
    {
        bool naEtapaDoGestor = aprovacao.Etapa == EtapaDoPedido.Gestor;
        string? dispensa = naEtapaDoGestor ? MotivoDaDispensa(aprovacao.Perfil, aprovacao.Contexto) : null;
        _decisoes.Add(DecisaoDoPedido.Registrar(
            Id, aprovacao.Etapa, new AtoDoAgente(aprovacao.Decisor, aprovacao.Instante), ResultadoDaDecisao.Aprovado, null, aprovacao.Perfil, dispensa));

        if (naEtapaDoGestor)
        {
            ValidaAte = aprovacao.ValidaAte?.ToUniversalTime();
        }

        bool concluido = !naEtapaDoGestor || dispensa is not null || !aprovacao.Perfil.ExigeAprovacaoDeProtecaoDeDados;
        return concluido ? Concluir(aprovacao.Instante) : AguardarProtecaoDeDados();
    }

    private Result<DesfechoDaAprovacao> Concluir(DateTimeOffset instante)
    {
        Encerrar(SituacaoDoPedido.Aprovado, instante);
        return Result<DesfechoDaAprovacao>.Success(DesfechoDaAprovacao.Concluido);
    }

    private Result<DesfechoDaAprovacao> AguardarProtecaoDeDados()
    {
        Situacao = SituacaoDoPedido.AguardandoProtecaoDeDados;
        return Result<DesfechoDaAprovacao>.Success(DesfechoDaAprovacao.AguardaProtecaoDeDados);
    }

    private void ConferirPessoasDistintas(Identidade decisor, EtapaDoPedido etapa, List<FieldError> erros)
    {
        if (decisor == Solicitante || (Beneficiario is { } beneficiario && decisor == beneficiario))
        {
            erros.Add(new("decisor", new DomainError(
                PedidoErrorCodes.DecisorEParte,
                "Ninguém decide pedido em que é solicitante ou beneficiário.")));
        }

        if (etapa == EtapaDoPedido.ProtecaoDeDados && AprovouComoGestor(decisor))
        {
            erros.Add(new("decisor", new DomainError(
                PedidoErrorCodes.MesmoDecisorNasDuasEtapas,
                "Quem decidiu como gestor não decide na etapa de proteção de dados.")));
        }
    }

    private bool AprovouComoGestor(Identidade decisor) => _decisoes.Exists(d =>
        d.Etapa == EtapaDoPedido.Gestor && d.Resultado == ResultadoDaDecisao.Aprovado && d.Decisor == decisor);

    private bool MesmoPerfilQueOGestorAvaliou(PerfilAvaliado perfil) =>
        UltimaAprovacaoDoGestor is { } gestor
        && gestor.PermissoesAvaliadas.SequenceEqual(perfil.Permissoes)
        && gestor.SensibilidadeAvaliada == perfil.Sensibilidade;

    // Só a designação do primeiro Representante dispensa a segunda etapa: havendo Representante vigente, outro decide.
    private static string? MotivoDaDispensa(PerfilAvaliado perfil, ContextoDaAprovacao contexto) =>
        perfil.Governanca == TipoDeGovernanca.RepresentanteDoControlador && !contexto.HaRepresentanteVigente
            ? MotivosDeDispensa.DesignacaoDeProtecaoDeDados
            : null;

    private sealed record AprovacaoNaEtapa(
        Identidade Decisor,
        EtapaDoPedido Etapa,
        PerfilAvaliado Perfil,
        ContextoDaAprovacao Contexto,
        DateTimeOffset Instante,
        DateTimeOffset? ValidaAte);
}
