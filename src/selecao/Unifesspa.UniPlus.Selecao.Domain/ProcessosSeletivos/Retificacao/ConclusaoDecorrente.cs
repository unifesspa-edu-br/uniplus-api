namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// A fase cujo conjunto de formulários obrigatórios diminuiu na retificação, com os que restam na versão nova: quem
/// passa a cumpri-la tem a conclusão registrada, com a versão nova, na mesma operação (UNI-REQ-0211).
/// </summary>
public sealed record ConclusaoDecorrente(Guid FaseCanonicaOrigemId, IReadOnlyList<CodigoDoFormulario> ObrigatoriosNaVersaoNova);
