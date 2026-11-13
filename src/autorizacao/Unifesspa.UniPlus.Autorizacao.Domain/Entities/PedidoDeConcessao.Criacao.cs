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

        DomainError? recusa = RecusaDoSolicitante(partes, dominios);
        (List<FieldError> erros, string? justificativa) = ValidarCampos(perfil, escopo, catalogo, declaracao, instante);
        (string? email, bool paraSiMesmo) = ResolverBeneficiario(partes, dominios, erros);

        return recusa is not null ? Result<PedidoDeConcessao>.Failure(recusa)
            : erros.Count > 0 ? Result<PedidoDeConcessao>.ValidationFailure(erros)
            : Result<PedidoDeConcessao>.Success(Novo(partes.Solicitante, perfil, escopo, Consolidada(declaracao, justificativa, perfil), instante, (email!, paraSiMesmo)));
    }

    private static DomainError? RecusaDoSolicitante(PartesDoPedido partes, IDominiosInstitucionais dominios) =>
        !ParaSiMesmo(partes.Solicitante, partes.EmailDoBeneficiario) && !SolicitanteInstitucional(partes.Solicitante, dominios)
            ? new DomainError(
                PedidoErrorCodes.SolicitanteNaoInstitucional,
                "Para pedir acesso a outra pessoa, o e-mail da sessão deve ser institucional e verificado.")
            : null;

    // A justificativa já normalizada; o perfil de governança anexa sempre o documento de designação.
    private static DeclaracaoDoPedido Consolidada(DeclaracaoDoPedido declaracao, string? justificativa, Perfil perfil) =>
        declaracao with { Justificativa = justificativa, AnexaraDocumento = declaracao.AnexaraDocumento || perfil.Governanca is not null };

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
            Justificativa = declaracao.Justificativa!,
            ValidaAteSugerida = declaracao.ValidaAteSugerida?.ToUniversalTime(),
            AnexaraDocumento = declaracao.AnexaraDocumento,
            Situacao = SituacaoInicial(declaracao.AnexaraDocumento, beneficiario.ParaSiMesmo),
            CriadoEm = instante,
        };
        pedido.DefinirPartes(solicitante, beneficiario);

        return pedido;
    }

    private void DefinirPartes(PessoaNoToken solicitante, (string Email, bool ParaSiMesmo) beneficiario)
    {
        BeneficiarioEmail = beneficiario.Email;
        SolicitanteEmissor = solicitante.Identidade.Emissor;
        SolicitanteSub = solicitante.Identidade.Sub;
        SolicitanteNome = NomeDeExibicao.Normalizar(solicitante.Nome);
        SolicitanteEmail = solicitante.EmailVerificado ? EmailNormalizado(solicitante.Email) : null;

        if (beneficiario.ParaSiMesmo)
        {
            DefinirBeneficiario(solicitante.Identidade, SolicitanteNome);
        }
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

    private static IEnumerable<FieldError> ErrosDoPerfil(Perfil perfil)
    {
        if (!perfil.Ativo)
        {
            yield return new("perfilId", new DomainError(
                PedidoErrorCodes.PerfilInativo,
                "O perfil de acesso está desativado e não aceita pedido novo."));
        }

        if (!perfil.TemComposicaoEmVigor)
        {
            yield return new("perfilId", new DomainError(
                PedidoErrorCodes.PerfilSemVersaoEmVigor,
                "O perfil de acesso não tem versão em vigor e não aceita pedido."));
        }
    }

    private static IEnumerable<FieldError> ErrosDoEscopo(Perfil perfil, EscopoDaConcessao escopo, ICatalogoDePermissoesConsultavel catalogo)
    {
        string? incoerencia = Concessao.ConferirEscopo(perfil, escopo, catalogo);
        if (incoerencia is not null)
        {
            yield return new("escopo", new DomainError(PedidoErrorCodes.EscopoInvalido, incoerencia));
        }
    }

    private static (List<FieldError> Erros, string? Justificativa) ValidarCampos(
        Perfil perfil, EscopoDaConcessao escopo, ICatalogoDePermissoesConsultavel catalogo, DeclaracaoDoPedido declaracao, DateTimeOffset instante)
    {
        List<FieldError> erros = [.. ErrosDoPerfil(perfil), .. ErrosDoEscopo(perfil, escopo, catalogo)];
        string? normalizada = NormalizarTexto(declaracao.Justificativa, JustificativaMinLength, JustificativaMaxLength);

        if (normalizada is null)
        {
            erros.Add(new("justificativa", new DomainError(
                PedidoErrorCodes.JustificativaInvalida,
                $"A justificativa é obrigatória e deve ter entre {JustificativaMinLength} e {JustificativaMaxLength} caracteres.")));
        }

        ConferirValidade(declaracao.ValidaAteSugerida, perfil.AdmitePrazo, instante, "validaAteSugerida", erros);

        return (erros, normalizada);
    }

    private static void ConferirValidade(
        DateTimeOffset? data, bool admitePrazo, DateTimeOffset instante, string campo, List<FieldError> erros)
    {
        string? mensagem = data is { } fim ? MensagemDaValidadeInvalida(fim, admitePrazo, instante) : null;
        if (mensagem is not null)
        {
            erros.Add(new(campo, new DomainError(PedidoErrorCodes.ValidadeInvalida, mensagem)));
        }
    }

    private static string? MensagemDaValidadeInvalida(DateTimeOffset fim, bool admitePrazo, DateTimeOffset instante)
    {
        if (!admitePrazo)
        {
            return MensagensDoPrazo.PerfilSemPrazo;
        }

        return fim <= instante ? "O fim da vigência deve ser posterior ao instante atual." : null;
    }
}
