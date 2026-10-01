namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O leitor cross-módulo dos modelos de formulário (UNI-REQ-0144): a escolha do processo vê só os
/// modelos ativos da finalidade que servem ao tipo dele, os do próprio tipo e os de todos os tipos;
/// o modelo pedido pelo identificador vem com o estado, ativo ou não.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ModeloFormularioReaderTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public ModeloFormularioReaderTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A escolha traz os modelos ativos da finalidade que servem ao tipo, inclusive os de todos os tipos, por código")]
    public async Task ListarAtivos_FiltraPorTipoFinalidadeEEstado()
    {
        string tipo = $"T{Guid.NewGuid():N}"[..20];
        ModeloFormulario doTipo = Novo("B", tipo);
        ModeloFormulario deTodos = Novo("A", tipoProcessoCodigo: null);
        ModeloFormulario desativado = Novo("C", tipo);
        ModeloFormulario deOutroTipo = Novo("D", "OUTRO_TIPO");
        desativado.Desativar().IsSuccess.Should().BeTrue();

        using IServiceScope escopo = _fixture.Factory.Services.CreateScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        db.ModelosFormulario.AddRange(doTipo, deTodos, desativado, deOutroTipo);
        await db.SaveChangesAsync();
        IModeloFormularioReader reader = escopo.ServiceProvider.GetRequiredService<IModeloFormularioReader>();

        IReadOnlyList<ModeloFormularioView> habilitacao = await reader.ListarAtivosAsync($" {tipo} ", "HABILITACAO");
        IReadOnlyList<ModeloFormularioView> inscricao = await reader.ListarAtivosAsync(tipo, "INSCRICAO");
        ModeloFormularioView? lidoDesativado = await reader.ObterAsync(desativado.Id);
        IReadOnlyList<ModeloFormularioView> tipoNaoGravavel = await reader.ListarAtivosAsync("PSR\u0000", "HABILITACAO");

        Guid[] ids = [.. habilitacao.Select(static m => m.Id)];
        ids.Should().Contain([doTipo.Id, deTodos.Id]).And.NotContain([desativado.Id, deOutroTipo.Id]);
        habilitacao.Select(static m => m.Codigo).Should().BeInAscendingOrder(StringComparer.Ordinal);
        inscricao.Select(static m => m.Id).Should().NotContain(doTipo.Id);
        tipoNaoGravavel.Should().BeEmpty();
        lidoDesativado!.Ativo.Should().BeFalse();
        lidoDesativado.Conteudo.Itens!.Single().FatoCodigo.Should().Be("CERTIFICADO");
    }

    private static ModeloFormulario Novo(string prefixo, string? tipoProcessoCodigo)
    {
        ItemDoModelo item = new("CERTIFICADO", 0, "DADOS", "Certificado", TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Sempre, null, [], false);
        return ModeloFormulario.Criar(
            $"{prefixo}_{Guid.NewGuid():N}"[..30], "Habilitação", null, FinalidadeFormulario.Habilitacao, tipoProcessoCodigo,
            new ConteudoDoModelo(
                "Habilitação",
                [
                    new("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, null),
                    new("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null),
                ],
                [item],
                [],
                []),
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)).Value!;
    }
}
