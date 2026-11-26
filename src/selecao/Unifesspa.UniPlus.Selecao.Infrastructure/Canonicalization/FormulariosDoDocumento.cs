namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Regras.Formularios;
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

    /// <summary>O que a estrutura do formulário confere dele, pelas fases que o documento congelou.</summary>
    internal DeclaracaoDoFormulario DeclaracaoDe(FormularioProcesso formulario) => formulario.Declarado.Estrutura(Cronograma);
}
