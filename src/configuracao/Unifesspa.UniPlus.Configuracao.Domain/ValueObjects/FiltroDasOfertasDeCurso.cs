namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>Restringe a listagem de ofertas pelo texto pesquisado e pelo curso.</summary>
public sealed record FiltroDasOfertasDeCurso(TermoDeBusca? Busca, Guid? CursoId);
