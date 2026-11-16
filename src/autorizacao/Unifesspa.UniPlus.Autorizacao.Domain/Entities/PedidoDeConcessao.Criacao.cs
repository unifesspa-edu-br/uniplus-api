namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>A criação do pedido e as conferências que a antecedem.</summary>
public sealed partial class PedidoDeConcessao
{
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
        (string? email, bool paraSiMesmo) = partes.ResolverBeneficiario(dominios, erros);

        return recusa is not null ? Result<PedidoDeConcessao>.Failure(recusa)
            : erros.Count > 0 ? Result<PedidoDeConcessao>.ValidationFailure(erros)
            : Result<PedidoDeConcessao>.Success(Novo(partes.Solicitante, perfil, escopo, declaracao.Consolidada(justificativa!, perfil), instante, (email!, paraSiMesmo)));
    }

    private static PedidoDeConcessao Novo(
        PessoaNoToken solicitante,
        Perfil perfil,
        EscopoDaConcessao escopo,
        DeclaracaoDoPedido declaracao,
        DateTimeOffset instante,
        (string Email, bool ParaSiMesmo) beneficiario)
    {
        PedidoDeConcessao pedido = new()
        {
            PerfilId = perfil.Id,
            VersaoId = perfil.VersaoVigente?.Id,
            UnidadeId = escopo.UnidadeId,
            Declaracao = declaracao,
            Situacao = SituacaoInicial(declaracao.AnexaraDocumento, beneficiario.ParaSiMesmo),
            CriadoEm = instante,
        };
        pedido.DefinirPartes(solicitante, beneficiario);

        return pedido;
    }

    private void DefinirPartes(PessoaNoToken solicitante, (string Email, bool ParaSiMesmo) beneficiario)
    {
        BeneficiarioEmail = beneficiario.Email;
        Solicitante = SolicitanteDoPedido.De(solicitante);

        Beneficiario = beneficiario.ParaSiMesmo ? BeneficiarioDoPedido.Conhecido(Solicitante.Identidade, Solicitante.Nome) : BeneficiarioDoPedido.Desconhecido();
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
}
