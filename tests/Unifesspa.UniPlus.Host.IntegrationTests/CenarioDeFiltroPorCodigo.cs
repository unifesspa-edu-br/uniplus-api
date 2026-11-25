namespace Unifesspa.UniPlus.Host.IntegrationTests;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Application.Permissoes;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class CenarioDeFiltroPorCodigo
{
    internal const string Pessoa = "leandro.viana";

    internal static async Task<Guid> PrepararAsync(IServiceProvider servicos)
    {
        (Guid processo, Guid unidade) = await SemearProcessoAsync(servicos);
        await ConcessoesDeTeste.ConcederPermissoesAsync(servicos, Pessoa,
            PermissaoSelecao.RegrasCatalogoConsultar.Codigo, PermissaoSelecao.LeiautesInepConsultar.Codigo,
            PermissaoConfiguracao.FatosCandidatoConsultar.Codigo, PermissaoConfiguracao.ModelosFormularioConsultar.Codigo);
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(servicos, PermissaoSelecao.ProcessosPublicar.Codigo);
        await ConcessoesDeTeste.ConcederAsync(servicos, Pessoa, perfil, EscopoDaConcessao.DaUnidade(unidade));
        return processo;
    }

    private static async Task<(Guid Processo, Guid Unidade)> SemearProcessoAsync(IServiceProvider servicos)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string sufixo = Guid.NewGuid().ToString("N");
        Unidade unidade = Unidade.Criar("Centro de Processos Seletivos", new IdentificadoresDaUnidade($"ceps-{sufixo}"[..20], $"C{sufixo}"[..10], $"U{sufixo}"[..14], null),
            CidadeDaUnidade.Nenhuma, new VigenciaDaUnidade(new DateOnly(2026, 1, 1), null), null, TipoUnidade.Centro, false).Value!;
        ProcessoSeletivo processo = CriarProcesso(unidade.Id, sufixo);
        banco.Set<Unidade>().Add(unidade);
        banco.Set<ProcessoSeletivo>().Add(processo);
        await banco.SaveChangesAsync();
        return (processo.Id, unidade.Id);
    }

    private static ProcessoSeletivo CriarProcesso(Guid unidade, string sufixo) => ProcessoSeletivo.Criar(
        "Processo de teste", TiposDeProcessoDeTeste.SiSU, OrigemCandidatos.InscricaoPropria, unidade,
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadorLegivel.Criar($"certame-{sufixo}"[..40]).Value!);
}
