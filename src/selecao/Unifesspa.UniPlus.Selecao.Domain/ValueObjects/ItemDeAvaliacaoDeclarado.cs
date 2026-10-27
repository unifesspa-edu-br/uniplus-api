namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O item de avaliação como o cliente o declara. Com <paramref name="Id"/> de um item existente da
/// etapa, é a mesma linha que muda; sem id, ou com um que a etapa não conhece, é um item novo.
/// </summary>
public sealed record ItemDeAvaliacaoDeclarado(Guid? Id, string Rotulo, decimal PontuacaoMaxima);
