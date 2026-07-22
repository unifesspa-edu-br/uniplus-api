namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Valida o bloco documental do ato ANTES de a publicação ser gravada.
/// </summary>
/// <remarks>
/// A ordem importa: o registro do ato acontece depois, por mensagem durável (ADR-0108).
/// Sem estas regras, um <c>ano: 0</c> ou um órgão em branco passariam pela publicação, o
/// Edital seria gravado, o cliente receberia 204 — e só então a requisição morreria na
/// dead letter, deixando Seleção publicada sem o ato correspondente. O que o formato do
/// documento pode recusar tem de ser recusado com 422, na hora, e não virar incidente
/// operacional depois.
/// <para>
/// Os limites espelham os de <c>AtoNormativo</c> em Publicações. Duplicá-los aqui é o
/// preço de os módulos não compartilharem entidade — e é preferível a publicar um edital
/// cujo ato o outro módulo vai recusar.
/// </para>
/// </remarks>
public sealed class DadosDoAtoValidator : AbstractValidator<DadosDoAto>
{
    private const int OrgaoMaxLength = 200;
    private const int SerieMaxLength = 100;
    private const int AssinanteMaxLength = 200;
    private const int TipoCodigoMaxLength = 60;

    public DadosDoAtoValidator()
    {
        RuleFor(x => x.Orgao)
            .NotEmpty()
            .WithMessage("Órgão publicador do ato é obrigatório.")
            .MaximumLength(OrgaoMaxLength)
            .WithMessage($"Órgão publicador deve ter no máximo {OrgaoMaxLength} caracteres.");

        RuleFor(x => x.Serie)
            .NotEmpty()
            .WithMessage("Série do ato é obrigatória.")
            .MaximumLength(SerieMaxLength)
            .WithMessage($"Série do ato deve ter no máximo {SerieMaxLength} caracteres.");

        // Campo omitido do corpo binda para 0 (int não-nullable) — sem esta regra, o ano
        // ausente passaria trivialmente e só seria recusado ao registrar o ato.
        RuleFor(x => x.Ano)
            .GreaterThan(0)
            .WithMessage("Ano do ato é obrigatório.");

        // Mesma armadilha: DateOnly omitido binda para 0001-01-01.
        RuleFor(x => x.DataPublicacao)
            .NotEqual(default(DateOnly))
            .WithMessage("Data de publicação declarada pelo ato é obrigatória.");

        RuleFor(x => x.Assinante)
            .NotEmpty()
            .WithMessage("Assinante do ato é obrigatório.")
            .MaximumLength(AssinanteMaxLength)
            .WithMessage($"Assinante deve ter no máximo {AssinanteMaxLength} caracteres.");

        RuleFor(x => x.TipoAtoCodigo)
            .NotEmpty()
            .WithMessage("Tipo do ato é obrigatório.")
            .MaximumLength(TipoCodigoMaxLength)
            .WithMessage($"Tipo do ato deve ter no máximo {TipoCodigoMaxLength} caracteres.")
            // Forma canônica do catálogo de Publicações (UPPER_SNAKE). Não se valida o VALOR
            // aqui — a lista de tipos é cadastro, e conhecê-la seria ramificar por tipo, o
            // que a ADR-0103 proíbe. Valida-se só a forma.
            .Matches("^[A-Z]+(_[A-Z]+)*$")
            .WithMessage("Tipo do ato deve estar em forma canônica (ex.: EDITAL_ABERTURA).");
    }
}
