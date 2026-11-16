namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Quem é o beneficiário do pedido e o e-mail institucional que o identifica.</summary>
public sealed partial class PedidoDeConcessao
{
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

        DomainError? recusaDoAceite = RecusaDaSituacaoNoAceite() ?? Beneficiario.RecusaDoAceite(pessoa, BeneficiarioEmail);
        Result? recusa = recusaDoAceite is { } erro ? Result.Failure(erro) : ApresentacaoDoTermo.Conferir(termoApresentado, Termo?.VersaoId);
        if (recusa is not null)
        {
            return recusa;
        }

        Beneficiario = BeneficiarioDoPedido.AceitoPor(pessoa, instante);
        RegistrarAceiteDoTermo(instante);
        Situacao = SituacaoDoPedido.AguardandoGestor;

        return Result.Success();
    }

    /// <summary>A pessoa é uma das partes do pedido: o solicitante ou o beneficiário.</summary>
    public bool EhParte(PessoaNoToken pessoa)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        return Solicitante.Eh(pessoa.Identidade)
            || Beneficiario.Reconhece(pessoa, BeneficiarioEmail);
    }

    private DomainError? RecusaDaSituacaoNoAceite() => Situacao switch
    {
        SituacaoDoPedido.AguardandoBeneficiario => null,
        SituacaoDoPedido.AguardandoDocumento => new DomainError(
            PedidoErrorCodes.AnexoNaoVerificado,
            "O documento declarado ainda não foi verificado; o aceite e a decisão esperam a verificação."),
        _ => SituacaoInvalida("O pedido não aguarda o aceite do beneficiário."),
    };
}
