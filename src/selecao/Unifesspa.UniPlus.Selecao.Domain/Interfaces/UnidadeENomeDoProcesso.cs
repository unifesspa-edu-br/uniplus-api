namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>A unidade dona do processo e o nome dele: o que o acesso às inscrições precisa saber, numa consulta só.</summary>
/// <param name="UnidadeId">A unidade dona do processo.</param>
/// <param name="Nome">O nome do processo.</param>
public sealed record UnidadeENomeDoProcesso(Guid UnidadeId, string Nome);
