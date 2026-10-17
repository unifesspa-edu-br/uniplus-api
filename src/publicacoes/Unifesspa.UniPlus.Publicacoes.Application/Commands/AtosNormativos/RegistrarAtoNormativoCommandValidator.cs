namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

using FluentValidation;

/// <summary>
/// Antecipa a recusa de shape que o agregado faria, devolvendo 400 antes de o
/// handler tocar o repositório. As regras de texto avaliam o valor
/// <b>normalizado</b> (trim), como a factory; por isso o nome da propriedade no
/// <c>ProblemDetails</c> vem de <c>OverridePropertyName</c>.
/// </summary>
public sealed class RegistrarAtoNormativoCommandValidator
    : AbstractValidator<RegistrarAtoNormativoCommand>
{
    public RegistrarAtoNormativoCommandValidator()
    {
        RuleFor(x => AtoNormativoRegras.Normalizar(x.Orgao))
            .NotEmpty().WithMessage(AtoNormativoRegras.OrgaoObrigatorio)
            .MaximumLength(AtoNormativoRegras.OrgaoMaxLength).WithMessage(AtoNormativoRegras.OrgaoTamanho)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.Orgao));

        RuleFor(x => AtoNormativoRegras.Normalizar(x.Serie))
            .NotEmpty().WithMessage(AtoNormativoRegras.SerieObrigatoria)
            .MaximumLength(AtoNormativoRegras.SerieMaxLength).WithMessage(AtoNormativoRegras.SerieTamanho)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.Serie));

        RuleFor(x => x.Ano)
            .GreaterThan(0).WithMessage(AtoNormativoRegras.AnoInvalido);

        RuleFor(x => AtoNormativoRegras.Normalizar(x.Numero))
            .MaximumLength(AtoNormativoRegras.NumeroMaxLength).WithMessage(AtoNormativoRegras.NumeroTamanho)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.Numero))
            .When(x => x.Numero is not null);

        RuleFor(x => AtoNormativoRegras.Normalizar(x.TipoCodigo))
            .NotEmpty().WithMessage(AtoNormativoRegras.TipoCodigoObrigatorio)
            .MaximumLength(AtoNormativoRegras.TipoCodigoMaxLength).WithMessage(AtoNormativoRegras.TipoCodigoTamanho)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.TipoCodigo));

        RuleFor(x => x.DocumentoHash)
            .NotEmpty().WithMessage(AtoNormativoRegras.DocumentoHashObrigatorio)
            .Matches(AtoNormativoRegras.HashPattern).WithMessage(AtoNormativoRegras.DocumentoHashFormato);

        RuleFor(x => AtoNormativoRegras.Normalizar(x.Assinante))
            .NotEmpty().WithMessage(AtoNormativoRegras.AssinanteObrigatorio)
            .MaximumLength(AtoNormativoRegras.AssinanteMaxLength).WithMessage(AtoNormativoRegras.AssinanteTamanho)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.Assinante));

        // O par {id, hash} é completo ou ausente (AC7).
        RuleFor(x => x)
            .Must(x => x.VersaoInvocadaId.HasValue == (x.VersaoInvocadaHash is not null))
            .WithMessage(AtoNormativoRegras.VersaoInvocadaIncompleta)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.VersaoInvocadaHash));

        // Id zerado não é referência: um Guid.Empty não aponta versão alguma.
        RuleFor(x => x.VersaoInvocadaId)
            .Must(id => id != Guid.Empty).WithMessage(AtoNormativoRegras.VersaoInvocadaIdObrigatorio)
            .When(x => x.VersaoInvocadaId.HasValue);

        RuleFor(x => x.VersaoInvocadaHash)
            .Matches(AtoNormativoRegras.HashPattern).WithMessage(AtoNormativoRegras.VersaoInvocadaHashFormato)
            .When(x => x.VersaoInvocadaHash is not null);

        // A retificação é o par (ato retificado, motivo), completo ou ausente (ADR-0103).
        RuleFor(x => x)
            .Must(x => x.AtoRetificadoId.HasValue == (AtoNormativoRegras.Normalizar(x.MotivoRetificacao) is not null))
            .WithMessage(AtoNormativoRegras.RetificacaoIncompleta)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.MotivoRetificacao));

        // Id zerado não é referência: um Guid.Empty não aponta ato algum.
        RuleFor(x => x.AtoRetificadoId)
            .Must(id => id != Guid.Empty).WithMessage(AtoNormativoRegras.AtoRetificadoIdObrigatorio)
            .When(x => x.AtoRetificadoId.HasValue);

        RuleFor(x => AtoNormativoRegras.Normalizar(x.MotivoRetificacao))
            .MaximumLength(AtoNormativoRegras.MotivoRetificacaoMaxLength).WithMessage(AtoNormativoRegras.MotivoRetificacaoTamanho)
            .OverridePropertyName(nameof(RegistrarAtoNormativoCommand.MotivoRetificacao))
            .When(x => AtoNormativoRegras.Normalizar(x.MotivoRetificacao) is not null);

        // Vínculos com entidades de outros domínios (ADR-0105). A forma do rótulo é
        // verificada; o valor, jamais — não há tipos permitidos, e não pode haver.
        //
        // O elemento nulo é recusado explicitamente: a anotação de nulidade do record não
        // impede um `"vinculos": [null]` no corpo, e sem esta regra o nulo atravessaria a
        // validação e estouraria no handler — 500 onde cabe 422.
        RuleForEach(x => x.Vinculos)
            .NotNull().WithMessage(AtoNormativoRegras.VinculoNulo)
            .When(x => x.Vinculos is not null);

        RuleForEach(x => x.Vinculos)
            .ChildRules(vinculo =>
            {
                vinculo.RuleFor(v => AtoNormativoRegras.Normalizar(v.EntidadeTipo))
                    .NotEmpty().WithMessage(AtoNormativoRegras.EntidadeTipoObrigatorio)
                    .MaximumLength(AtoNormativoRegras.EntidadeTipoMaxLength).WithMessage(AtoNormativoRegras.EntidadeTipoTamanho)
                    .Matches(AtoNormativoRegras.EntidadeTipoPattern).WithMessage(AtoNormativoRegras.EntidadeTipoFormato)
                    .OverridePropertyName(nameof(VinculoEntidadeInput.EntidadeTipo));

                vinculo.RuleFor(v => v.EntidadeId)
                    .NotEmpty().WithMessage(AtoNormativoRegras.EntidadeIdObrigatorio);
            })
            .When(x => x.Vinculos is not null && x.Vinculos.All(v => v is not null));

        // O vínculo é único por trio (ato, tipo, id): repetir a mesma entidade no mesmo
        // ato não acrescenta vínculo nenhum, e o payload está internamente incoerente.
        RuleFor(x => x.Vinculos)
            .Must(SemEntidadeRepetida).WithMessage(AtoNormativoRegras.VinculoDuplicado)
            .When(x => x.Vinculos is { Count: > 1 });
    }

    private static bool SemEntidadeRepetida(IReadOnlyList<VinculoEntidadeInput>? vinculos)
    {
        if (vinculos is null)
        {
            return true;
        }

        // O elemento nulo tem regra própria; aqui ele não é duplicata de coisa alguma.
        HashSet<(string, Guid)> vistos = new(vinculos.Count);
        return vinculos
            .Where(v => v is not null)
            .All(v => vistos.Add((AtoNormativoRegras.Normalizar(v.EntidadeTipo) ?? string.Empty, v.EntidadeId)));
    }
}
