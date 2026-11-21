namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Validação recursiva de UM nó da árvore — auto-referenciada via
/// <c>RuleForEach(x =&gt; x.Filhos).SetValidator(this)</c> (padrão documentado do
/// FluentValidation para estruturas em árvore): coerência de campos por
/// <see cref="NoExigenciaInput.Tipo"/> e delegação ao validador de folha.
/// </summary>
public sealed class NoExigenciaInputValidator : AbstractValidator<NoExigenciaInput>
{
    private static readonly string[] TiposValidos = ["FOLHA", "E", "OU"];

    // Cardinalidade qualificada, catálogo fechado (Domain.Enums.ChaveDistincaoCodigo).
    private static readonly string[] ChavesDistincaoValidas = ["COMPETENCIA_MENSAL", "EXERCICIO_ANUAL", "OCORRENCIA"];

    public NoExigenciaInputValidator()
    {
        RuleFor(x => x.Tipo)
            .Must(static valor => TiposValidos.Contains(valor, StringComparer.Ordinal))
            .WithMessage($"Tipo do nó deve ser um de: {string.Join(", ", TiposValidos)}.");
        RegrasDoDocumento();
        RegrasDosFilhos();
        RegrasDaQuantidadeMinima();
        RegrasDaDistincao();
        RegrasDaConsequencia();
        RegrasDasBasesLegaisDoGrupo();
    }

    private void RegrasDoDocumento()
    {
        RuleFor(x => x.Documento)
            .NotNull()
            .When(static x => x.Tipo == "FOLHA")
            .WithMessage("Um nó FOLHA precisa declarar 'documento'.");
        RuleFor(x => x.Documento)
            .Null()
            .When(static x => x.Tipo is "E" or "OU")
            .WithMessage("Um nó E/OU não pode declarar 'documento' — só folha declara.");
        RuleFor(x => x.Documento!)
            .SetValidator(new ItemDocumentoExigidoInputValidator())
            .When(static x => x.Tipo == "FOLHA" && x.Documento is not null);
    }

    private void RegrasDosFilhos()
    {
        RuleFor(x => x.Filhos)
            .Must(static filhos => filhos is { Count: > 0 })
            .When(static x => x.Tipo is "E" or "OU")
            .WithMessage("Um nó E/OU precisa de ao menos um filho.");
        RuleFor(x => x.Filhos)
            .Must(static filhos => filhos is null or { Count: 0 })
            .When(static x => x.Tipo == "FOLHA")
            .WithMessage("Um nó FOLHA não pode ter filhos.");
        RuleForEach(x => x.Filhos)
            .SetValidator(this)
            .When(static x => x.Filhos is { Count: > 0 });
    }

    // A quantidade mínima conta apresentações em FOLHA; só o grupo E fica sem cardinalidade própria (transparente).
    private void RegrasDaQuantidadeMinima()
    {
        RuleFor(x => x.Cardinalidade!.QuantidadeMinima)
            .Null()
            .When(static x => x.Tipo == "E" && x.Cardinalidade is not null)
            .WithMessage("cardinalidade.quantidadeMinima só é permitida em nó FOLHA ou OU/N-de.");
        RuleFor(x => x.Cardinalidade!.QuantidadeMinima)
            .GreaterThanOrEqualTo(1)
            .When(static x => x.Tipo is "OU" or "FOLHA" && x.Cardinalidade?.QuantidadeMinima is not null)
            .WithMessage("cardinalidade.quantidadeMinima, quando presente, deve ser maior ou igual a 1.");
    }

    // A coerência chave x dataReferencia x ocorrenciasEsperadas é do domínio (NoExigencia.CriarFolha); aqui só a
    // forma: catálogo fechado e campo restrito a FOLHA. A chave em branco também é recusada, em vez de o domínio
    // a tratar como "sem qualificação" e aceitar um pedido malformado.
    private void RegrasDaDistincao()
    {
        RuleFor(x => x.Cardinalidade!.Distincao!)
            .SetValidator(new DistincaoForaDeFolhaInputValidator())
            .When(static x => x.Tipo is "E" or "OU" && x.Cardinalidade?.Distincao is not null);
        RuleFor(x => x.Cardinalidade!.Distincao!.Chave)
            .Must(static valor => ChavesDistincaoValidas.Contains(valor, StringComparer.Ordinal))
            .When(static x => x.Tipo == "FOLHA" && x.Cardinalidade?.Distincao?.Chave is not null)
            .WithMessage($"cardinalidade.distincao.chave deve ser um de: {string.Join(", ", ChavesDistincaoValidas)}.");
    }

    private void RegrasDaConsequencia()
    {
        RuleFor(x => x.Indeferimento!.Consequencia)
            .Null()
            .When(static x => x.Tipo is "FOLHA" or "E" && x.Indeferimento is not null)
            .WithMessage("indeferimento.consequencia de nó só é permitida em grupo OU/N-de.");
        RuleFor(x => x.Indeferimento!.Consequencia)
            .Must(static valor => ConsequenciaIndeferimentoCodigo.EhValida(valor))
            .When(static x => x.Tipo == "OU" && !string.IsNullOrWhiteSpace(x.Indeferimento?.Consequencia))
            .WithMessage($"Consequência do grupo deve ser um de: {string.Join(", ", ConsequenciaIndeferimentoCodigo.Validas)}.");
    }

    private void RegrasDasBasesLegaisDoGrupo() =>
        RuleFor(x => x.Indeferimento!.BasesLegais)
            .Must(static basesLegais => basesLegais is null or { Count: 0 })
            .When(static x => x.Indeferimento is not null
                && (x.Tipo is "FOLHA" or "E" || string.IsNullOrWhiteSpace(x.Indeferimento.Consequencia)))
            .WithMessage("indeferimento.basesLegais de nó só é permitida em grupo OU/N-de com consequência declarada.");
}

/// <summary>A distinção das apresentações é exclusiva de FOLHA: num grupo E/OU nenhum dos campos pode vir preenchido.</summary>
public sealed class DistincaoForaDeFolhaInputValidator : AbstractValidator<DistincaoDasApresentacoesInput>
{
    public DistincaoForaDeFolhaInputValidator()
    {
        RuleFor(x => x.Chave)
            .Null()
            .WithMessage("cardinalidade.distincao.chave só é permitida em nó FOLHA.");
        RuleFor(x => x.DataReferencia)
            .Null()
            .WithMessage("cardinalidade.distincao.dataReferencia só é permitida em nó FOLHA.");
        RuleFor(x => x.OcorrenciasEsperadas)
            .Must(static ocorrencias => ocorrencias is null or { Count: 0 })
            .WithMessage("cardinalidade.distincao.ocorrenciasEsperadas só é permitida em nó FOLHA.");
    }
}
