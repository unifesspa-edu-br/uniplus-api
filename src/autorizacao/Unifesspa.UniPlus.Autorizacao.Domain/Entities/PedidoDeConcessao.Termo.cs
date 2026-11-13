namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// O termo de responsabilidade no pedido protegido (ADR-0162): a versão vigente fica fixada na criação e não muda mais, e
/// só o beneficiário a aceita. A ordem é documento verificado, depois aceite, depois gestor; no pedido para si mesmo sem
/// documento, o envio é o aceite.
/// </summary>
public sealed partial class PedidoDeConcessao
{
    private const string CampoDaVersaoApresentada = "termoVersaoApresentadaId";

    /// <summary>O solicitante declarou, na criação, que anexaria documento; sempre verdadeiro no perfil de governança.</summary>
    public bool AnexaraDocumento { get; private set; }

    /// <summary>A versão do termo fixada na criação; nula no pedido que não exige termo.</summary>
    public Guid? TermoVersaoId { get; private set; }

    public int? TermoNumero { get; private set; }

    public string? TermoResumo { get; private set; }

    /// <summary>O aceite do termo pelo beneficiário; nulo até ele aceitar ou no pedido sem termo.</summary>
    public AceiteDoTermo? AceiteDoTermo { get; private set; }

    /// <summary>A versão fixada, com número e resumo; nula no pedido que não exige termo.</summary>
    public TermoFixado? Termo => TermoVersaoId is { } id && TermoNumero is { } numero && TermoResumo is { } resumo
        ? new TermoFixado(id, numero, resumo)
        : null;

    /// <summary>O pedido espera, do beneficiário, o aceite do termo fixado.</summary>
    public bool AguardaAceiteDoTermo => TermoVersaoId is not null && Situacao == SituacaoDoPedido.AguardandoBeneficiario;

    /// <summary>
    /// O perfil exige o termo: a composição em vigor tem permissão pessoal, identificadora ou sensível, ou o perfil é de
    /// governança, que exige o termo mesmo que uma recomposição futura o deixe sem permissão pessoal (UNI-REQ-0377).
    /// </summary>
    public static bool ExigeTermo(Perfil perfil, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        return perfil.Governanca is not null || perfil.Protegido(catalogo);
    }

    /// <summary>
    /// Fixa no pedido recém-criado a versão vigente do termo, quando o perfil o exige, e confere a versão que o cliente
    /// apresentou, obrigatória em todo pedido protegido (UNI-REQ-0377): divergente da vigente, conflito; ausente no pedido
    /// protegido, ou presente quando o pedido não exige termo, recusa de campo. No pedido para si mesmo sem documento, o
    /// envio é o aceite, registrado com a versão fixada.
    /// </summary>
    public Result FixarTermo(TermoNaCriacao termo, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(termo);

        if (RecusaDoTermoNaCriacao(termo) is { } recusa)
        {
            return recusa;
        }

        if (termo.Exigido)
        {
            Fixar(termo.Vigente!, instante);
        }

        return Result.Success();
    }

    private bool EnvioEOAceite => Beneficiario is not null && !AnexaraDocumento;

    private static Result? RecusaDoTermoNaCriacao(TermoNaCriacao termo)
    {
        if (!termo.Exigido)
        {
            return termo.Apresentada is null ? null : ApresentacaoIndevida();
        }

        if (termo.Vigente is null)
        {
            return Result.Failure(SemTermoVigente());
        }

        return termo.Apresentada is { } apresentada ? Comparar(apresentada, termo.Vigente.Id) : ApresentacaoAusente();
    }

    private void Fixar(VersaoDoTermo vigente, DateTimeOffset instante)
    {
        TermoVersaoId = vigente.Id;
        TermoNumero = vigente.Numero;
        TermoResumo = vigente.Resumo;

        if (EnvioEOAceite)
        {
            AceiteDoTermo = AceiteDoTermo.Registrar(Id, Termo!, Beneficiario!, instante);
        }
    }

    // O aceite do beneficiário confere a versão apresentada com a fixada, que não muda depois da criação.
    private Result? RecusaDoTermoNoAceite(Guid? apresentada)
    {
        if (Termo is not { } fixado)
        {
            return apresentada is null ? null : ApresentacaoIndevida();
        }

        return apresentada is { } versao ? Comparar(versao, fixado.VersaoId) : ApresentacaoAusente();
    }

    private void RegistrarAceiteDoTermo(DateTimeOffset instante)
    {
        if (Termo is { } fixado)
        {
            AceiteDoTermo = AceiteDoTermo.Registrar(Id, fixado, Beneficiario!, instante);
        }
    }

    // Defesa contra estado corrompido: a máquina de estados não leva ao gestor o pedido com termo e sem aceite. E o perfil
    // que passou a exigir o termo depois da criação devolve o pedido, que não tem versão fixada: nenhuma concessão protegida
    // nasce sem aceite do termo (ADR-0162).
    private DomainError? RecusaPeloTermo(AprovacaoNaEtapa aprovacao)
    {
        if (Termo is not null && AceiteDoTermo is null)
        {
            return SituacaoInvalida("O pedido com termo fixado só se decide depois do aceite do beneficiário.");
        }

        return aprovacao.Etapa == EtapaDoPedido.Gestor && Termo is null && ExigeTermo(aprovacao.Perfil)
            ? DevolverPorTermoExigido(aprovacao)
            : null;
    }

    private DomainError DevolverPorTermoExigido(AprovacaoNaEtapa aprovacao)
    {
        _decisoes.Add(DecisaoDoPedido.Registrar(
            Id, aprovacao.Etapa, new AtoDoAgente(aprovacao.Decisor, aprovacao.Instante), ResultadoDaDecisao.TermoExigido, null, aprovacao.Perfil, null));
        Encerrar(SituacaoDoPedido.Devolvido, aprovacao.Instante);

        return new DomainError(
            PedidoErrorCodes.TermoExigido,
            "O perfil passou a dar acesso a dado pessoal ou sensível depois da criação do pedido, que não tem termo de responsabilidade fixado. O pedido foi devolvido; refaça-o para aceitar o termo.");
    }

    // No pedido protegido, o documento entra só se declarado na criação e só enquanto o pedido o aguarda; o pedido comum
    // continua aceitando anexo até a primeira decisão.
    private DomainError? RecusaDoNovoAnexo()
    {
        if (Termo is null)
        {
            return null;
        }

        if (!AnexaraDocumento)
        {
            return new DomainError(
                PedidoErrorCodes.AnexoNaoDeclarado,
                "O documento só se anexa ao pedido que declarou, na criação, que o anexaria. Cancele e refaça o pedido declarando o documento.");
        }

        return Situacao == SituacaoDoPedido.AguardandoDocumento ? null : SituacaoInvalida("O documento declarado já foi verificado.");
    }

    // Depois do documento verificado, o pedido aguarda o aceite quando o beneficiário é outra pessoa ou o termo o exige; só
    // então vai ao gestor.
    private SituacaoDoPedido EtapaSeguinteAoDocumento() =>
        Beneficiario is null || Termo is not null ? SituacaoDoPedido.AguardandoBeneficiario : SituacaoDoPedido.AguardandoGestor;

    private static DomainError SemTermoVigente() => new(
        PedidoErrorCodes.SemTermoVigente,
        "O perfil pedido dá acesso a dado pessoal ou sensível e exige o aceite do termo de responsabilidade, mas nenhuma versão foi publicada. Peça ao gestor de acessos que publique o termo.");

    private static bool ExigeTermo(PerfilAvaliado perfil) => perfil.ExigeAprovacaoDeProtecaoDeDados || perfil.Governanca is not null;

    private static Result? Comparar(Guid apresentada, Guid esperada) =>
        apresentada == esperada
            ? null
            : Result.Failure(new DomainError(
                PedidoErrorCodes.TermoDivergente,
                "A versão do termo apresentada não é a vigente (na criação) ou a fixada no pedido (no aceite). Leia o termo novamente e repita a operação."));

    private static Result ApresentacaoAusente() => Result.ValidationFailure([new FieldError(CampoDaVersaoApresentada, new DomainError(
        PedidoErrorCodes.ApresentacaoDoTermoInvalida,
        "Este pedido exige informar a versão do termo de responsabilidade apresentada."))]);

    private static Result ApresentacaoIndevida() => Result.ValidationFailure([new FieldError(CampoDaVersaoApresentada, new DomainError(
        PedidoErrorCodes.ApresentacaoDoTermoInvalida,
        "Este pedido não exige o termo de responsabilidade, e a versão não deve ser informada."))]);
}
