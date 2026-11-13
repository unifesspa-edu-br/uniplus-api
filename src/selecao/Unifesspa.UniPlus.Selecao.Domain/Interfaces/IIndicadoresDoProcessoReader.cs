namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed record ContagensDoProcesso(long Inscricoes, long Inscritos, long Rascunhos);

public interface IIndicadoresDoProcessoReader
{
    Task<ContagensDoProcesso> LerAsync(Guid processoSeletivoId, CancellationToken cancellationToken = default);
}
