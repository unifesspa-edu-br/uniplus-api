namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Montagem compartilhada dos testes do formulário público lido do banco (Postgres real): o processo
/// com um fato de seleção, a publicação com a divulgação semeada e as leituras do formulário publicado
/// e do rascunho.
/// </summary>
internal sealed class CenarioDeFormularioPublicado
{
    private static readonly SnapshotPublicacaoCanonicalizer Canonicalizer = new();

    private readonly ProcessoSeletivoDbFixture _fixture;

    public CenarioDeFormularioPublicado(ProcessoSeletivoDbFixture fixture)
    {
        _fixture = fixture;
    }

    // O registro de PRODUÇÃO (não um substituto de teste): a leitura pública real, com
    // Postgres real, tem de passar pelo mesmo gate de versão que o handler usa fora dos
    // testes — reconhecendo hoje somente o codec vivo (ADR-0110 Emenda 2).
    public static EnvelopeCodec RegistroCodecs { get; } = new EnvelopeCodec();

    public UniPlusDbContext CriarDbContext() => _fixture.CreateDbContext();

    public static SnapshotCanonico Canonicalizar(EntradaCanonicalizacao entrada) => Canonicalizer.Canonicalizar(entrada);

    public static DadosEdital DadosDoEdital(Guid documentoId) => DadosEdital.Criar(
        "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(-3)), new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.FromHours(-3)), documentoId).Value!;

    private static ReferenciaRegra Regra(string codigo, char semente) =>
        ReferenciaRegra.De(codigo, "v1", new string(semente, 64));

    private static FaseCronograma FaseDeResultadoFinal() => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.Da(
            origemId: Guid.CreateVersion7(), codigo: "RESULTADO_FINAL", origemData: OrigemDataFase.NoEdital, agrupaEtapas: true, permiteComplementacao: false, coletaInscricao: true,
            coletaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        HomologacaoDasInscricoesDeTeste.PublicacoesDaFaseQueHomologa(),
        bancasRequeridas: [BancasDeTeste.DaHomologacaoDasInscricoesDaFase()]).Value!;

    public static ProcessoSeletivo NovoProcessoComFatoDeSelecao(string nome)
    {
        ProcessoSeletivo processo = ProcessoSeletivoPublicacaoSeeder.NovoProcessoConforme(nome);

        processo.DefinirItens([CorRacaDeEscolhaUnica()]).IsSuccess.Should().BeTrue();
        processo.DefinirTitulo("Formulário de Inscrição").IsSuccess.Should().BeTrue();
        processo.DefinirTermos([ExemploDeProcessoSeletivoEntrada.Termo("DECLARACAO_VERACIDADE", 0)]).IsSuccess.Should().BeTrue();

        return processo;
    }

    private static FatoColetado CorRacaDeEscolhaUnica() => FatoColetadoDeTeste.Coletando("COR_RACA", 0)
        .ComRotulo("Cor ou raça")
        .DoTipo(TipoDeCampo.EscolhaUnica)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .Construir();

    /// <summary>
    /// Publica o processo e grava o processo, a versão publicada e a divulgação. No caminho real a
    /// divulgação nasce quando o ato normativo se confirma no registro central; aqui é semeada
    /// direto, porque o que se exercita é a projeção. O processo que cobra taxa recebe, antes, o formulário de isenção que a publicação exige.
    /// </summary>
    public async Task<VersaoConfiguracao> PublicarEPersistirAsync(
        ProcessoSeletivo processo, DadosEdital dados, SnapshotCanonico congelado, string hashDocumento, string ator)
    {
        IsencaoDaTaxaDeTeste.GarantirOFormularioDeQuemCobraTaxa(processo);
        Result<VersaoConfiguracao> publicar = processo.Publicar(
            new CenarioDeCongelamento { Dados = dados, Documento = new DocumentoCongelado(congelado.Bytes, congelado.SchemaVersion, congelado.AlgoritmoHash, hashDocumento), AtorUsuarioContaId = ator, Contagem = ExemploDeProcessoSeletivoDadosDoEdital.ContextoRico(), MotivosEscolhidos = MotivosDeDecisaoDoProcessoDeTeste.Cobrindo(processo) });
        publicar.IsSuccess.Should().BeTrue(publicar.Error?.Message);
        await PersistirDivulgadoAsync(processo, publicar.Value!, dados);
        return publicar.Value!;
    }

    public async Task<FormularioApresentadoDoProcessoDto> LerPublicadoAsync(
        Guid processoId, string formularioCodigo = FormulariosDoProcessoDeTeste.Inscricao)
    {
        await using UniPlusDbContext readContext = _fixture.CreateDbContext();
        Result<FormularioApresentadoDoProcessoDto> lido = await ObterFormularioApresentadoQueryHandler.Handle(
            new ObterFormularioApresentadoQuery(processoId, formularioCodigo), ProcessoSeletivoRepositoryDeTeste.Criar(readContext),
            new CertameDivulgadoRepository(readContext), RegistroCodecs,
            new EnderecosDosDocumentosDoAto(new CertameDivulgadoRepository(readContext), AcervoDeTeste.Endereco), CancellationToken.None);
        lido.IsSuccess.Should().BeTrue(lido.Error?.Message);
        return lido.Value!;
    }

    public async Task<FormularioApresentadoDoProcessoDto> LerRascunhoAsync(Guid processoId, IReadOnlyList<FatoCandidatoView> catalogoVivo)
    {
        IFatoCandidatoReader leitor = Substitute.For<IFatoCandidatoReader>();
        leitor.ListarAsync(Arg.Any<CancellationToken>()).Returns(catalogoVivo);
        await using UniPlusDbContext rascunhoContext = _fixture.CreateDbContext();
        Result<FormularioApresentadoDoProcessoDto> rascunho = await ObterFormularioApresentadoDoRascunhoQueryHandler.Handle(
            new ObterFormularioApresentadoDoRascunhoQuery(processoId, FormulariosDoProcessoDeTeste.Inscricao),
            ProcessoSeletivoRepositoryDeTeste.Criar(rascunhoContext), leitor, FusoInstitucionalDeTeste(), CancellationToken.None);

        rascunho.IsSuccess.Should().BeTrue(rascunho.Error?.Message);
        return rascunho.Value!;
    }

    private static IResolvedorFusoInstitucional FusoInstitucionalDeTeste()
    {
        IResolvedorFusoInstitucional fuso = Substitute.For<IResolvedorFusoInstitucional>();
        fuso.Resolver().Returns(Result<TimeZoneInfo>.Success(TimeZoneInfo.FindSystemTimeZoneById(FusoInstitucional.ZoneId)));
        return fuso;
    }

    private async Task PersistirDivulgadoAsync(ProcessoSeletivo processo, VersaoConfiguracao versao, DadosEdital dados)
    {
        await using UniPlusDbContext writeContext = _fixture.CreateDbContext();
        ProcessoSeletivoRepository repository = ProcessoSeletivoRepositoryDeTeste.Criar(writeContext);
        await repository.AdicionarAsync(processo, CancellationToken.None);
        await repository.AdicionarVersaoConfiguracaoAsync(versao, CancellationToken.None);
        await new CertameDivulgadoRepository(writeContext).AdicionarAsync(
            Divulgacao(processo, versao, dados),
            CancellationToken.None);
        await writeContext.SaveChangesAsync(CancellationToken.None);
    }

    private static CertameDivulgado Divulgacao(ProcessoSeletivo processo, VersaoConfiguracao versao, DadosEdital dados) =>
        CertameDivulgado.Criar(
            processo.Id,
            versao.NumeroVersao,
            versao.AtoCriadorId,
            new string('a', 64),
            versaoProjecao: "1",
            new FacetasDoCertameDivulgado(
                processo.IdentificadorLegivel!.Value.Valor, processo.Nome, dados.Numero, ["AC"], dados.PeriodoInscricaoInicio, dados.PeriodoInscricaoFim),
            """{"nome":"documento"}""",
            TimeProvider.System.GetUtcNow());

}
