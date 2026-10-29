namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cadastra na Organização Institucional as unidades de <see cref="UnidadesDeTeste"/>, com os mesmos
/// ids: a publicação reconfere a unidade de cada fase no cadastro vivo, e os cenários semeados
/// declaram as fases com essas unidades.
/// </summary>
internal static class UnidadesDeTesteNoCadastro
{
    public static async Task SemearAsync(IServiceProvider servicos)
    {
        ArgumentNullException.ThrowIfNull(servicos);

        await using AsyncServiceScope scope = servicos.CreateAsyncScope();
        OrganizacaoInstitucionalDbContext org = scope.ServiceProvider.GetRequiredService<OrganizacaoInstitucionalDbContext>();
        foreach (UnidadeResponsavelSnapshot modelo in new[] { UnidadesDeTeste.Ceps, UnidadesDeTeste.Cga })
        {
            Unidade unidade = Unidade.Criar(
                nome: modelo.Nome,
                alias: null,
                slug: modelo.Sigla.ToLowerInvariant(),
                sigla: modelo.Sigla,
                codigo: modelo.Sigla,
                unidadeSuperiorId: null,
                tipo: TipoUnidade.Centro,
                unidadeAcademica: true,
                vigenciaInicio: new DateOnly(2020, 1, 1),
                vigenciaFim: null,
                cidadeCodigoIbge: "1504208",
                cidadeNome: "Marabá",
                cidadeUf: "PA").Value!;
            org.Entry(unidade).Property(u => u.Id).CurrentValue = modelo.OrigemId;
            org.Unidades.Add(unidade);
        }

        await org.SaveChangesAsync().ConfigureAwait(false);
    }
}
