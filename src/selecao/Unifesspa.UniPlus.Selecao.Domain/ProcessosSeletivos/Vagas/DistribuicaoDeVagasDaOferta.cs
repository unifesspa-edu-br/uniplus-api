namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// O que as regras entre as ofertas do processo precisam de cada uma: o curso a que a oferta pertence
/// e as modalidades que ela seleciona.
/// </summary>
public sealed record DistribuicaoDeVagasDaOferta(Guid OfertaCursoOrigemId, IReadOnlyList<ModalidadeSelecionada> Modalidades);
