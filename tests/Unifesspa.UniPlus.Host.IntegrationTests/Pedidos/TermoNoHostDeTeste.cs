namespace Unifesspa.UniPlus.Host.IntegrationTests.Pedidos;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;

/// <summary>
/// O termo de responsabilidade no banco do host sob teste: volta ao estado da carga inicial, publica versões como a
/// produção (pelo agregado, sob a trava exclusiva) e informa a versão que um pedido fixou ou que a criação deve apresentar.
/// </summary>
internal static class TermoNoHostDeTeste
{
    public const string Ato = "Portaria nº 123/2026 da Reitoria";

    private static readonly Identidade Publicador = new(TestAuthHandler.DefaultIssuer, "diogo.souza", "Diogo Bruno Filipe Souza");

    public static string Texto(string marca) =>
        $"Termo de responsabilidade ({marca}). Declaro que usarei o acesso concedido somente para as finalidades da minha função, "
        + "que guardarei sigilo sobre os dados pessoais a que tiver acesso e que comunicarei à unidade responsável qualquer incidente de segurança.";

    /// <summary>Apaga os aceites, as exceções da instalação e as versões, e deixa o registro sem vigente.</summary>
    public static async Task ReiniciarAsync(IServiceProvider servicos)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        DatabaseFacade banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Database;
        await banco.ExecuteSqlRawAsync("DELETE FROM autorizacao.aceites_do_termo");
        await banco.ExecuteSqlRawAsync("DELETE FROM autorizacao.termos_na_instalacao");
        await banco.ExecuteSqlRawAsync("UPDATE autorizacao.pedidos_de_concessao SET termo_versao_id = NULL, termo_numero = NULL, termo_resumo = NULL");
        await banco.ExecuteSqlRawAsync("UPDATE autorizacao.termos_de_responsabilidade SET versao_vigente_id = NULL, ultimo_numero = 0");
        await banco.ExecuteSqlRawAsync("DELETE FROM autorizacao.versoes_do_termo");
    }

    /// <summary>Publica a versão seguinte, numa transação própria, e a devolve.</summary>
    public static async Task<VersaoDoTermo> PublicarAsync(IServiceProvider servicos, string marca)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ITermoDeResponsabilidadeRepository termos = escopo.ServiceProvider.GetRequiredService<ITermoDeResponsabilidadeRepository>();
        await using IDbContextTransaction transacao = await contexto.Database.BeginTransactionAsync();
        (TermoDeResponsabilidade termo, VersaoDoTermo? vigente) = await termos.ObterParaPublicarAsync();
        VersaoDoTermo nova = termo.Publicar(ConteudoDoTermo.Criar(Texto(marca), Ato).Value!, Publicador, vigente, vigente?.Id, DateTimeOffset.UtcNow).Value!;
        await termos.AdicionarVersaoAsync(nova);
        await contexto.SaveChangesAsync();
        await transacao.CommitAsync();

        return nova;
    }

    /// <summary>A versão que o pedido fixou na criação; nula no pedido sem termo.</summary>
    public static async Task<Guid?> FixadaAsync(IServiceProvider servicos, Guid pedidoId)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();

        return await escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>().Pedidos.AsNoTracking()
            .Where(p => p.Id == pedidoId).Select(p => p.TermoVersaoId).SingleOrDefaultAsync();
    }

    /// <summary>O beneficiário aceita o pedido apresentando a versão do termo que ele fixou, se fixou.</summary>
    public static async Task<HttpResponseMessage> AceitarAsync(IServiceProvider servicos, HttpClient beneficiario, Guid pedidoId, string? chave = null) =>
        await PedidosDeConcessaoTestBase.EnviarAsync(
            beneficiario, HttpMethod.Post, $"{PedidosDeConcessaoTestBase.Rota}/{pedidoId}/aceite",
            new { termoVersaoApresentadaId = await FixadaAsync(servicos, pedidoId) }, chave);

    /// <summary>A vigente, que a tela apresentaria, quando o perfil exige o termo; nula no perfil que não o exige.</summary>
    public static async Task<Guid?> ParaApresentarAsync(IServiceProvider servicos, Guid perfilId)
    {
        await using AsyncServiceScope escopo = servicos.CreateAsyncScope();
        Perfil? perfil = await escopo.ServiceProvider.GetRequiredService<IPerfilRepository>().ObterPorIdAsync(perfilId);
        bool exigido = perfil is not null
            && PedidoDeConcessao.ExigeTermo(perfil, escopo.ServiceProvider.GetRequiredService<ICatalogoDePermissoesConsultavel>());

        return exigido ? (await escopo.ServiceProvider.GetRequiredService<ITermoDeResponsabilidadeRepository>().ObterVigenteAsync())?.Id : null;
    }
}
