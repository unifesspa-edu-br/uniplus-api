namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// O formulário como o documento o declara: o código que o identifica, a fase do cronograma a que está ligado, pela
/// fase canônica de origem, e se é obrigatório.
/// </summary>
public sealed record FormularioNoDocumento(CodigoDoFormulario Codigo, Guid FaseCanonicaOrigemId, bool Obrigatorio);
