namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Onde uma resposta é usada: a fase em que quem usa se responde e, quando quem usa é um formulário,
/// o próprio formulário (ADR-0171). A exigência de documento usa a resposta numa fase sem ser formulário.
/// </summary>
public sealed record PontoDeUso(FaseCronograma Fase, FormularioDeclarado? Formulario);
