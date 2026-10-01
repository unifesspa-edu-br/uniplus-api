namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O catálogo de fatos na forma em que a escrita do formulário do processo confere as regras: a
/// view de cada fato, o registro neutro das regras compartilhadas, o vocabulário dos predicados e o
/// domínio dos categóricos cujos valores vêm do próprio processo.
/// </summary>
internal sealed record ContextoDoCatalogo(
    IReadOnlyDictionary<string, FatoCandidatoView> Fatos,
    IReadOnlyDictionary<string, FatoDoCatalogo> FatosDasRegras,
    IReadOnlyDictionary<string, DescritorFatoCandidato> Vocabulario,
    IReadOnlyDictionary<string, DominioDeValores> DominiosDinamicos)
{
    /// <summary>
    /// O contexto do catálogo lido para o processo: o domínio dos fatos categóricos cuja fonte é o
    /// processo vem dele mesmo, nunca de um catálogo global.
    /// </summary>
    public static ContextoDoCatalogo De(ProcessoSeletivo processo, IReadOnlyList<FatoCandidatoView> catalogo) => new(
        catalogo.ToDictionary(static f => f.Codigo, StringComparer.Ordinal),
        VocabularioDeFatos.ParaRegras(catalogo),
        VocabularioDeFatos.Descritores(catalogo),
        VocabularioDeFatos.DominiosDinamicos(processo, catalogo));
}
