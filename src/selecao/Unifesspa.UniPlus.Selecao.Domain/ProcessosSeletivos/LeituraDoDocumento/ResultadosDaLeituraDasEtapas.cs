namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;

/// <summary>
/// As etapas que a leitura adotou, na ordem canônica, e o que as partes seguintes (a classificação) precisam delas: o id em memória
/// de cada etapa pela chave que o documento declarou, as chaves das etapas que a leitura recusou e as recusas e as origens que ligam
/// cada parte adotada à posição em que veio. A chave recusada se distingue da inexistente: quem cita a recusada não acumula causa nova.
/// </summary>
internal sealed record EtapasLidas(
    IReadOnlyList<EtapaProcesso> Etapas,
    IReadOnlyDictionary<string, Guid> IdPelaChave,
    IReadOnlySet<string> ChavesRecusadas,
    IReadOnlyList<RecusaDaParte> Recusas,
    IReadOnlyList<OrigemNoDocumento> Origens);
