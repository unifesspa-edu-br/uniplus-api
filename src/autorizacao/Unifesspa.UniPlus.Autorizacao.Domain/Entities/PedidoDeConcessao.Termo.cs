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

    // Defesa contra estado corrompido: a máquina de estados não leva ao gestor o pedido com termo e sem aceite. E o perfil
    // que passou a exigir o termo depois da criação devolve o pedido, que não tem versão fixada: nenhuma concessão protegida
    // nasce sem aceite do termo (ADR-0162).
    private DomainError? RecusaPeloTermo(AprovacaoNaEtapa aprovacao)
    {
        if (Termo is not null && AceiteDoTermo is null)
        {
            return SituacaoInvalida("O pedido com termo fixado só se decide depois do aceite do beneficiário.");
        }

        return aprovacao.Etapa == EtapaDoPedido.Gestor && Termo is null && ExigenciaDoTermo.Do(aprovacao.Perfil)
            ? DevolverPorTermoExigido(aprovacao)
            : null;
    }

    private DomainError DevolverPorTermoExigido(AprovacaoNaEtapa aprovacao)
    {
        _decisoes.Add(DecisaoDoPedido.Registrar(
            Id, aprovacao.Etapa, new AtoDoAgente(aprovacao.Decisor, aprovacao.Instante), ResultadoDaDecisao.TermoExigido, null, aprovacao.Perfil, null));
        Andamento = Andamento.Encerrado(SituacaoDoPedido.Devolvido, aprovacao.Instante);

        return new DomainError(
            PedidoErrorCodes.TermoExigido,
            "O perfil passou a dar acesso a dado pessoal ou sensível depois da criação do pedido, que não tem termo de responsabilidade fixado. O pedido foi devolvido; refaça-o para aceitar o termo.");
    }

    // Depois do documento verificado, o pedido aguarda o aceite quando o beneficiário é outra pessoa ou o termo o exige; só
    // então vai ao gestor.
    private SituacaoDoPedido EtapaSeguinteAoDocumento() =>
        Beneficiario.Identidade is null || Termo is not null ? SituacaoDoPedido.AguardandoBeneficiario : SituacaoDoPedido.AguardandoGestor;
}
