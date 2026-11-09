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
    /// pessoal ou sensível o pedido segue para a proteção de dados, senão a aprovação conclui. A segunda aprovação é
    /// dispensada para o perfil da própria proteção de dados, que ninguém poderia aprovar, e para o beneficiário que já
    /// tem o perfil de proteção de dados (ADR-0152); a decisão do gestor registra a dispensa e o motivo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O fim da vigência é definido pelo gestor, na etapa dele; a proteção de dados não o informa e a concessão herda
    /// o que o gestor gravou.
    /// </para>
    /// <para>
    /// O perfil que mudou entre as etapas, ou o prazo que venceu antes da proteção de dados, devolve o pedido ao gestor e
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

        if (EtapaAtual is not { } etapa)
        {
            return Result<DesfechoDaAprovacao>.Failure(SituacaoInvalida("O pedido não aguarda aprovação."));
        }

        List<FieldError> erros = ErrosDaAprovacao(decisor, etapa, perfil, instante, validaAte);
        if (erros.Count > 0)
        {
            return Result<DesfechoDaAprovacao>.ValidationFailure(erros);
        }

        return DevolverAoGestorSeMudou(decisor, etapa, perfil, instante) is { } devolucao
            ? Result<DesfechoDaAprovacao>.Failure(devolucao)
            : RegistrarAprovacao(decisor, etapa, perfil, contexto, instante, validaAte);
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
        if (erros.Count > 0)
        {
            return Result.ValidationFailure(erros);
        }

        RegistrarRecusa(decisor, etapa, motivoNormalizado, perfil, instante);
        return Result.Success();
    }

    private DecisaoDoPedido? UltimaAprovacaoDoGestor => _decisoes
        .Where(d => d.Etapa == EtapaDoPedido.Gestor && d.Resultado == ResultadoDaDecisao.Aprovado)
        .MaxBy(d => d.Instante);

    private List<FieldError> ErrosDaAprovacao(
        Identidade decisor, EtapaDoPedido etapa, PerfilAvaliado perfil, DateTimeOffset instante, DateTimeOffset? validaAte)
    {
        List<FieldError> erros = [];
        ConferirPessoasDistintas(decisor, etapa, erros);

        if (!perfil.Ativo)
        {
            erros.Add(new("perfilId", new DomainError(
                PedidoErrorCodes.PerfilInativo,
                "O perfil de acesso está desativado e não aceita aprovação.")));
        }

        ConferirValidadeDaEtapa(etapa, perfil, instante, validaAte, erros);

        return erros;
    }

    private static void ConferirValidadeDaEtapa(
        EtapaDoPedido etapa, PerfilAvaliado perfil, DateTimeOffset instante, DateTimeOffset? validaAte, List<FieldError> erros)
    {
        if (etapa == EtapaDoPedido.Gestor)
        {
            ConferirValidade(validaAte, perfil.AdmitePrazo, instante, "validaAte", erros);
        }
        else if (validaAte is not null)
        {
            erros.Add(new("validaAte", new DomainError(
                PedidoErrorCodes.ValidadeInvalida,
                "O fim da vigência é definido pelo gestor de acessos, na etapa dele.")));
        }
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

    private void RegistrarRecusa(
        Identidade decisor, EtapaDoPedido etapa, string? motivo, PerfilAvaliado perfil, DateTimeOffset instante)
    {
        _decisoes.Add(DecisaoDoPedido.Registrar(
            Id, etapa, decisor, ResultadoDaDecisao.Recusado, motivo, perfil, null, instante));
        Encerrar(SituacaoDoPedido.Recusado, instante);
    }

    // Nenhuma etapa concede o que a anterior não avaliou: o gestor decide de novo sobre o perfil de agora, e o prazo que
    // venceu volta ao gestor porque só ele o define. A tentativa fica registrada com quem a fez e o perfil que ele viu.
    private DomainError? DevolverAoGestorSeMudou(Identidade decisor, EtapaDoPedido etapa, PerfilAvaliado perfil, DateTimeOffset instante)
    {
        if (etapa != EtapaDoPedido.ProtecaoDeDados)
        {
            return null;
        }

        if (!MesmoPerfilQueOGestorAvaliou(perfil))
        {
            return DevolverAoGestor(decisor, etapa, perfil, instante, ResultadoDaDecisao.PerfilAlterado, new DomainError(
                PedidoErrorCodes.PerfilAlterado,
                "O perfil mudou depois da aprovação do gestor; o pedido voltou a aguardar o gestor."));
        }

        return ValidaAte is { } fim && fim <= instante
            ? DevolverAoGestor(decisor, etapa, perfil, instante, ResultadoDaDecisao.PrazoVencido, new DomainError(
                PedidoErrorCodes.PrazoVencido,
                "O prazo definido pelo gestor venceu antes desta aprovação; o pedido voltou ao gestor para definir outro."))
            : null;
    }

    private DomainError DevolverAoGestor(
        Identidade decisor, EtapaDoPedido etapa, PerfilAvaliado perfil, DateTimeOffset instante, ResultadoDaDecisao resultado, DomainError erro)
    {
        _decisoes.Add(DecisaoDoPedido.Registrar(Id, etapa, decisor, resultado, null, perfil, null, instante));
        Situacao = SituacaoDoPedido.AguardandoGestor;
        ValidaAte = null;

        return erro;
    }

    private Result<DesfechoDaAprovacao> RegistrarAprovacao(
        Identidade decisor,
        EtapaDoPedido etapa,
        PerfilAvaliado perfil,
        ContextoDaAprovacao contexto,
        DateTimeOffset instante,
        DateTimeOffset? validaAte)
    {
        string? dispensa = etapa == EtapaDoPedido.Gestor ? MotivoDaDispensa(perfil, contexto) : null;
        bool concluido = etapa == EtapaDoPedido.ProtecaoDeDados || dispensa is not null || !perfil.ExigeAprovacaoDeProtecaoDeDados;

        _decisoes.Add(DecisaoDoPedido.Registrar(
            Id, etapa, decisor, ResultadoDaDecisao.Aprovado, null, perfil, dispensa, instante));

        if (etapa == EtapaDoPedido.Gestor)
        {
            ValidaAte = validaAte?.ToUniversalTime();
        }

        return concluido ? Concluir(instante) : AguardarProtecaoDeDados();
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

        if (etapa == EtapaDoPedido.ProtecaoDeDados && _decisoes.Exists(d =>
                d.Etapa == EtapaDoPedido.Gestor && d.Resultado == ResultadoDaDecisao.Aprovado && d.Decisor == decisor))
        {
            erros.Add(new("decisor", new DomainError(
                PedidoErrorCodes.MesmoDecisorNasDuasEtapas,
                "Quem decidiu como gestor não decide na etapa de proteção de dados.")));
        }
    }

    private bool MesmoPerfilQueOGestorAvaliou(PerfilAvaliado perfil) =>
        UltimaAprovacaoDoGestor is { } gestor
        && gestor.PermissoesAvaliadas.SequenceEqual(perfil.Permissoes)
        && gestor.SensibilidadeAvaliada == perfil.Sensibilidade;

    private static string? MotivoDaDispensa(PerfilAvaliado perfil, ContextoDaAprovacao contexto) => perfil switch
    {
        { Governanca: TipoDeGovernanca.ProtecaoDeDados } => MotivosDeDispensa.DesignacaoDeProtecaoDeDados,
        { ExigeAprovacaoDeProtecaoDeDados: true } when contexto.BeneficiarioJaTemProtecaoDeDados => MotivosDeDispensa.BeneficiarioJaTemProtecaoDeDados,
        _ => null,
    };
}
