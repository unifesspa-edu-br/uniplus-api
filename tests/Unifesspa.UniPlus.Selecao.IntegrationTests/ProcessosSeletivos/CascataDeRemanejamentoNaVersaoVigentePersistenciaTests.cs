namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CenarioDeRetificacaoPersistida;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CenarioDeVersaoVigentePersistida;

/// <summary>
/// A cascata de remanejamento segue a mesma regra de vigência das demais dimensões: o seletor
/// devolve a cascata congelada na versão do instante, não a mais recente (ADR-0075/0076).
/// </summary>
[Collection(VersaoVigentePersistenciaColecao.Nome)]
public sealed class CascataDeRemanejamentoNaVersaoVigentePersistenciaTests
{
    private readonly CenarioDeVersaoVigentePersistida _cenario;

    public CascataDeRemanejamentoNaVersaoVigentePersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeVersaoVigentePersistida(fixture);
    }

    /// <summary>Mesma forma de <c>CascataDeRemanejamentoNaRetificacaoTests.CascataDeUmDestinoPorOrigem</c> — uma cascata VÁLIDA, distinguível pela contagem de destinos.</summary>
    private static ConfiguracaoCascataRemanejamento CascataDeUmDestinoPorOrigem()
    {
        (string Origem, string Destino)[] pares =
        [
            ("LB_PPI", "LB_Q"), ("LB_Q", "LB_PPI"), ("LB_PCD", "LI_PCD"), ("LB_EP", "LI_EP"),
            ("LI_PPI", "LB_PPI"), ("LI_Q", "LB_Q"), ("LI_PCD", "LB_PCD"), ("LI_EP", "LB_EP"),
        ];
        List<DestinoRemanejamento> destinos = [.. pares.Select(p => DestinoRemanejamento.Criar(p.Origem, 1, p.Destino).Value!)];

        return ConfiguracaoCascataRemanejamento.Criar(
            ReferenciaRegra.De(RegraRemanejamentoCodigo.Cascata, "v1", new string('9', 64)),
            "AC",
            destinos).Value!;
    }

    [Fact(DisplayName = "Seletor resolve a cascata vigente no instante certo — mesma regra das demais dimensões (ADR-0075/0076), sem caminho especial")]
    public async Task ObterVersaoVigente_ResolveACascataVigenteNoInstanteCerto()
    {
        RelogioManualDaVigencia clock = new(T0);
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoComOfertaFederalECascata(
            nameof(ObterVersaoVigente_ResolveACascataVigenteNoInstanteCerto));
        await _cenario.PublicarEPersistirAsync(processo, clock);

        clock.Avancar(TimeSpan.FromDays(1));
        await RetificarComCascataEditadaAsync(processo.Id, clock);

        await using UniPlusDbContext context = _cenario.CriarDbContext();
        ProcessoSeletivoRepository leitura = ProcessoSeletivoRepositoryDeTeste.Criar(context);

        // No instante T0 (antes da retificação) a vigente é a versão 1 — a matriz legal completa (8×7).
        VersaoConfiguracao? vigenteEmT0 = await leitura.ObterVersaoVigenteAsync(processo.Id, T0, CancellationToken.None);
        vigenteEmT0.Should().NotBeNull();
        DestinosDaPrimeiraOrigem(vigenteEmT0!).Should().HaveCount(7,
            "no instante T0 a vigente é a versão 1, com a matriz legal completa semeada por NovoProcessoComOfertaFederalECascata");

        // Depois da retificação (T0+2d) a vigente é a versão 2 — a cascata EDITADA.
        VersaoConfiguracao? vigenteApos = await leitura.ObterVersaoVigenteAsync(processo.Id, T0.AddDays(2), CancellationToken.None);
        vigenteApos.Should().NotBeNull();
        DestinosDaPrimeiraOrigem(vigenteApos!).Should().HaveCount(1,
            "depois da retificação a vigente é a versão 2, com a cascata editada — um destino por origem");

        // Antes da primeira publicação, nenhuma versão vige — a cascata não tem caminho
        // especial: segue a MESMA regra de ausência que as demais dimensões (ADR-0076).
        VersaoConfiguracao? antesDaPrimeiraPublicacao = await leitura
            .ObterVersaoVigenteAsync(processo.Id, T0.AddSeconds(-1), CancellationToken.None);
        antesDaPrimeiraPublicacao.Should().BeNull(
            "antes da primeira publicação não há configuração vigente — vale para a cascata como vale para " +
            "qualquer outra dimensão, sem gate especial no seletor");
    }

    private static JsonArray DestinosDaPrimeiraOrigem(VersaoConfiguracao versao) =>
        JsonNode.Parse(versao.ConfiguracaoCongelada)!["cascataRemanejamento"]!["ordens"]![0]!["destinos"]!.AsArray();

    /// <summary>
    /// A cascata MUDA entre as duas versões — é o que o teste prova: o seletor de vigente devolve a
    /// cascata do INSTANTE, não sempre a mais recente. Mudar a configuração VIVA de um processo
    /// publicado exige a sessão editorial — DefinirCascataRemanejamento recusa
    /// (MutacaoPosPublicacaoBloqueada) sem um Rascunho aberto, mesmo com PrecondicaoIfMatch.Curinga;
    /// o atalho atômico Retificar só re-canonicaliza a configuração como está, sem editá-la.
    /// </summary>
    private async Task RetificarComCascataEditadaAsync(Guid processoId, TimeProvider clock)
    {
        await using UniPlusDbContext escrita = _cenario.CriarDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(escrita);
        ProcessoSeletivo carregado = (await repository.ObterParaMutacaoAsync(processoId, CancellationToken.None))!;
        VersaoConfiguracao versaoAtual = (await repository.ObterVersaoAtualAsync(processoId, CancellationToken.None))!;

        AbrirSessaoEditandoACascata(carregado, versaoAtual, clock);
        DocumentoAPublicar documento = DocumentoConfirmado(processoId);
        VersaoConfiguracao fechada = FecharSessao(carregado, versaoAtual, documento, clock);

        await escrita.DocumentosAPublicar.AddAsync(documento, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(fechada, CancellationToken.None);
        await escrita.SaveChangesAsync(CancellationToken.None);
    }

    private static void AbrirSessaoEditandoACascata(ProcessoSeletivo carregado, VersaoConfiguracao versaoAtual, TimeProvider clock)
    {
        Result<RascunhoRetificacao> abertura = carregado.AbrirRetificacao(
            "Correção da ordem legal de remanejamento", versaoAtual, identificadorDaVersaoBase: null, VersoesPublicadasDeTeste.SoAVigente(carregado), "integration-test-user", clock.GetUtcNow());
        abertura.IsSuccess.Should().BeTrue(abertura.Error?.Message);

        carregado.DefinirCascataRemanejamento(CascataDeUmDestinoPorOrigem(), PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
    }

    private static VersaoConfiguracao FecharSessao(
        ProcessoSeletivo carregado, VersaoConfiguracao versaoAtual, DocumentoAPublicar documento, TimeProvider clock)
    {
        DadosEdital dados = NovosDados(documento.Id);
        SnapshotCanonico canonico = Canonicalizar(carregado, dados, documento);

        Result<VersaoConfiguracao> fechar = carregado.FecharRetificacao(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(canonico.Bytes, canonico.SchemaVersion, canonico.AlgoritmoHash, documento.HashSha256!), AtorUsuarioContaId = "integration-test-user", Relogio = clock, Contagem = CorpusEnvelopeDadosDoEdital.ContextoRico() }, versaoAtual, PrecondicaoIfMatch.Curinga);
        fechar.IsSuccess.Should().BeTrue(fechar.Error?.Message);
        return fechar.Value!;
    }
}
