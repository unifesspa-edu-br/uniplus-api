namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>Contagens agregadas das inscrições de um processo, sem dados individuais.</summary>
public sealed record ContagensDoProcesso(long Inscricoes, long Inscritos, long Rascunhos);

/// <summary>Lê os indicadores em uma única consulta consistente.</summary>
public interface IIndicadoresDoProcessoReader
{
    Task<ContagensDoProcesso> LerAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default);
}
