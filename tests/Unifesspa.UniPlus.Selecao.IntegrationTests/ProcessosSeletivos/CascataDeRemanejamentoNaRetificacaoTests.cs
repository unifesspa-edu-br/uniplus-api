namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// A cascata de remanejamento sob a sessão editorial de retificação: o fechamento congela a cascata
/// editada na versão nova sem tocar a anterior, e o descarte restaura a cascata da versão anterior.
/// </summary>
[Collection(RetificacaoPersistenciaColecao.Nome)]
public sealed class CascataDeRemanejamentoNaRetificacaoTests
{
    private readonly CenarioDeRetificacaoPersistida _cenario;

    public CascataDeRemanejamentoNaRetificacaoTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeRetificacaoPersistida(fixture);
    }

    /// <summary>
    /// Uma cascata VÁLIDA (cobre as 8 origens SegueCascata de
    /// <c>NovoProcessoComOfertaFederalECascata</c>), mas com forma DIFERENTE da matriz legal
    /// completa (8×7) semeada por ela: um único destino por origem, num mapeamento
    /// LB↔LI diferente. É a "edição durante a sessão" que os dois testes abaixo exercitam —
    /// distinguível da versão original tanto pela contagem de destinos quanto pelo conteúdo.
    /// </summary>
    private static ConfiguracaoCascataRemanejamento CascataDeUmDestinoPorOrigem()
    {
        (string Origem, string Destino)[] pares =
        [
            ("LB_PPI", "LB_Q"), ("LB_Q", "LB_PPI"), ("LB_PCD", "LI_PCD"), ("LB_EP", "LI_EP"),
            ("LI_PPI", "LB_PPI"), ("LI_Q", "LB_Q"), ("LI_PCD", "LB_PCD"), ("LI_EP", "LB_EP"),
        ];
        List<DestinoRemanejamento> destinos = [.. pares.Select(p => DestinoRemanejamento.Criar(p.Origem, 1, p.Destino).Value!)];

        return ConfiguracaoCascataRemanejamento.Criar(
            ReferenciaRegra.Criar(RegraRemanejamentoCodigo.Cascata, "v1", new string('9', 64)).Value!,
            "AC",
            destinos).Value!;
    }

    [Fact(DisplayName = "Abrir retificação, editar a cascata de remanejamento e fechar gera a versão N+1 com a cascata nova — a versão N permanece intacta")]
    public async Task Retificacao_ComEdicaoDeCascataDuranteASessao_FechamentoGeraVersaoComCascataNova()
    {
        string nome = nameof(Retificacao_ComEdicaoDeCascataDuranteASessao_FechamentoGeraVersaoComCascataNova);
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoComOfertaFederalECascata(nome);
        VersaoConfiguracao versaoAbertura = await _cenario.PublicarEPersistirAsync(processo);

        VersaoConfiguracao versaoRetificacao;
        await using (UniPlusDbContext sessao = _cenario.CriarDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(sessao);
            ProcessoSeletivo carregado = (await repository.ObterParaMutacaoAsync(processo.Id, CancellationToken.None))!;

            CenarioDeRetificacaoPersistida.AbrirRetificacao(carregado, "Corrigir a ordem legal de remanejamento", versaoAbertura);

            // A EDIÇÃO durante a sessão — o que este teste prova: DefinirCascataRemanejamento
            // escreve DIRETO na configuração viva, sem staging, como qualquer outro Definir*.
            carregado.DefinirCascataRemanejamento(CascataDeUmDestinoPorOrigem(), PrecondicaoIfMatch.Curinga)
                .IsSuccess.Should().BeTrue("mutar a configuração viva durante a sessão é permitido");

            versaoRetificacao = await CenarioDeRetificacaoPersistida.FecharRetificacaoAsync(sessao, repository, carregado, versaoAbertura);
        }

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        VersaoConfiguracao aberturaLida = await CenarioDeRetificacaoPersistida.LerVersaoAsync(readContext, versaoAbertura.Id);
        VersaoConfiguracao retificacaoLida = await CenarioDeRetificacaoPersistida.LerVersaoAsync(readContext, versaoRetificacao.Id);

        // A versão N permanece byte-a-byte intacta (append-only) — a edição da sessão nunca a alcança.
        aberturaLida.HashConfiguracao.Should().Be(versaoAbertura.HashConfiguracao);
        aberturaLida.ConfiguracaoCongeladaCanonica.Should().Equal(versaoAbertura.ConfiguracaoCongeladaCanonica);

        retificacaoLida.NumeroVersao.Should().Be(2);
        retificacaoLida.AtoCriadorRetificaId.Should().Be(versaoAbertura.AtoCriadorId);

        JsonObject cascataNaAbertura = JsonNode.Parse(aberturaLida.ConfiguracaoCongelada)!["cascataRemanejamento"]!.AsObject();
        JsonObject cascataNaRetificacao = JsonNode.Parse(retificacaoLida.ConfiguracaoCongelada)!["cascataRemanejamento"]!.AsObject();

        cascataNaAbertura["ordens"]![0]!["destinos"]!.AsArray().Should().HaveCount(7,
            "a versão N congelou a matriz legal completa (8×7) semeada por NovoProcessoComOfertaFederalECascata");
        cascataNaRetificacao["ordens"]![0]!["destinos"]!.AsArray().Should().HaveCount(1,
            "a versão N+1 congela a cascata EDITADA durante a sessão — um destino por origem, não a matriz original");
    }

    [Fact(DisplayName = "Descartar a retificação após editar a cascata restaura exatamente a cascata da versão anterior")]
    public async Task Retificacao_DescartadaAposEditarCascata_RestauraCascataDaVersaoAnterior()
    {
        string nome = nameof(Retificacao_DescartadaAposEditarCascata_RestauraCascataDaVersaoAnterior);
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoComOfertaFederalECascata(nome);
        VersaoConfiguracao versaoAbertura = await _cenario.PublicarEPersistirAsync(processo);

        Guid processoId = processo.Id;
        await using (UniPlusDbContext sessao = _cenario.CriarDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(sessao);
            ProcessoSeletivo tracked = (await repository.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

            CenarioDeRetificacaoPersistida.AbrirRetificacao(tracked, "Testar edição e descarte da cascata", versaoAbertura);

            tracked.DefinirCascataRemanejamento(CascataDeUmDestinoPorOrigem(), PrecondicaoIfMatch.Curinga)
                .IsSuccess.Should().BeTrue();
            tracked.Cascata!.Destinos.Should().HaveCount(8,
                "pré-condição: a sessão editorial trocou a matriz legal completa (56 destinos) por um destino por origem (8)");

            await CenarioDeRetificacaoPersistida.DescartarComProvaAsync(sessao, tracked, versaoAbertura);
        }

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relido = (await leitura.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

        relido.Status.Should().Be(StatusProcesso.Publicado);
        relido.Rascunho.Should().BeNull("a sessão foi descartada — não há mais retificação em curso");
        relido.Cascata.Should().NotBeNull();
        relido.Cascata!.Destinos.Should().HaveCount(56,
            "o descarte restaurou a matriz legal completa que a versão N congelou — não os 8 destinos da edição abandonada");
        relido.Cascata.FallbackCodigo.Should().Be("AC");

        List<VersaoConfiguracao> versoes = await readContext.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId).ToListAsync(CancellationToken.None);
        versoes.Should().ContainSingle("descartar não cria versão nova — só a abertura persiste");
    }
}
