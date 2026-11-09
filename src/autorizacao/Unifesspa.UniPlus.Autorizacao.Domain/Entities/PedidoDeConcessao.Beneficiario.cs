namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Quem é o beneficiário do pedido e o e-mail institucional que o identifica.</summary>
public sealed partial class PedidoDeConcessao
{
    /// <summary>
    /// O beneficiário aceita o pedido que o nomeou: o e-mail verificado do token tem de ser o do pedido, e o
    /// aceite grava a identidade e o nome do token dele.
    /// </summary>
    public Result Aceitar(PessoaNoToken pessoa, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        DomainError? recusa = RecusaDoAceite(pessoa);
        if (recusa is not null)
        {
            return Result.Failure(recusa);
        }

        DefinirBeneficiario(pessoa.Identidade, NomeDeExibicao.Normalizar(pessoa.Nome));
        BeneficiarioAceitouEm = instante;
        Situacao = SituacaoDoPedido.AguardandoGestor;

        return Result.Success();
    }

    /// <summary>A pessoa é uma das partes do pedido: o solicitante ou o beneficiário.</summary>
    public bool EhParte(PessoaNoToken pessoa)
    {
        ArgumentNullException.ThrowIfNull(pessoa);

        return pessoa.Identidade == Solicitante
            || (Beneficiario is { } beneficiario && pessoa.Identidade == beneficiario)
            || (Beneficiario is null && EhOBeneficiarioPeloEmail(pessoa));
    }

    private DomainError? RecusaDoAceite(PessoaNoToken pessoa)
    {
        if (Situacao != SituacaoDoPedido.AguardandoBeneficiario)
        {
            return SituacaoInvalida("O pedido não aguarda o aceite do beneficiário.");
        }

        if (!EhOBeneficiarioPeloEmail(pessoa))
        {
            return new DomainError(
                PedidoErrorCodes.NaoEOBeneficiario,
                "Só a pessoa a quem o pedido se destina, com o e-mail institucional verificado, o aceita.");
        }

        // O aprovador julga a pessoa pelo nome: sem ele no token, o pedido não avança à aprovação.
        return NomeDeExibicao.Normalizar(pessoa.Nome) is null ? SemNome() : null;
    }

    private bool EhOBeneficiarioPeloEmail(PessoaNoToken pessoa) =>
        pessoa.EmailVerificado
        && EmailNormalizado(pessoa.Email) is { } email
        && string.Equals(email, BeneficiarioEmail, StringComparison.Ordinal);

    private void DefinirBeneficiario(Identidade identidade, string? nome)
    {
        BeneficiarioEmissor = identidade.Emissor;
        BeneficiarioSub = identidade.Sub;
        BeneficiarioNome = nome;
    }
    private static DomainError SemNome() => new(
        PedidoErrorCodes.BeneficiarioSemNome,
        "A sessão do beneficiário não traz o nome completo, que o aprovador precisa para julgar o pedido.");

    // O beneficiário é o próprio solicitante quando o e-mail vem em branco ou é o do token dele, verificado:
    // quem usa o próprio e-mail já provou que é ele, e não há ninguém a quem esperar.
    private static (string? Email, bool ParaSiMesmo) ResolverBeneficiario(
        PessoaNoToken solicitante,
        string? emailInformado,
        IDominiosInstitucionais dominios,
        List<FieldError> erros)
    {
        string? informado = EmailNormalizado(emailInformado);
        string? doToken = solicitante.EmailVerificado ? EmailNormalizado(solicitante.Email) : null;
        bool paraSiMesmo = ParaSiMesmo(solicitante, emailInformado);

        if (paraSiMesmo)
        {
            if (doToken is null || !DominioInstitucional(doToken, dominios))
            {
                erros.Add(new("beneficiarioEmail", new DomainError(
                    PedidoErrorCodes.BeneficiarioInvalido,
                    "Para pedir para si, o e-mail da sessão deve ser institucional e verificado; para outra pessoa, informe o e-mail institucional dela.")));
            }

            if (NomeDeExibicao.Normalizar(solicitante.Nome) is null)
            {
                erros.Add(new("beneficiario", SemNome()));
            }

            return (doToken, true);
        }

        if (informado is null || !DominioInstitucional(informado, dominios))
        {
            erros.Add(new("beneficiarioEmail", new DomainError(
                PedidoErrorCodes.BeneficiarioInvalido,
                "O beneficiário é identificado pelo e-mail institucional dele.")));
        }

        return (informado, false);
    }

    private static bool ParaSiMesmo(PessoaNoToken solicitante, string? emailInformado)
    {
        string? informado = EmailNormalizado(emailInformado);
        string? doToken = solicitante.EmailVerificado ? EmailNormalizado(solicitante.Email) : null;

        return string.IsNullOrWhiteSpace(emailInformado) || (informado is not null && informado == doToken);
    }

    /// <summary>O e-mail da sessão é verificado e de domínio institucional: quem pede por outra pessoa responde por ela.</summary>
    public static bool SolicitanteInstitucional(PessoaNoToken pessoa, IDominiosInstitucionais dominios)
    {
        ArgumentNullException.ThrowIfNull(pessoa);
        ArgumentNullException.ThrowIfNull(dominios);

        return pessoa.EmailVerificado
            && EmailNormalizado(pessoa.Email) is { } email
            && DominioInstitucional(email, dominios);
    }

    private static bool DominioInstitucional(string email, IDominiosInstitucionais dominios) =>
        dominios.Contem(email[(email.LastIndexOf('@') + 1)..]);

    /// <summary>O e-mail como o pedido o guarda e compara (minúsculas, formato válido); nulo se inválido.</summary>
    public static string? EmailNormalizado(string? email)
    {
        Result<Email> resultado = Email.Criar(email);

        return resultado.IsSuccess && resultado.Value!.Valor.Length <= EmailMaxLength && !resultado.Value.Valor.Contains('\0', StringComparison.Ordinal)
            ? resultado.Value.Valor
            : null;
    }
}
