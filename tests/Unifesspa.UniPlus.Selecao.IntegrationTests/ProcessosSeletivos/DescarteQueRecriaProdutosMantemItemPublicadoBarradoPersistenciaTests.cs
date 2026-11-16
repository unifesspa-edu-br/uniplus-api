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
        Guid processoId;
        Guid versaoId;
        Guid produtoPreliminarId;
        Guid produtoFinalId;
        Guid documentoId;
        VersaoConfiguracao versao;
        Guid faseInscricaoOrigem = Guid.CreateVersion7();
        Guid fasePreliminarOrigem = Guid.CreateVersion7();
        Guid faseFinalOrigem = Guid.CreateVersion7();

        await using (UniPlusDbContext db = _fixture.CreateDbContext())
        {
            ProcessoSeletivo processo = NovoProcesso($"PS item publicado {Guid.CreateVersion7()}");
            processo.DefinirCronogramaFases(
                [
                    Fase(1, "INSCRICAO", faseInscricaoOrigem, []),
                    Fase(2, "RESULTADO_PRELIMINAR", fasePreliminarOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)],
                        faseConcluinteCodigo: "RESULTADO_FINAL"),
                    Fase(3, "RESULTADO_FINAL", faseFinalOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)]),
                ],
                [],
                PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

            Result<VersaoConfiguracao> publicacao = Publicar(processo);
            publicacao.IsSuccess.Should().BeTrue(publicacao.Error?.Message);
            processo.ClearDomainEvents();
            versao = publicacao.Value!;

            await db.ProcessosSeletivos.AddAsync(processo);
            await db.AddAsync(versao);
            DocumentoAPublicar documento = Documento(processo.Id);
            await db.AddAsync(documento);
            await db.SaveChangesAsync();

            ProdutoDaFase preliminar = ProdutoDe(processo, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar);
            AtoDoProcesso publicado = AtoDoItem(
                processo.Id, documento.Id, IdentidadeDoItemDoEdital.DaFase(
                    processo.CronogramaFases.Single(f => f.Codigo == "RESULTADO_PRELIMINAR"), preliminar));
            await db.AddAsync(publicado);
            await db.SaveChangesAsync();

            processoId = processo.Id;
            versaoId = versao.Id;
            produtoPreliminarId = preliminar.Id;
            produtoFinalId = ProdutoDe(processo, "RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo).Id;
            documentoId = documento.Id;
        }

        await using (UniPlusDbContext sessao = _fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(sessao, processoId);
            tracked.AbrirRetificacao("Insere a homologação no meio do cronograma", versao, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(tracked), "teste", Agora)
                .IsSuccess.Should().BeTrue();
            tracked.DefinirCronogramaFases(
                [
                    Fase(1, "INSCRICAO", faseInscricaoOrigem, []),
                    Fase(2, "HOMOLOGACAO", Guid.CreateVersion7(),
                        [ProdutoDaFase.Criar("HOMOLOGACAO_INSCRICOES", PapelProdutoFase.Definitivo)]),
                    Fase(3, "RESULTADO_PRELIMINAR", fasePreliminarOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Preliminar)],
                        faseConcluinteCodigo: "RESULTADO_FINAL"),
                    Fase(4, "RESULTADO_FINAL", faseFinalOrigem,
                        [ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelProdutoFase.Definitivo)]),
                ],
                [],
                PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await sessao.SaveChangesAsync();
        }

        await using (UniPlusDbContext descarte = _fixture.CreateDbContext())
        {
            ProcessoSeletivo tracked = await CarregarAsync(descarte, processoId);
            VersaoConfiguracao versaoDoBanco = await descarte.Set<VersaoConfiguracao>().FirstAsync(v => v.Id == versaoId);
            tracked.RestaurarConfiguracaoCongelada(
                versaoDoBanco,
                GrafoCongelado(faseInscricaoOrigem, fasePreliminarOrigem, faseFinalOrigem, produtoPreliminarId, produtoFinalId))
                .IsSuccess.Should().BeTrue();
            tracked.DescartarRetificacao(PrecondicaoIfMatch.Curinga).IsSuccess.Should().BeTrue();
            await descarte.SaveChangesAsync();
        }

        await using UniPlusDbContext verificacao = _fixture.CreateDbContext();
        ProcessoSeletivo reposto = await CarregarAsync(verificacao, processoId);
        FaseCronograma fasePreliminar = reposto.CronogramaFases.Single(f => f.Codigo == "RESULTADO_PRELIMINAR");
        ProdutoDaFase recriado = fasePreliminar.Produtos.Single();
        recriado.Id.Should().NotBe(produtoPreliminarId, "pré-condição: o descarte sobre a fase deslocada recria o produto com identidade nova");

        AtoDoProcesso segunda = AtoDoItem(processoId, documentoId, IdentidadeDoItemDoEdital.DaFase(fasePreliminar, recriado));
        await verificacao.AddAsync(segunda);
        Func<Task> salvar = async () => await verificacao.SaveChangesAsync();

        (await salvar.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException!.Message.Should().Contain("ux_atos_do_processo_item_original");
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
