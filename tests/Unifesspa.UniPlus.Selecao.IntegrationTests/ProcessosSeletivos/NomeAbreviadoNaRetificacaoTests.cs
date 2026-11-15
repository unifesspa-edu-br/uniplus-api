namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// A regra de abreviação de nome é DERIVADA no congelamento, não uma configuração viva. A matriz
/// precisa das três direções: publicação, fechamento e descarte não podem ser a mesma prova
/// disfarçada de três formas.
/// </summary>
[Collection(RetificacaoPersistenciaColecao.Nome)]
public sealed class NomeAbreviadoNaRetificacaoTests
{
    private readonly CenarioDeRetificacaoPersistida _cenario;

    public NomeAbreviadoNaRetificacaoTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeRetificacaoPersistida(fixture);
    }

    private static ProcessoSeletivo NovoProcessoComNomeAbreviadoNaDivulgacao(string nome)
    {
        ProcessoSeletivo processo = CenarioDeRetificacaoPersistida.NovoProcessoConforme(nome);
        processo.DefinirConfiguracaoDivulgacao(
            ConfiguracaoDivulgacao.Criar(["numero_inscricao", "nome_abreviado"], null).Value!,
            PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        return processo;
    }

    [Fact(DisplayName =
        "issue #563: publicar com nome_abreviado congela a regra vigente; fechar a retificação após removê-lo grava regraNomeAbreviado null na versão nova, sem tocar a versão anterior")]
    public async Task Retificacao_ComRemocaoDeNomeAbreviado_FechamentoGravaRegraNula()
    {
        string nome = nameof(Retificacao_ComRemocaoDeNomeAbreviado_FechamentoGravaRegraNula);
        ProcessoSeletivo processo = NovoProcessoComNomeAbreviadoNaDivulgacao(nome);
        VersaoConfiguracao versaoAbertura = await _cenario.PublicarEPersistirAsync(processo);

        JsonObject divulgacaoNaAbertura = JsonNode.Parse(versaoAbertura.ConfiguracaoCongelada)!["divulgacao"]!.AsObject();
        divulgacaoNaAbertura["regraNomeAbreviado"]!.GetValue<string>().Should().Be(
            RegrasDeNomeAbreviado.Vigente, "publicar com nome_abreviado no conjunto congela o identificador da regra vigente");

        VersaoConfiguracao versaoRetificacao;
        await using (UniPlusDbContext sessao = _cenario.CriarDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(sessao);
            ProcessoSeletivo carregado = (await repository.ObterParaMutacaoAsync(processo.Id, CancellationToken.None))!;

            CenarioDeRetificacaoPersistida.AbrirRetificacao(carregado, "Remover a divulgação do nome abreviado", versaoAbertura);

            // A EDIÇÃO durante a sessão: remove nome_abreviado — só o piso sobrevive.
            carregado.DefinirConfiguracaoDivulgacao(
                ConfiguracaoDivulgacao.Criar(["numero_inscricao"], null).Value!, PrecondicaoIfMatch.Curinga)
                .IsSuccess.Should().BeTrue();

            versaoRetificacao = await CenarioDeRetificacaoPersistida.FecharRetificacaoAsync(sessao, repository, carregado, versaoAbertura);
        }

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        VersaoConfiguracao aberturaLida = await CenarioDeRetificacaoPersistida.LerVersaoAsync(readContext, versaoAbertura.Id);
        VersaoConfiguracao retificacaoLida = await CenarioDeRetificacaoPersistida.LerVersaoAsync(readContext, versaoRetificacao.Id);

        // A versão N permanece byte-a-byte intacta — a edição da sessão nunca a alcança.
        aberturaLida.ConfiguracaoCongeladaCanonica.Should().Equal(versaoAbertura.ConfiguracaoCongeladaCanonica);

        JsonObject divulgacaoNaRetificacao = JsonNode.Parse(retificacaoLida.ConfiguracaoCongelada)!["divulgacao"]!.AsObject();
        divulgacaoNaRetificacao["camposPublicos"]!.AsArray().Select(static n => n!.GetValue<string>())
            .Should().Equal("numero_inscricao");
        divulgacaoNaRetificacao["regraNomeAbreviado"].Should().BeNull(
            "o fechamento congela a configuração viva EDITADA (sem nome_abreviado) — a regra derivada tem de zerar " +
            "junto, não carregar o valor da versão anterior");
    }

    [Fact(DisplayName =
        "issue #563: descartar a retificação após remover nome_abreviado restaura o conjunto abreviado e recodifica com o identificador congelado")]
    public async Task Retificacao_DescartadaAposRemoverNomeAbreviado_RestauraConjuntoERegraCongelados()
    {
        string nome = nameof(Retificacao_DescartadaAposRemoverNomeAbreviado_RestauraConjuntoERegraCongelados);
        ProcessoSeletivo processo = NovoProcessoComNomeAbreviadoNaDivulgacao(nome);
        VersaoConfiguracao versaoAbertura = await _cenario.PublicarEPersistirAsync(processo);

        Guid processoId = processo.Id;
        await using (UniPlusDbContext sessao = _cenario.CriarDbContext())
        {
            ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(sessao);
            ProcessoSeletivo tracked = (await repository.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

            CenarioDeRetificacaoPersistida.AbrirRetificacao(tracked, "Testar edição e descarte da divulgação", versaoAbertura);

            tracked.DefinirConfiguracaoDivulgacao(
                ConfiguracaoDivulgacao.Criar(["numero_inscricao"], null).Value!, PrecondicaoIfMatch.Curinga)
                .IsSuccess.Should().BeTrue();
            tracked.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal(
                ["numero_inscricao"], "pré-condição: a sessão editorial removeu nome_abreviado");

            // Se a regra derivada não reproduzisse 'iniciais_mais_ultimo_sobrenome' para o
            // conjunto restaurado, a prova de fidelidade falharia aqui — antes de qualquer escrita.
            await CenarioDeRetificacaoPersistida.DescartarComProvaAsync(sessao, tracked, versaoAbertura);
        }

        await using UniPlusDbContext readContext = _cenario.CriarDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(readContext);
        ProcessoSeletivo relido = (await leitura.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;

        relido.Rascunho.Should().BeNull("a sessão foi descartada — não há mais retificação em curso");
        relido.ConfiguracaoDivulgacao.Should().NotBeNull();
        relido.ConfiguracaoDivulgacao!.CamposPublicos.Should().Equal(
            ["nome_abreviado", "numero_inscricao"],
            "o descarte restaurou o conjunto AMPLIADO que a versão N congelou — não o conjunto reduzido editado na sessão abandonada");

        List<VersaoConfiguracao> versoes = await readContext.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId).ToListAsync(CancellationToken.None);
        versoes.Should().ContainSingle("descartar não cria versão nova — só a abertura persiste");
    }
}
