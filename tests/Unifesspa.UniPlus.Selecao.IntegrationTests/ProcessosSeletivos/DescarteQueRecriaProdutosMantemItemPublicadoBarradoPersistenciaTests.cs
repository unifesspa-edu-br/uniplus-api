namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoDescarteAposDeslocamentoDeOrdem;

/// <remarks>Contra Postgres real: o estrago do descarte é do identity map do EF e das linhas de produtos da fase.</remarks>
public sealed class DescarteQueRecriaProdutosMantemItemPublicadoBarradoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private readonly ProcessoSeletivoDbFixture _fixture;

    public DescarteQueRecriaProdutosMantemItemPublicadoBarradoPersistenciaTests(ProcessoSeletivoDbFixture fixture) =>
        _fixture = fixture;

    [Fact(DisplayName = "Depois do descarte que recria os produtos, o item já publicado continua barrado para uma segunda publicação original")]
    public async Task Descarte_RecriaOsProdutos_ItemJaPublicadoContinuaBarrado()
    {
        CenarioDoDeslocamentoDeOrdem cenario = new(_fixture);
        await cenario.PublicarAsync("PS item publicado");
        Guid documentoId = await PublicarOItemPreliminarAsync(cenario);
        await cenario.InserirHomologacaoNoMeioAsync();
        await cenario.DescartarAsync();

        await ConferirQueOItemContinuaBarradoAsync(cenario, documentoId);
    }

    private async Task ConferirQueOItemContinuaBarradoAsync(CenarioDoDeslocamentoDeOrdem cenario, Guid documentoId)
    {
        await using UniPlusDbContext verificacao = _fixture.CreateDbContext();
        ProcessoSeletivo reposto = await CarregarAsync(verificacao, cenario.ProcessoId);
        FaseCronograma fasePreliminar = reposto.CronogramaFases.Single(f => f.Codigo == "RESULTADO_PRELIMINAR");
        ProdutoDaFase recriado = fasePreliminar.Produtos.Single();
        recriado.Id.Should().NotBe(cenario.ProdutoPreliminarId, "pré-condição: o descarte sobre a fase deslocada recria o produto com identidade nova");

        await verificacao.AddAsync(AtoDoItem(cenario.ProcessoId, documentoId, IdentidadeDoItemDoEdital.DaFase(fasePreliminar, recriado)));
        Func<Task> salvar = async () => await verificacao.SaveChangesAsync();

        (await salvar.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException!.Message.Should().Contain("ux_atos_do_processo_item_original");
    }

    private async Task<Guid> PublicarOItemPreliminarAsync(CenarioDoDeslocamentoDeOrdem cenario)
    {
        await using UniPlusDbContext contexto = _fixture.CreateDbContext();
        ProcessoSeletivo processo = await CarregarAsync(contexto, cenario.ProcessoId);
        DocumentoAPublicar documento = Documento(processo.Id);
        await contexto.AddAsync(documento);
        ProdutoDaFase preliminar = ProdutoDe(processo, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
        FaseCronograma fase = processo.CronogramaFases.Single(f => f.Codigo == "RESULTADO_PRELIMINAR");
        await contexto.AddAsync(AtoDoItem(processo.Id, documento.Id, IdentidadeDoItemDoEdital.DaFase(fase, preliminar)));
        await contexto.SaveChangesAsync();
        return documento.Id;
    }

    private static DocumentoAPublicar Documento(Guid processoId)
    {
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, FinalidadeDoDocumento.DocumentoDeAto, "lista.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, new string('c', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    private static AtoDoProcesso AtoDoItem(Guid processoId, Guid documentoId, IdentidadeDoItemDoEdital item) =>
        AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                Guid.CreateVersion7(), processoId, "RESULTADO_CLASSIFICACAO", "Resultado da classificação", "1/2026", new DateOnly(2026, 1, 20),
                "Diretor do CEPS", "ceps", "ps-item-publicado", documentoId, [], ItemDoEdital: item),
            TimeProvider.System.GetUtcNow()).Value!;
}
