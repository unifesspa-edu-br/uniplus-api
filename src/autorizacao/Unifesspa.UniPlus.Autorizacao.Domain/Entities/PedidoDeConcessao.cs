namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Pedido de concessão de um perfil de acesso a uma pessoa, num escopo. Toda concessão nasce de um
/// pedido aprovado: o gestor de acessos aprova a adequação funcional e, quando o perfil reúne
/// permissão pessoal ou sensível, a proteção de dados aprova depois, a adequação à proteção de dados.
/// </summary>
/// <remarks>
/// <para>
/// <b>Só a identidade do token é gravada.</b> Nunca CPF; o nome e o e-mail do beneficiário existem para
/// que o aprovador saiba quem é a pessoa (aprovar exige julgar a pessoa), e o nome vem do token dele, no
/// aceite. O e-mail do solicitante é o verificado do token e serve só para avisá-lo do desfecho. A sensibilidade
/// do perfil também não se grava: deriva do catálogo.
/// </para>
/// <para>
/// <b>Nenhuma decisão acontece sem a identidade completa do beneficiário.</b> O pedido para outra
/// pessoa espera o aceite dela, que grava emissor, <c>contaId</c> e nome do token.
/// </para>
/// <para>
/// <b>As decisões só se acrescentam</b> e guardam o retrato do que o aprovador avaliou. Os estados
/// terminais não voltam.
/// </para>
/// </remarks>
public sealed class PedidoDeConcessao : EntityBase
{
    private readonly DecisoesDoPedido _decisoes = [];
    private readonly AnexosDoPedido _anexos = [];

    /// <summary>Quem pediu o acesso. Só a identidade do token é gravada: nunca o CPF.</summary>
    public SolicitanteDoPedido Solicitante { get; private set; } = null!;

    /// <summary>E-mail institucional do beneficiário, em minúsculas; é como o pedido o encontra até ele aceitar.</summary>
    public string BeneficiarioEmail { get; private set; } = string.Empty;

    /// <summary>A identidade do beneficiário, desconhecida até o aceite.</summary>
    public BeneficiarioDoPedido Beneficiario { get; private set; } = null!;

    public Guid PerfilId { get; private set; }

    /// <summary>Unidade do escopo; nula só no perfil de governança, de escopo institucional.</summary>
    public Guid? UnidadeId { get; private set; }

    /// <summary>O que o solicitante declarou ao pedir.</summary>
    public DeclaracaoDoPedido Declaracao { get; private set; } = null!;

    /// <summary>Onde o pedido está: a situação, o prazo que o gestor definiu e o encerramento.</summary>
    public AndamentoDoPedido Andamento { get; private set; } = null!;

    public DateTimeOffset CriadoEm { get; private set; }

    /// <summary>A concessão que a última aprovação gerou; nula até lá.</summary>
    public Guid? ConcessaoId { get; private set; }

    public IReadOnlyList<DecisaoDoPedido> Decisoes => _decisoes;

    private SegregacaoDoPedido Segregacao => new(Solicitante, Beneficiario, _decisoes);

    public IReadOnlyList<AnexoDoPedido> Anexos => _anexos;

    public EscopoDaConcessao Escopo => EscopoDaConcessao.Da(UnidadeId);

    // Construtor de materialização do EF Core.
    private PedidoDeConcessao()
    {
    }

    /// <summary>Cancela o pedido, só antes da primeira decisão.</summary>
    public Result Cancelar(DateTimeOffset instante)
    {
        if (!PodeSerAlteradoPeloSolicitante)
        {
            return Result.Failure(SituacaoInvalida("O pedido só se cancela antes da primeira decisão."));
        }

        Andamento = Andamento.Encerrado(SituacaoDoPedido.Cancelado, instante);
        return Result.Success();
    }

    /// <summary>Registra a concessão que a última aprovação gerou.</summary>
    public void RegistrarConcessao(Guid concessaoId)
    {
        if (Andamento.Situacao != SituacaoDoPedido.Aprovado)
        {
            throw new InvalidOperationException("Só o pedido aprovado tem concessão.");
        }

        ConcessaoId = concessaoId;
    }

    private bool PodeSerAlteradoPeloSolicitante =>
        Andamento.Situacao is SituacaoDoPedido.AguardandoDocumento or SituacaoDoPedido.AguardandoBeneficiario or SituacaoDoPedido.AguardandoGestor
        && _decisoes.Count == 0;

    private static DomainError SituacaoInvalida(string mensagem) => new(PedidoErrorCodes.SituacaoInvalida, mensagem);

    /// <summary>
    /// Cria o pedido. Acumula toda violação independente num só conjunto de erros (ADR-0125). Que o pedido ativo igual não
    /// exista é do índice único, traduzido por quem grava; que a unidade exista é do handler; o termo de responsabilidade se
    /// fixa em seguida, por <see cref="FixarTermo"/>.
    /// </summary>
    /// <param name="partes">Quem pede e a quem o pedido se destina.</param>
    /// <param name="declaracao">Justificativa, validade sugerida e intenção de anexar documento; os perfis de gestor de acessos e de Representante do controlador não admitem prazo.</param>
    public static Result<PedidoDeConcessao> Criar(
        PartesDoPedido partes,
        Perfil perfil,
        EscopoDaConcessao escopo,
        DeclaracaoDoPedido declaracao,
        IDominiosInstitucionais dominios,
        ICatalogoDePermissoesConsultavel catalogo,
        DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(partes);
        ArgumentNullException.ThrowIfNull(perfil);
        ArgumentNullException.ThrowIfNull(escopo);
        ArgumentNullException.ThrowIfNull(declaracao);
        ArgumentNullException.ThrowIfNull(dominios);
        ArgumentNullException.ThrowIfNull(catalogo);

        DomainError? recusa = partes.RecusaDoSolicitante(dominios);
        (List<FieldError> erros, string? justificativa) = ValidacaoDaCriacaoDoPedido.ValidarCampos(perfil, escopo, catalogo, declaracao, instante);
        BeneficiarioIndicado beneficiario = partes.ResolverBeneficiario(dominios, erros);

        return recusa is not null ? Result<PedidoDeConcessao>.Failure(recusa)
            : erros.Count > 0 ? Result<PedidoDeConcessao>.ValidationFailure(erros)
            : Result<PedidoDeConcessao>.Success(Novo(partes.Solicitante, perfil, escopo, declaracao.Consolidada(justificativa!, perfil), instante, beneficiario));
    }

    private static PedidoDeConcessao Novo(
        PessoaNoToken solicitante,
        Perfil perfil,
        EscopoDaConcessao escopo,
        DeclaracaoDoPedido declaracao,
        DateTimeOffset instante,
        BeneficiarioIndicado beneficiario)
    {
        PedidoDeConcessao pedido = new()
        {
            PerfilId = perfil.Id,
            VersaoId = perfil.VersaoVigente?.Id,
            UnidadeId = escopo.UnidadeId,
            Declaracao = declaracao,
            Andamento = AndamentoDoPedido.Inicial(SituacaoInicial(declaracao.AnexaraDocumento, beneficiario.ParaSiMesmo)),
            CriadoEm = instante,
        };
        pedido.DefinirPartes(solicitante, beneficiario);

        return pedido;
    }

    private void DefinirPartes(PessoaNoToken solicitante, BeneficiarioIndicado beneficiario)
    {
        BeneficiarioEmail = beneficiario.Email!;
        Solicitante = SolicitanteDoPedido.De(solicitante);
        Beneficiario = beneficiario.Inicial(Solicitante);
    }

    // O documento declarado vem antes de tudo; sem ele, o pedido para si mesmo vai ao gestor e o para outra pessoa espera o
    // aceite dela.
    private static SituacaoDoPedido SituacaoInicial(bool anexaraDocumento, bool paraSiMesmo)
    {
        if (anexaraDocumento)
        {
            return SituacaoDoPedido.AguardandoDocumento;
        }

        return paraSiMesmo ? SituacaoDoPedido.AguardandoGestor : SituacaoDoPedido.AguardandoBeneficiario;
    }

    /// <summary>
    /// O beneficiário aceita o pedido que o nomeou e, no pedido protegido, o termo fixado nele. O beneficiário já conhecido
    /// (o próprio solicitante) é conferido pela identidade do token; o ainda desconhecido, pelo e-mail verificado do pedido. O
    /// aceite grava a identidade e o nome do token e, havendo termo, a versão e o resumo aceitos. O pedido que aguarda o
    /// documento declarado não aceita: o aceite espera a verificação.
    /// </summary>
    /// <param name="termoApresentado">A versão do termo que o cliente apresentou; obrigatória no pedido com termo e proibida nos demais.</param>
    public Result Aceitar(PessoaNoToken pessoa, Guid? termoApresentado, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        DomainError? recusaDoAceite = Andamento.RecusaDoAceite() ?? Beneficiario.RecusaDoAceite(pessoa, BeneficiarioEmail);
        Result? recusa = recusaDoAceite is { } erro ? Result.Failure(erro) : ApresentacaoDoTermo.Conferir(termoApresentado, Termo?.VersaoId);
        if (recusa is not null)
        {
            return recusa;
        }

        Beneficiario = BeneficiarioDoPedido.AceitoPor(pessoa, instante);
        RegistrarAceiteDoTermo(instante);
        Andamento = Andamento.Em(SituacaoDoPedido.AguardandoGestor);

        return Result.Success();
    }

    /// <summary>A pessoa é uma das partes do pedido: o solicitante ou o beneficiário.</summary>
    public bool EhParte(PessoaNoToken pessoa)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        return Solicitante.Eh(pessoa.Identidade)
            || Beneficiario.Reconhece(pessoa, BeneficiarioEmail);
    }

    /// <summary>A versão fixada na criação, com número e resumo; nula no pedido que não exige termo.</summary>
    public TermoFixado? Termo { get; private set; }

    /// <summary>O aceite do termo pelo beneficiário; nulo até ele aceitar ou no pedido sem termo.</summary>
    public AceiteDoTermo? AceiteDoTermo { get; private set; }

    /// <summary>O pedido espera, do beneficiário, o aceite do termo fixado.</summary>
    public bool AguardaAceiteDoTermo => Termo is not null && Andamento.Situacao == SituacaoDoPedido.AguardandoBeneficiario;

    /// <summary>
    /// Fixa no pedido recém-criado a versão vigente do termo, quando o perfil o exige, e confere a versão que o cliente
    /// apresentou, obrigatória em todo pedido protegido (UNI-REQ-0377): divergente da vigente, conflito; ausente no pedido
    /// protegido, ou presente quando o pedido não exige termo, recusa de campo. No pedido para si mesmo sem documento, o
    /// envio é o aceite, registrado com a versão fixada.
    /// </summary>
    public Result FixarTermo(TermoNaCriacao termo, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(termo);

        if (termo.Recusa() is { } recusa)
        {
            return recusa;
        }

        if (termo.Fixado is { } fixado)
        {
            Fixar(fixado, instante);
        }

        return Result.Success();
    }

    private bool EnvioEOAceite => Beneficiario.Identidade is not null && !Declaracao.AnexaraDocumento;

    private void Fixar(TermoFixado fixado, DateTimeOffset instante)
    {
        Termo = fixado;

        if (EnvioEOAceite)
        {
            AceiteDoTermo = AceiteDoTermo.Registrar(Id, fixado, Beneficiario.Identidade!, instante);
        }
    }

    private void RegistrarAceiteDoTermo(DateTimeOffset instante)
    {
        if (Termo is { } fixado)
        {
            AceiteDoTermo = AceiteDoTermo.Registrar(Id, fixado, Beneficiario.Identidade!, instante);
        }
    }

    // Depois do documento verificado, o pedido aguarda o aceite quando o beneficiário é outra pessoa ou o termo o exige; só
    // então vai ao gestor.
    private SituacaoDoPedido EtapaSeguinteAoDocumento() =>
        Beneficiario.Identidade is null || Termo is not null ? SituacaoDoPedido.AguardandoBeneficiario : SituacaoDoPedido.AguardandoGestor;

    /// <summary>
    /// Cria o anexo e devolve-o para o handler emitir a URL de envio. Só antes da primeira decisão e, no pedido protegido,
    /// só o documento declarado na criação, enquanto o pedido o aguarda.
    /// </summary>
    public Result<AnexoDoPedido> NovoAnexo(long tamanhoDoPdf)
    {
        DomainError? recusa = PodeSerAlteradoPeloSolicitante
            ? Declaracao.RecusaDoDocumento(Termo, Andamento.Situacao)
            : SituacaoInvalida("O anexo só se envia antes da primeira decisão.");
        return recusa is null ? _anexos.Novo(Id, tamanhoDoPdf) : Result<AnexoDoPedido>.Failure(recusa);
    }

    /// <summary>Põe o anexo em verificação, com o ETag que o PUT devolveu. Só antes da primeira decisão.</summary>
    public Result<AnexoDoPedido> ConfirmarAnexo(Guid anexoId, string? etag, DateTimeOffset instante)
    {
        if (!PodeSerAlteradoPeloSolicitante)
        {
            return Result<AnexoDoPedido>.Failure(SituacaoInvalida("O anexo só se confirma antes da primeira decisão."));
        }

        return _anexos.Confirmar(anexoId, etag, instante);
    }

    /// <summary>
    /// Libera o pedido que espera o documento declarado quando já há anexo verificado: o pedido segue para o aceite do
    /// beneficiário, se ele é outra pessoa ou se o pedido tem termo, ou para o gestor. Em qualquer outra situação, ou sem o anexo
    /// verificado, nada muda e devolve falso.
    /// </summary>
    public bool LiberarAoVerificarDocumento()
    {
        if (Andamento.Situacao != SituacaoDoPedido.AguardandoDocumento || !_anexos.TemVerificado)
        {
            return false;
        }

        Andamento = Andamento.Em(EtapaSeguinteAoDocumento());
        return true;
    }

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

        if (RecusaDoTermoSemAceite() is { } recusa)
        {
            return Result<DesfechoDaAprovacao>.Failure(recusa);
        }

        return aprovacao.Devolucao(_decisoes, Andamento, Termo) is { } devolucao
            ? Result<DesfechoDaAprovacao>.Failure(Devolver(aprovacao, devolucao))
            : RegistrarAprovacao(aprovacao);
    }

    // Defesa contra estado corrompido: a máquina de estados não leva ao gestor o pedido com termo e sem aceite.
    private DomainError? RecusaDoTermoSemAceite() =>
        Termo is not null && AceiteDoTermo is null
            ? SituacaoInvalida("O pedido com termo fixado só se decide depois do aceite do beneficiário.")
            : null;

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

        Andamento = Andamento.DepoisDaAprovacao(aprovacao);

        return Result<DesfechoDaAprovacao>.Success(aprovacao.Desfecho);
    }

    private readonly List<TrocaDeVersaoDoPedido> _trocasDeVersao = [];

    /// <summary>A versão do perfil que o pedido segue; nula só no perfil de governança, que não tem versão.</summary>
    public Guid? VersaoId { get; private set; }

    public IReadOnlyList<TrocaDeVersaoDoPedido> TrocasDeVersao => _trocasDeVersao;

    /// <summary>
    /// Aplica ao pedido a entrada em vigor de uma versão nova do perfil. A versão com acréscimo devolve o pedido, que não
    /// pode seguir com permissão que ninguém aprovou. Sem acréscimo, a aprovação do gestor que já tinha sido dada perde
    /// o efeito e o pedido volta a ele; nas demais etapas o pedido só passa para a versão nova. Cada efeito grava uma troca.
    /// </summary>
    public DesfechoDaTrocaDeVersao AoEntrarEmVigor(TrocaDeVigencia troca, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(troca);

        if (!Andamento.Ativo)
        {
            return DesfechoDaTrocaDeVersao.Inalterado;
        }

        DesfechoDaTrocaDeVersao desfecho = Andamento.DesfechoDaTroca(troca.TemAcrescimo);
        Andamento = Andamento.DepoisDaTroca(desfecho, instante);
        VersaoId = desfecho == DesfechoDaTrocaDeVersao.Devolvido ? VersaoId : troca.Nova.Id;
        _trocasDeVersao.Add(TrocaDeVersaoDoPedido.Registrar(Id, troca.Anterior!.Numero, troca.Nova.Numero, desfecho, instante));

        return desfecho;
    }

    /// <summary>
    /// Confere que o pedido ativo segue a versão em vigor do perfil. A troca de vigência atualiza ou devolve todo pedido
    /// ativo sob a trava do perfil, então o pedido ativo que chega à decisão em outra versão é defeito, não situação de
    /// negócio. O pedido encerrado, como o devolvido, mantém a versão que seguia e não é conferido.
    /// </summary>
    public void ExigirVersaoEmVigorDe(Perfil perfil)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        if (Andamento.Ativo && VersaoId != perfil.VersaoVigente?.Id)
        {
            throw new InvalidOperationException(
                $"O pedido {Id} segue a versão {VersaoId} e o perfil {perfil.Id} tem outra em vigor.");
        }
    }
}
