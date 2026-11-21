namespace Unifesspa.UniPlus.Selecao.Application.Validators.ProcessosSeletivos;

using Commands.ProcessosSeletivos;

using FluentValidation;

/// <summary>
/// Validação recursiva de UM nó da árvore — auto-referenciada via
/// <c>RuleForEach(x =&gt; x.Filhos).SetValidator(this)</c> (padrão documentado do
/// FluentValidation para estruturas em árvore): coerência de campos por
/// <see cref="NoExigenciaInput.Tipo"/> e delegação ao validador de folha.
/// </summary>
public sealed class NoExigenciaInputValidator : AbstractValidator<NoExigenciaInput>
{
    public NoExigenciaInputValidator()
    {
        RegrasDoDocumento();
        RegrasDosFilhos();
        RegrasDaDistincao();
        RegrasDaConsequencia();
        RegrasDasBasesLegaisDoGrupo();
    }

    private void RegrasDoDocumento()
    {
        RuleFor(x => x.Documento!)
            .SetValidator(new ItemDocumentoExigidoInputValidator())
            .When(static x => x.Tipo == "FOLHA" && x.Documento is not null);
    }

    private void RegrasDosFilhos()
    {
        RuleForEach(x => x.Filhos)
            .SetValidator(this)
            .When(static x => x.Filhos is { Count: > 0 });
    }

    // A coerência chave x dataReferencia x ocorrenciasEsperadas é do domínio (NoExigencia.CriarFolha); aqui só a
    // forma: catálogo fechado e campo restrito a FOLHA. A chave em branco também é recusada, em vez de o domínio
    // a tratar como "sem qualificação" e aceitar um pedido malformado.
    private void RegrasDaDistincao()
    {
        RuleFor(x => x.Cardinalidade!.Distincao!)
            .SetValidator(new DistincaoForaDeFolhaInputValidator())
            .When(static x => x.Tipo is "E" or "OU" && x.Cardinalidade?.Distincao is not null);

    }

    private void RegrasDaConsequencia() =>
        RuleFor(x => x.Indeferimento!.Consequencia)
            .Null()
            .When(static x => x.Tipo == "FOLHA" && x.Indeferimento is not null)
            .WithMessage("indeferimento.consequencia de nó só é permitida em grupo OU/N-de.");

    private void RegrasDasBasesLegaisDoGrupo() =>
        RuleFor(x => x.Indeferimento!.BasesLegais)
            .Must(static basesLegais => basesLegais is null or { Count: 0 })
            .When(static x => x.Tipo == "FOLHA" && x.Indeferimento is not null)
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
