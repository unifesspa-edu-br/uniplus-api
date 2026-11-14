namespace Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Um processo seletivo de inscrição própria da unidade administradora de teste, só com o que a criação exige.</summary>
internal static class ProcessoSeletivoDeTeste
{
    public static ProcessoSeletivo Novo(string nome) => ProcessoSeletivo.Criar(
        nome, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    public static async Task<ProcessoSeletivo> GravarAsync(UniPlusDbContext context, string nome)
    {
        ArgumentNullException.ThrowIfNull(context);
        ProcessoSeletivo processo = Novo(nome);
        context.ProcessosSeletivos.Add(processo);
        await context.SaveChangesAsync();
        return processo;
    }
}
