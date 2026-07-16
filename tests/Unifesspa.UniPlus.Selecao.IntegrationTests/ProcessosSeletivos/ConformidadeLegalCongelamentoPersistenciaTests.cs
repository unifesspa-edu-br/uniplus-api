namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// Story #853 — CA-18 (paridade congelado × cadastro vivo) e CA-19 (o passado não muda,
/// RN08), contra Postgres real (Testcontainers): a fonte é a MESMA usada pelo gate — repositório
/// real de <see cref="ObrigatoriedadeLegal"/> + <see cref="AvaliadorConformidadeLegal.Avaliar"/>.
/// </summary>
public sealed class ConformidadeLegalCongelamentoPersistenciaTests : IClassFixture<ProcessoSeletivoDbFixture>
{
    private static readonly string HashFixo = string.Concat(Enumerable.Repeat("ab01234567", 7))[..64];
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();
    private static readonly DateOnly DataDeCorte = new(2026, 1, 1);

    private readonly ProcessoSeletivoDbFixture _fixture;

    public ConformidadeLegalCongelamentoPersistenciaTests(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    private static ReferenciaRegra Regra(string codigo, string hashChar) =>
        ReferenciaRegra.Criar(codigo, "v1", new string(hashChar[0], 64)).Value!;

    private static ProcessoSeletivo NovoProcessoConforme(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(nome, TipoProcesso.SiSU, OrigemCandidatos.InscricaoPropria);

        processo.DefinirEtapas([
            EtapaProcesso.Criar("Prova Objetiva", CaraterEtapa.Classificatoria, peso: 1m, ordem: 1),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirOfertaAtendimento(
            OfertaAtendimentoEspecializado.Criar([], [], []).Value!, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ModalidadeSelecionada modalidade = ModalidadeSelecionada.Criar(
            modalidadeOrigemId: Guid.CreateVersion7(),
            codigo: "AC",
            descricao: null,
            naturezaLegal: NaturezaLegalModalidade.Ampla,
            composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
            composicaoOrigemCodigo: null,
            regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
            remanejamentoDestino: null,
            remanejamentoPar: null,
            remanejamentoFallback: null,
            criteriosCumulativos: [],
            acaoQuandoIndeferido: null,
            baseLegal: "Res. Unifesspa 532/2021").Value!;
        ConfiguracaoDistribuicaoVagas distribuicao = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: Regra(RegraDistribuicaoVagasCodigo.Institucional, "a"),
            referenciaDemografica: null,
            modalidades: [modalidade]).Value!;
        processo.DefinirDistribuicaoVagas([distribuicao], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        ConfiguracaoClassificacao classificacao = ConfiguracaoClassificacao.Criar(
            regraCalculo: Regra(RegraCalculoCodigo.ClassificacaoImportada, "b"),
            regraArredondamento: null,
            casasArredondamento: null,
            regraOrdemAlocacao: Regra(RegraOrdemAlocacaoCodigo.AlocacaoOpcoesRn04, "c"),
            nOpcoesAlocacao: 1,
            regrasEliminacao: []).Value!;
        processo.DefinirClassificacao(classificacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        FaseCronograma faseConforme = FaseCronograma.Criar(
            ordem: 1,
            faseCanonicaOrigemId: Guid.CreateVersion7(),
            codigo: "RESULTADO_FINAL",
            donoInstitucional: "CEPS",
            origemData: OrigemDataFase.Propria,
            agrupaEtapas: true,
            permiteComplementacao: false,
            produzResultado: true,
            resultadoDefinitivo: true,
            coletaInscricao: true,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            atoProduzidoCodigo: "RESULTADO_FINAL",
            atoProduzidoEfeitoIrreversivel: false,
            bancasRequeridas: [],
            regraRecurso: null).Value!;
        processo.DefinirCronogramaFases([faseConforme], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        return processo;
    }

    private async Task<Guid> SemearRegraVigenteAsync(string regraCodigo)
    {
        ObrigatoriedadeLegal regra = ObrigatoriedadeLegal.Criar(
            tipoProcessoCodigo: ObrigatoriedadeLegal.TipoProcessoUniversal,
            categoria: CategoriaObrigatoriedade.Outros,
            regraCodigo: regraCodigo,
            predicado: new EtapaObrigatoria("Prova Objetiva"),
            descricaoHumana: "Etapa objetiva obrigatória (teste de integração)",
            baseLegal: "Lei de teste",
            vigenciaInicio: new DateOnly(2020, 1, 1)).Value!;

        await using SelecaoDbContext context = _fixture.CreateDbContext();
        ObrigatoriedadeLegalRepository repository = new(context, TimeProvider.System);
        await repository.AdicionarAsync(regra, CancellationToken.None);
        await context.SaveChangesAsync(CancellationToken.None);

        return regra.Id;
    }

    private async Task<(Guid ProcessoId, Guid SnapshotId)> PublicarComConformidadeLegalAsync(string nome)
    {
        ProcessoSeletivo processo = NovoProcessoConforme(nome);

        DocumentoEdital documento = DocumentoEdital.IniciarPendente(processo.Id, TimeProvider.System, TimeSpan.FromMinutes(15));
        documento.Confirmar(1024, HashFixo, TimeProvider.System).IsSuccess.Should().BeTrue();

        Result<DadosEdital> dadosResult = DadosEdital.Criar(
            numero: "001/2026",
            periodoInscricaoInicio: DataDeCorte,
            periodoInscricaoFim: new DateOnly(2026, 1, 31),
            documentoEditalId: documento.Id);
        dadosResult.IsSuccess.Should().BeTrue();

        // Mesma dupla de chamadas do gate real (ConferenciaDeConformidadeLegal): repositório
        // real + AvaliadorConformidadeLegal.Avaliar — a Application injeta isto no handler;
        // aqui, como as demais integrações desta suíte, a orquestração é feita inline.
        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        ObrigatoriedadeLegalRepository obrigatoriedadeLegalRepository = new(readContext, TimeProvider.System);
        IReadOnlyList<ObrigatoriedadeLegal> vigentes = await obrigatoriedadeLegalRepository
            .ObterVigentesParaTipoProcessoAsync(processo.Tipo.ToString(), DataDeCorte, CancellationToken.None);
        ResultadoConformidade conformidade = AvaliadorConformidadeLegal.Avaliar(processo, processo.Tipo.ToString(), vigentes);
        conformidade.Regras.Should().OnlyContain(static r => r.Aprovada, "pré-condição do teste — o processo satisfaz a regra semeada");

        SnapshotCanonico canonico = Canonicalizer.Canonicalizar(
            new EntradaCanonicalizacao(processo, dadosResult.Value!, documento.HashSha256!, Conformidade: conformidade));

        Result<VersaoConfiguracao> publicarResult = processo.Publicar(
            dadosResult.Value!,
            canonico.Bytes,
            canonico.SchemaVersion,
            canonico.AlgoritmoHash,
            documento.HashSha256!,
            atorUsuarioSub: "integration-test-user",
            TimeProvider.System);
        publicarResult.IsSuccess.Should().BeTrue(publicarResult.Error?.Message);

        await using SelecaoDbContext writeContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = new(writeContext, TimeProvider.System);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await writeContext.DocumentosEdital.AddAsync(documento, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(publicarResult.Value!, CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);

        return (processo.Id, publicarResult.Value!.Id);
    }

    [Fact(DisplayName = "CA-13/CA-18 — obrigatoriedades[] congela TODAS as regras vigentes e bate, conjunto a conjunto, com o recômputo do cadastro vivo na mesma data de corte")]
    public async Task Congelamento_BateComRecomputoDoCadastroVivo()
    {
        // DUAS regras — não uma — para que a comparação "conjunto a conjunto" (não só um
        // lookup por id) tenha algo real a provar: nenhuma órfã no congelado, nenhuma
        // vigente de fora faltando.
        Guid regraId1 = await SemearRegraVigenteAsync(UniqueRegraCodigo());
        Guid regraId2 = await SemearRegraVigenteAsync(UniqueRegraCodigo());
        (Guid processoId, Guid snapshotId) = await PublicarComConformidadeLegalAsync(nameof(Congelamento_BateComRecomputoDoCadastroVivo));

        await using SelecaoDbContext readContext = _fixture.CreateDbContext();
        VersaoConfiguracao versao = await readContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == snapshotId, CancellationToken.None);

        JsonObject documentosExigidos = JsonNode.Parse(versao.ConfiguracaoCongelada)!["documentosExigidos"]!.AsObject();
        JsonArray obrigatoriedadesCongeladas = documentosExigidos["obrigatoriedades"]!.AsArray();
        obrigatoriedadesCongeladas.Should().HaveCount(2);
        obrigatoriedadesCongeladas.Should().OnlyContain(o => o!["aprovada"]!.GetValue<bool>());

        (Guid RegraId, string Hash)[] congeladoSet = [.. obrigatoriedadesCongeladas
            .Select(o => (o!["regraId"]!.GetValue<Guid>(), o["hash"]!.GetValue<string>()))
            .OrderBy(t => t.Item1)];

        // CA-18: recomputar a MESMA dupla de chamadas do gate (repositório vigentes + o
        // avaliador), a partir do cadastro vivo, na MESMA data de corte — não um lookup por
        // id, o conjunto inteiro — produz exatamente o mesmo (RegraId, Hash). Nenhuma
        // entrada órfã no congelado; nenhuma regra vigente de fora do congelado.
        await using SelecaoDbContext freshReadContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository processoRepository = new(freshReadContext, TimeProvider.System);
        ObrigatoriedadeLegalRepository freshRepository = new(freshReadContext, TimeProvider.System);

        ProcessoSeletivo processoVivo = (await processoRepository.ObterComConfiguracaoAsync(processoId, CancellationToken.None))!;
        IReadOnlyList<ObrigatoriedadeLegal> vigentesAgora = await freshRepository
            .ObterVigentesParaTipoProcessoAsync(processoVivo.Tipo.ToString(), DataDeCorte, CancellationToken.None);
        ResultadoConformidade recomputado = AvaliadorConformidadeLegal.Avaliar(
            processoVivo, processoVivo.Tipo.ToString(), vigentesAgora);

        (Guid RegraId, string Hash)[] recomputadoSet = [.. recomputado.Regras
            .Select(r => (r.RegraId, r.Hash))
            .OrderBy(t => t.Item1)];

        recomputadoSet.Should().Equal(congeladoSet,
            "o recômputo a partir do cadastro vivo, na mesma data de corte, tem de reproduzir exatamente o " +
            "conjunto congelado — nem uma entrada a mais (órfã), nem uma a menos (vigente de fora)");
        congeladoSet.Select(t => t.RegraId).Should().BeEquivalentTo([regraId1, regraId2]);
    }

    [Fact(DisplayName = "CA-19 (RN08 — o passado não muda) — desativar a regra depois da publicação não altera o congelado nem o hash")]
    public async Task DesativarRegraAposPublicacao_NaoAlteraOCongeladoNemOHash()
    {
        await SemearRegraVigenteAsync(UniqueRegraCodigo());
        (_, Guid snapshotId) = await PublicarComConformidadeLegalAsync(nameof(DesativarRegraAposPublicacao_NaoAlteraOCongeladoNemOHash));

        await using SelecaoDbContext antesContext = _fixture.CreateDbContext();
        VersaoConfiguracao versaoAntes = await antesContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == snapshotId, CancellationToken.None);
        string configuracaoAntes = versaoAntes.ConfiguracaoCongelada;
        string hashAntes = versaoAntes.HashConfiguracao;

        // O CEPS desativa a obrigatoriedade no cadastro — a mesma operação de sempre, sem
        // saber (nem precisar saber) que já existe um congelamento que a usou.
        await using SelecaoDbContext escritaContext = _fixture.CreateDbContext();
        await escritaContext.Database.ExecuteSqlAsync(
            $"UPDATE selecao.obrigatoriedades_legais SET is_deleted = true WHERE regra_codigo LIKE 'TEST\\_%' ESCAPE '\\'");

        // Uma reavaliação AO VIVO, na MESMA data de corte, já não veria a regra — prova que a
        // desativação teve efeito real no cadastro, e não que o teste está inerte.
        await using SelecaoDbContext posContext = _fixture.CreateDbContext();
        ObrigatoriedadeLegalRepository posRepository = new(posContext, TimeProvider.System);
        IReadOnlyList<ObrigatoriedadeLegal> vigentesDepoisDaDesativacao = await posRepository
            .ObterVigentesParaTipoProcessoAsync(TipoProcesso.SiSU.ToString(), DataDeCorte, CancellationToken.None);
        vigentesDepoisDaDesativacao.Should().BeEmpty(
            "pré-condição do teste — a desativação precisa ter efeito real no cadastro vivo");

        await using SelecaoDbContext depoisContext = _fixture.CreateDbContext();
        VersaoConfiguracao versaoDepois = await depoisContext.VersoesConfiguracao
            .AsNoTracking().FirstAsync(v => v.Id == snapshotId, CancellationToken.None);

        versaoDepois.ConfiguracaoCongelada.Should().Be(configuracaoAntes,
            "RN08 — o passado não muda: desativar a obrigatoriedade depois da publicação não altera o snapshot já congelado");
        versaoDepois.HashConfiguracao.Should().Be(hashAntes,
            "o hash é derivado dos bytes canônicos — se o conteúdo não muda, o hash não muda");
    }

    private static string UniqueRegraCodigo() => $"TEST_GATE_{Guid.CreateVersion7():N}";
}
