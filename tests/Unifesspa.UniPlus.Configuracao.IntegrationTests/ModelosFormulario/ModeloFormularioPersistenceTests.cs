namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.ModelosFormulario;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O modelo de formulário contra Postgres real: o conteúdo volta do documento jsonb igual ao que foi
/// gravado, com as regras do item, da seção e do termo, e o código é único entre todos os modelos.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
public sealed class ModeloFormularioPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public ModeloFormularioPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static PredicadoDnf Quando(string fato, string valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;

    private static ModeloFormulario Novo(string codigo)
    {
        ConteudoDoModelo conteudo = new(
            "Habilitação",
            [
                new("ESCOLA", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Escolaridade", "Como concluiu", null, Quando("FORMA_CONCLUSAO", "REGULAR")),
                new("DOCUMENTOS", 1, TipoEtapaFormulario.Bloco, BlocoSistema.ComprovacaoDocumental, "Documentos", null, null, null),
                new("REVISAO", 2, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null),
            ],
            [
                new("CERTIFICADO_EMITIDO", 0, "ESCOLA", "Certificado emitido?", TipoRenderizacao.Booleano, null, "Responda pelo histórico",
                    Obrigatoriedade.Sempre, null, [], PedirConfirmacao: true),
                new("TIPO_DOCUMENTO", 1, "ESCOLA", "Tipo de documento", TipoRenderizacao.SelecaoUnica, null, null,
                    Obrigatoriedade.Quando(Quando("FORMA_CONCLUSAO", "REGULAR")), null,
                    [RestricoesDeValor.Opcoes([(Quando("FORMA_CONCLUSAO", "REGULAR"), ["HISTORICO"])]).Value!], PedirConfirmacao: false),
            ],
            [new("VERACIDADE", 0, Guid.CreateVersion7(), Guid.CreateVersion7(), Quando("FORMA_CONCLUSAO", "REGULAR"), Obrigatoriedade.Sempre)],
            ["FORMA_CONCLUSAO"]);
        Result<ModeloFormulario> modelo = ModeloFormulario.Criar(
            codigo, "Habilitação Medicina 2027", "Modelo do PSR", FinalidadeFormulario.Habilitacao, "PSR", conteudo,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal));
        modelo.IsSuccess.Should().BeTrue(modelo.Error?.Message);
        return modelo.Value!;
    }

    [Fact(DisplayName = "O conteúdo do modelo volta do documento jsonb igual ao gravado")]
    public async Task Conteudo_IdaEVolta()
    {
        ModeloFormulario modelo = Novo($"HAB_{Guid.NewGuid():N}"[..30]);
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext("admin-a"))
        {
            ctx.ModelosFormulario.Add(modelo);
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext leitura = _fixture.CreateDbContext(userId: null);
        ModeloFormulario lido = await leitura.ModelosFormulario.SingleAsync(m => m.Id == modelo.Id);

        lido.Finalidade.Should().Be(FinalidadeFormulario.Habilitacao);
        lido.TipoProcessoCodigo.Should().Be("PSR");
        lido.Conteudo.Should().BeEquivalentTo(modelo.Conteudo, opcoes => opcoes.ComparingByMembers<JsonElement>()
            .Using<JsonElement>(c => c.Subject.GetRawText().Should().Be(c.Expectation.GetRawText())).WhenTypeIs<JsonElement>());
    }

    [Fact(DisplayName = "O código do modelo é único entre todos os modelos")]
    public async Task CodigoRepetido_Recusado()
    {
        string codigo = $"HAB_{Guid.NewGuid():N}"[..30];
        await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext("admin-a"))
        {
            ctx.ModelosFormulario.Add(Novo(codigo));
            await ctx.SaveChangesAsync();
        }

        await using ConfiguracaoDbContext segundo = _fixture.CreateDbContext("admin-a");
        segundo.ModelosFormulario.Add(Novo(codigo));
        Func<Task> gravar = async () => await segundo.SaveChangesAsync();

        DbUpdateException ex = (await gravar.Should().ThrowAsync<DbUpdateException>()).Which;
        ex.InnerException.Should().BeOfType<Npgsql.PostgresException>().Which.ConstraintName.Should().Be("ix_modelos_formulario_codigo");
    }
}
