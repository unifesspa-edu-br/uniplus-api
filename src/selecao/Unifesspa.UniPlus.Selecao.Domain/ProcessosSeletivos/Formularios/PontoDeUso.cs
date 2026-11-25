namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Onde uma resposta é usada: a fase em que quem usa se responde e, quando quem usa é um formulário,
/// o próprio formulário (ADR-0171). O ponto da exigência de documento é o formulário que a entrega, na
/// fase dela; o ponto da regra de campo, de página ou de termo é o formulário dono. Sem formulário, o ponto
/// é só a fase.
/// </summary>
public sealed record PontoDeUso(FaseCronograma Fase, FormularioDeclarado? Formulario);
