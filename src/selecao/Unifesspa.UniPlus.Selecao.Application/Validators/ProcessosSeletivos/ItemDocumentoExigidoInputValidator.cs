namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>Validação de forma de UMA folha (<see cref="ItemDocumentoExigidoInput"/>).</summary>
/// <remarks>
/// A aplicabilidade, a consequência do indeferimento, o tamanho máximo e a coerência entre geral e condição têm
/// equivalente incondicional em <c>DocumentoExigido.ValidarFormaBasica</c> (ADR-0125), que acumula; mantê-las aqui
/// faria o FluentValidation (que roda antes do handler) bloquear no primeiro erro e tornaria a acumulação do
/// domínio inatingível. O mesmo vale para a forma das bases legais, que o domínio acumula.
/// </remarks>
public sealed class ItemDocumentoExigidoInputValidator : AbstractValidator<ItemDocumentoExigidoInput>
{
    public ItemDocumentoExigidoInputValidator()
    {
        RuleFor(i => i.TipoDocumento)
            .Must(static tipo => tipo is not null && tipo.Id != Guid.Empty)
            .WithMessage("O id do tipo de documento é obrigatório.");
        RuleFor(i => i.Momento)
            .Must(static momento => momento is not null && momento.FaseId != Guid.Empty)
            .WithMessage("A fase em que o documento é exigido é obrigatória.");
        RegrasDosObjetosAninhados();
    }

    private void RegrasDosObjetosAninhados()
    {
        RuleForEach(i => i.Aplicabilidade!.Condicoes)
            .SetValidator(new CondicaoGatilhoInputValidator())
            .When(static i => i.Aplicabilidade?.Condicoes is not null);
        RuleForEach(i => i.Indeferimento!.BasesLegais)
            .NotNull()
            .WithMessage("Item de base legal não pode ser nulo.")
            .SetValidator(new BaseLegalInputValidator())
            .When(static i => i.Indeferimento?.BasesLegais is not null);
        RuleFor(i => i.Requisitos!.IdadeMaximaEmissao!)
            .SetValidator(new IdadeMaximaEmissaoInputValidator())
            .When(static i => i.Requisitos?.IdadeMaximaEmissao is not null);
    }
}

/// <summary>Forma de uma condição do gatilho: cláusula não negativa, fato, operador do catálogo e valor.</summary>
public sealed class CondicaoGatilhoInputValidator : AbstractValidator<CondicaoGatilhoInput>
{
    private static readonly string[] OperadoresValidos = ["IGUAL", "EM", "MAIOR_IGUAL", "MENOR_IGUAL", "DIFERENTE", "NAO_EM"];

    public CondicaoGatilhoInputValidator()
    {
        RuleFor(c => c.Clausula).GreaterThanOrEqualTo(0).WithMessage("O ordinal da cláusula não pode ser negativo.");
        RuleFor(c => c.Fato).NotEmpty().WithMessage("O fato da condição é obrigatório.");
        RuleFor(c => c.Operador)
            .Must(valor => OperadoresValidos.Contains(valor, StringComparer.Ordinal))
            .WithMessage($"Operador deve ser um de: {string.Join(", ", OperadoresValidos)}.");
        RuleFor(c => c.Valor).NotEmpty().WithMessage("O valor da condição é obrigatório.");
    }
}

/// <summary>
/// Forma de uma base legal da exigência: só o teto de tamanho, que espelha o mapeamento de
/// <c>DocumentoExigidoBaseLegal</c> — sem ele, um texto longo demais passaria e só falharia ao gravar (500).
/// </summary>
public sealed class BaseLegalInputValidator : AbstractValidator<BaseLegalInput>
{
    private const int ReferenciaMaxLength = 500;
    private const int ObservacaoMaxLength = 1000;

    public BaseLegalInputValidator()
    {
        RuleFor(b => b.Referencia)
            .MaximumLength(ReferenciaMaxLength)
            .WithMessage($"A referência da base legal deve ter no máximo {ReferenciaMaxLength} caracteres.");
        RuleFor(b => b.Observacao)
            .MaximumLength(ObservacaoMaxLength)
            .When(b => b.Observacao is not null)
            .WithMessage($"A observação da base legal deve ter no máximo {ObservacaoMaxLength} caracteres.");
    }
}

/// <summary>
/// Forma de cada campo não nulo da idade máxima de emissão. A coerência tudo-nulo ou completo é do domínio
/// (<c>IdadeMaximaEmissao.Criar</c>).
/// </summary>
public sealed class IdadeMaximaEmissaoInputValidator : AbstractValidator<IdadeMaximaEmissaoInput>
{
    private static readonly string[] UnidadesValidas = ["DIAS", "MESES", "ANOS"];

    private static readonly string[] ReferenciaTiposValidos =
        ["FIM_INSCRICAO", "INICIO_FASE", "FIM_FASE", "DATA_ESPECIFICA", "DATA_SUBMISSAO"];

    public IdadeMaximaEmissaoInputValidator()
    {
        RuleFor(i => i.Valor)
            .GreaterThan(0)
            .WithMessage("O valor da idade máxima de emissão deve ser maior que zero.");
        RuleFor(i => i.Unidade)
            .Must(valor => UnidadesValidas.Contains(valor, StringComparer.Ordinal))
            .When(i => i.Unidade is not null)
            .WithMessage($"Unidade da idade máxima de emissão deve ser um de: {string.Join(", ", UnidadesValidas)}.");
        RuleFor(i => i.ReferenciaTipo)
            .Must(valor => ReferenciaTiposValidos.Contains(valor, StringComparer.Ordinal))
            .When(i => i.ReferenciaTipo is not null)
            .WithMessage($"ReferenciaTipo da idade máxima de emissão deve ser um de: {string.Join(", ", ReferenciaTiposValidos)}.");
    }
}
