namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using FluentValidation;

using Commands.ProcessosSeletivos;
using Domain.Entities;
using Domain.ValueObjects;

/// <summary>
/// Validação de contrato da abertura da sessão editorial (Story #860).
/// </summary>
/// <remarks>
/// O domínio já recusa motivo em branco e motivo longo demais — e continua recusando: ele
/// é a fonte da verdade, e a <see cref="RascunhoRetificacao"/> não confia em quem a chama.
/// O que este validator acrescenta é o <b>422 de contrato</b>, na mesma forma que os outros
/// oito comandos deste módulo devolvem, para que o cliente receba a mesma resposta em toda
/// a superfície do controller em vez de descobrir que esta rota é a única diferente.
/// </remarks>
public sealed class AbrirRetificacaoCommandValidator : AbstractValidator<AbrirRetificacaoCommand>
{
    public AbrirRetificacaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("Id do processo seletivo é obrigatório.");

        RuleFor(x => x.Motivo)
            .NotEmpty()
            .WithMessage("Motivo da retificação é obrigatório.")
            // O teto é aferido sobre o valor NORMALIZADO, que é o que será persistido: a
            // normalização NFC pode EXPANDIR code points (U+0958 → U+0915 U+093C), e medir o
            // valor cru deixaria passar um motivo que só estoura o limite depois dela.
            .Must(motivo => motivo is null
                || HashCanonicalComputer.NormalizeNfc(motivo.Trim()).Length <= RascunhoRetificacao.MotivoMaxLength)
            .WithMessage($"Motivo da retificação deve ter no máximo {RascunhoRetificacao.MotivoMaxLength} caracteres.");
    }
}

/// <summary>
/// Validação de contrato da alteração do motivo (Story #860) — mesmas regras da abertura.
/// </summary>
public sealed class AlterarMotivoRetificacaoCommandValidator : AbstractValidator<AlterarMotivoRetificacaoCommand>
{
    public AlterarMotivoRetificacaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("Id do processo seletivo é obrigatório.");

        RuleFor(x => x.Motivo)
            .NotEmpty()
            .WithMessage("Motivo da retificação é obrigatório.")
            .Must(motivo => motivo is null
                || HashCanonicalComputer.NormalizeNfc(motivo.Trim()).Length <= RascunhoRetificacao.MotivoMaxLength)
            .WithMessage($"Motivo da retificação deve ter no máximo {RascunhoRetificacao.MotivoMaxLength} caracteres.");
    }
}

/// <summary>
/// Validação de contrato do fechamento (Story #862). Espelha o
/// <see cref="RetificarProcessoSeletivoCommandValidator"/> — <b>menos o motivo</b>, que não
/// vem no corpo: ele já foi declarado na abertura e vive no rascunho.
/// </summary>
public sealed class FecharRetificacaoCommandValidator : AbstractValidator<FecharRetificacaoCommand>
{
    private const int NumeroMaxLength = 60;

    public FecharRetificacaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("Id do processo seletivo é obrigatório.");

        RuleFor(x => x.Ato)
            .NotNull()
            .WithMessage("Dados do ato normativo são obrigatórios.")
            .SetValidator(new DadosDoAtoValidator()!);

        RuleFor(x => x.DocumentoEditalId)
            .NotEmpty()
            .WithMessage("Referência ao documento do Edital é obrigatória.");

        RuleFor(x => x.Numero)
            .MaximumLength(NumeroMaxLength)
            .WithMessage($"Número do Edital deve ter no máximo {NumeroMaxLength} caracteres.");

        RuleFor(x => x.PeriodoInscricaoInicio)
            .NotEqual(default(DateOnly))
            .WithMessage("Início do período de inscrição é obrigatório.");

        RuleFor(x => x.PeriodoInscricaoFim)
            .NotEqual(default(DateOnly))
            .WithMessage("Fim do período de inscrição é obrigatório.")
            .GreaterThanOrEqualTo(x => x.PeriodoInscricaoInicio)
            .WithMessage("O fim do período de inscrição não pode anteceder o início.");
    }
}

/// <summary>
/// Validação de contrato do descarte (Story #861). O descarte não tem corpo — só o id da
/// rota e a precondição do header.
/// </summary>
public sealed class DescartarRetificacaoCommandValidator : AbstractValidator<DescartarRetificacaoCommand>
{
    public DescartarRetificacaoCommandValidator()
    {
        RuleFor(x => x.ProcessoSeletivoId)
            .NotEmpty()
            .WithMessage("Id do processo seletivo é obrigatório.");
    }
}
