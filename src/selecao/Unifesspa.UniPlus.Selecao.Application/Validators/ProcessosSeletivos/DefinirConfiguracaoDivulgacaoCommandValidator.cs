namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ValueObjects;

using FluentValidation;

/// <summary>
/// Valida a forma do <see cref="DefinirConfiguracaoDivulgacaoCommand"/> antes do handler —
/// defesa em profundidade dos guards de <see cref="ConfiguracaoDivulgacao.Criar"/> (fonte de
/// verdade); aqui é só para recusar cedo, com 422 do middleware.
/// </summary>
/// <remarks>
/// <b>Nenhuma regra aqui aplica <c>NotNull</c> a <c>CamposPublicos</c>.</b> Lista nula é o
/// pedido legítimo de restaurar o default minimizado — um <c>NotNull</c> recusaria essa
/// requisição com 422 antes de o handler sequer rodar. Por isso toda regra condicional de
/// vocabulário/piso/exclusão/justificativa fica sob <c>When(x => x.CamposPublicos is not
/// null)</c>; a única recusa incondicional é a lista <b>vazia</b>.
/// </remarks>
public sealed class DefinirConfiguracaoDivulgacaoCommandValidator : AbstractValidator<DefinirConfiguracaoDivulgacaoCommand>
{
    // Por código de caractere, não como literal '\0' no fonte — evita que algum editor ou
    // ferramenta de texto grave o byte cru no arquivo (o mesmo raciocínio de
    // ConfiguracaoDivulgacao.CaractereNulo).
    private const char CaractereNulo = (char)0;

    public DefinirConfiguracaoDivulgacaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("ProcessoSeletivoId é obrigatório.");

        RuleFor(x => x.CamposPublicos)
            .Must(static campos => campos!.Count > 0)
            .When(x => x.CamposPublicos is not null)
            .WithMessage("A lista de campos públicos não pode ser vazia — para restaurar o default, envie null.");

        RuleForEach(x => x.CamposPublicos)
            .Must(static campo => campo is ConfiguracaoDivulgacao.NumeroInscricao or ConfiguracaoDivulgacao.NomeAbreviado or ConfiguracaoDivulgacao.Nome)
            .When(x => x.CamposPublicos is not null)
            .WithMessage(
                $"Campo fora do vocabulário de divulgação pública ({ConfiguracaoDivulgacao.NumeroInscricao}, " +
                $"{ConfiguracaoDivulgacao.NomeAbreviado}, {ConfiguracaoDivulgacao.Nome}).");

        RuleFor(x => x)
            .Must(static x => x.CamposPublicos!.Contains(ConfiguracaoDivulgacao.NumeroInscricao, StringComparer.Ordinal))
            .When(x => x.CamposPublicos is { Count: > 0 })
            .WithMessage($"'{ConfiguracaoDivulgacao.NumeroInscricao}' é o piso da divulgação pública — não pode ser removido.");

        RuleFor(x => x)
            .Must(static x => !(x.CamposPublicos!.Contains(ConfiguracaoDivulgacao.NomeAbreviado, StringComparer.Ordinal)
                && x.CamposPublicos!.Contains(ConfiguracaoDivulgacao.Nome, StringComparer.Ordinal)))
            .When(x => x.CamposPublicos is not null)
            .WithMessage($"'{ConfiguracaoDivulgacao.NomeAbreviado}' e '{ConfiguracaoDivulgacao.Nome}' não podem ser publicados ao mesmo tempo.");

        RuleFor(x => x)
            .Must(static x => !x.CamposPublicos!.Contains(ConfiguracaoDivulgacao.Nome, StringComparer.Ordinal)
                || !string.IsNullOrWhiteSpace(x.Justificativa))
            .When(x => x.CamposPublicos is not null)
            .WithMessage($"Publicar '{ConfiguracaoDivulgacao.Nome}' exige justificativa.");

        RuleFor(x => x.Justificativa)
            // O teto é aferido sobre a forma Trim+NFC, a MESMA que ConfiguracaoDivulgacao.Criar
            // mede — medir o valor bruto recusaria uma justificativa cujos espaços de borda a
            // normalização remove, tornando o contrato HTTP mais restritivo que o domínio (mesmo
            // raciocínio de AbrirRetificacaoCommandValidator/RascunhoRetificacao.MotivoMaxLength).
            .Must(static justificativa => justificativa is null
                || HashCanonicalComputer.NormalizeNfc(justificativa.Trim()).Length <= ConfiguracaoDivulgacao.JustificativaMaxLength)
            .WithMessage($"Justificativa deve ter no máximo {ConfiguracaoDivulgacao.JustificativaMaxLength} caracteres.")
            // Recusa cedo o caractere nulo (U+0000): ele sobrevive a IsNullOrWhiteSpace, Trim e
            // NormalizeNfc, chegaria ao SaveChanges e o PostgreSQL rejeitaria o byte zero em
            // coluna de texto com 500 em vez de 422. ConfiguracaoDivulgacao.Criar repete esta
            // defesa (é a fonte de verdade); aqui é só a resposta antecipada.
            .Must(static justificativa => justificativa is null || !justificativa.Contains(CaractereNulo))
            .WithMessage("Justificativa não pode conter o caractere nulo (U+0000).");
    }
}
