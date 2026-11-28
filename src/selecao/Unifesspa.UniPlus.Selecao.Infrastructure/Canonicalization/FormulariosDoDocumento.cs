namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

/// <summary>
/// Os formulários que o documento congelado traz e o cronograma das fases dele: é onde cada campo, grupo e termo lido
/// encontra o formulário que o exige e a declaração do formulário, que depende das fases.
/// </summary>
internal sealed record FormulariosDoDocumento(IReadOnlyList<FormularioProcesso> Formularios, CronogramaDoProcesso Cronograma)
{
    /// <summary>O formulário de código dado; ausente quando o documento não traz nenhum com esse código.</summary>
    internal FormularioProcesso? De(string codigo) =>
        Formularios.FirstOrDefault(formulario => string.Equals(formulario.Codigo.Valor, codigo, StringComparison.Ordinal));

    /// <summary>A recusa do código de formulário que o documento não traz, no caminho do item que o cita.</summary>
    internal static FieldError RecusaDoFormularioDesconhecido(string path) => new(
        "formularioCodigo",
        new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.formularioCodigo' não é o código de um formulário do documento."));

    /// <summary>O que a estrutura do formulário confere dele, pelas fases que o documento congelou.</summary>
    internal DeclaracaoDoFormulario DeclaracaoDe(FormularioProcesso formulario) => formulario.Declarado.Estrutura(Cronograma);
}
