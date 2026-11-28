namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Valida o bloco documental do ato ANTES de a publicação ser gravada.
/// </summary>
/// <remarks>
/// A ordem importa: o formato que o registro central recusaria é recusado aqui, com 422 no
/// campo, antes de a transação rastrear a versão, o ato e a divulgação. Sem estas regras, um
/// <c>ano: 0</c> ou um órgão em branco só seriam recusados pela porta, no fim, sem apontar o
/// campo.
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

    // Texto ausente, nulo ou em branco é recusado na conferência do ato, com o código de campo obrigatório;
    // aqui, só o tamanho do que veio.
    public DadosDoAtoValidator()
    {
        ValidarOrgaoESerie();
        ValidarAnoEData();
        ValidarAssinante();
        ValidarTipoDoAto();
    }

    private void ValidarOrgaoESerie()
    {
        RuleFor(x => x.Orgao)
            .MaximumLength(OrgaoMaxLength)
            .WithMessage($"Órgão publicador deve ter no máximo {OrgaoMaxLength} caracteres.");

        RuleFor(x => x.Serie)
            .MaximumLength(SerieMaxLength)
            .WithMessage($"Série do ato deve ter no máximo {SerieMaxLength} caracteres.");
    }

    private void ValidarAnoEData()
    {
        // O ano e a data ausentes são recusados na conferência do ato, com o código de campo obrigatório;
        // aqui, só o valor informado fora da faixa.
        RuleFor(x => x.Ano)
            .GreaterThan(0)
            .WithMessage("Ano do ato deve ser maior que zero.");

        RuleFor(x => x.DataDoAto)
            .NotEqual(default(DateOnly))
            .WithMessage("Data do ato declarada pelo documento é inválida.");
    }

    private void ValidarAssinante()
    {
        RuleFor(x => x.Assinante)
            .MaximumLength(AssinanteMaxLength)
            .WithMessage($"Assinante deve ter no máximo {AssinanteMaxLength} caracteres.");
    }

    private void ValidarTipoDoAto()
    {
        RuleFor(x => x.TipoAtoCodigo)
            .MaximumLength(TipoCodigoMaxLength)
            .WithMessage($"Tipo do ato deve ter no máximo {TipoCodigoMaxLength} caracteres.");
    }
}
