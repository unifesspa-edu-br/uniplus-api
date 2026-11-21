namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Semeia uma obrigatoriedade legal universal que exige uma etapa que o processo base
/// (<see cref="ProcessoSeletivoPublicavelSeeder"/>) não tem.
/// </summary>
internal static class ObrigatoriedadeLegalNaoAtendidaSeeder
{
    /// <summary>Código do tipo de etapa que a regra exige e o processo base não tem.</summary>
    public const string TipoEtapaNaoOfertada = "ENTREVISTA_NAO_OFERTADA";

    /// <summary>
    /// Semeia uma obrigatoriedade legal universal (Story #853) que exige uma etapa que o
    /// processo base (<see cref="ProcessoSeletivoPublicavelSeeder"/>) não tem — a publicação
    /// recusa por <c>ProcessoSeletivo.ConformidadeLegalInsuficiente</c> com o checklist
    /// ESTRUTURAL 100% verde (CA-05/CA-07: <c>obrigatoriedadesReprovadas</c> aparece,
    /// <c>pendencias</c> não).
    /// </summary>
    /// <remarks>
    /// O tipo de etapa exigido precisa estar vivo no cadastro de Configuração
    /// (<see cref="TiposDeEtapaSeeder"/>): a publicação recusa antes de avaliar quando a
    /// regra referencia código inexistente, e a recusa seria por outro motivo — a regra
    /// ficaria inavaliável em vez de reprovada.
    /// </remarks>
    public static async Task<Guid> SemearAsync(UniPlusDbContext db, string regraCodigo)
    {
        ObrigatoriedadeLegal regra = ObrigatoriedadeLegal.Criar(
            ObrigatoriedadeLegal.TipoProcessoUniversal,
            CategoriaObrigatoriedade.Outros,
            regraCodigo,
            new EtapaObrigatoria(TipoEtapaNaoOfertada),
            new TextoNormativo("Etapa de entrevista obrigatória (teste de integração issue #1096)", "Base legal de teste", null, null),
            new DateOnly(2020, 1, 1),
            null).Value!;

        ObrigatoriedadeLegalRepository repository = new(db, TimeProvider.System);
        await repository.AdicionarAsync(regra, CancellationToken.None);
        await db.SaveChangesAsync();

        return regra.Id;
    }
}
