namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// O que a configuração declara de um formulário do processo para as regras de alcance e de código: o
/// código que o identifica, a fase do cronograma a que está ligado e se é obrigatório. A fase é nula
/// enquanto o rascunho ainda não a escolheu (ADR-0171).
/// </summary>
public sealed record FormularioDeclarado(CodigoDoFormulario Codigo, Guid? FaseId, bool Obrigatorio);
