namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os documentos exigidos, as obrigatoriedades, os metadados dos fatos e os formulários lidos do envelope.
/// </summary>
internal sealed record BlocosDosDocumentos
{
    public required ResultadoConformidade? Conformidade { get; init; }

    public required IReadOnlyList<DocumentoExigido> DocumentosExigidos { get; init; }

    public required ReferenciaTemporalFatos? ReferenciaTemporalFatos { get; init; }

    public required IReadOnlyDictionary<string, MetadadoFatoCongelado>? MetadadosFatosCongelados { get; init; }

    public required IReadOnlyList<FormularioProcesso> Formularios { get; init; }

    public required IReadOnlyList<TermoExigidoFormulario> TermosExigidos { get; init; }
}
