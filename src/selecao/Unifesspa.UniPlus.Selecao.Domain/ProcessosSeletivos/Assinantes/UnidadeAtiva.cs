namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Assinantes;

/// <summary>Uma unidade ativa da estrutura institucional e a unidade acima dela, quando há.</summary>
internal sealed record UnidadeAtiva(Guid Id, Guid? SuperiorId);
