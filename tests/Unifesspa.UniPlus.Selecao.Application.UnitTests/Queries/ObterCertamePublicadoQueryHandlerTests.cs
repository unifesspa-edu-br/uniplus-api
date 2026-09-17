namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Cobertura do <see cref="ObterCertamePublicadoQueryHandler"/>: o critério conjunto de
/// visibilidade (versão vigente <b>e</b> ato criador registrado), o colapso das recusas numa só,
/// a exceção que não colapsa (versão de envelope fora das capacidades de leitura) e a projeção
/// campo a campo — inclusive a ausência dos blocos que o envelope carrega e o público não recebe.
/// </summary>
public sealed class ObterCertamePublicadoQueryHandlerTests
{
    private const string VersaoCorrenteReconhecida = "0.0.19";

    private const string VersaoAposentada = "0.0.7";

    private const string OfertaId = "0199a1b2-1111-7000-8000-000000000001";

    private const string DocumentoEditalId = "0199a1b2-2222-7000-8000-000000000002";

    private const string AtoRetificadoId = "0199a1b2-3333-7000-8000-000000000003";

    private static readonly TimeProvider Relogio = TimeProvider.System;

    private static readonly IRegistroCodecsEnvelope RegistroReconhecendoVersaoCorrente =
        CriarRegistroReconhecendo(VersaoCorrenteReconhecida);

    /// <summary>
    /// Envelope com todos os blocos que a projeção publica — e mais dois que ela não publica
    /// (<c>classificacao</c>, <c>fatosColetados</c>), para que a asserção de ausência tenha o que
    /// vazar caso a projeção deixe de ser campo a campo.
    /// </summary>
    private const string EnvelopeCompleto = $$"""
        {
          "tipoProcesso": {"origemId": "0199a1b2-4444-7000-8000-000000000004", "codigo": "SISU", "nome": "Sistema de Seleção Unificada"},
          "periodo": {"numero": "01/2026", "inicio": "2026-03-01T03:00:00Z", "fim": "2026-03-20T02:59:59Z"},
          "localidade": {"codigoIbge": "1504208", "nome": "Marabá", "uf": "PA", "fusoHorario": "America/Belem"},
          "identidadesUnidade": {
            "administradora": {
              "origemId": "0199a1b2-5555-7000-8000-000000000005",
              "sigla": "IGE", "slug": "ige", "nome": "Instituto de Geociências e Engenharias", "tipo": "INSTITUTO",
              "cidadeCodigoIbge": "1504208", "cidadeNome": "Marabá", "cidadeUf": "PA"
            }
          },
          "hashesEdital": {"documentoEditalId": "{{DocumentoEditalId}}", "hashSha256": "abc123"},
          "ofertas": ["{{OfertaId}}"],
          "modalidadesOfertadas": ["AC", "LB_PPI"],
          "vagas": [{
            "ofertaCursoOrigemId": "{{OfertaId}}",
            "quadro": [{"modalidadeCodigo": "AC", "quantidade": 20}, {"modalidadeCodigo": "LB_PPI", "quantidade": 10}],
            "vrNominal": 30, "vrFinal": 30, "estouro": 0, "capadoEmVo": false, "totalPublicado": 30
          }],
          "etapas": [{
            "id": "0199a1b2-6666-7000-8000-000000000006",
            "nome": "Prova objetiva", "carater": "Classificatoria",
            "tipoEtapa": {"origemId": "0199a1b2-7777-7000-8000-000000000007", "codigo": "PROVA", "nome": "Prova"},
            "peso": "1.0000", "notaMinima": null, "ordem": 1, "faseCodigo": "APLICACAO_PROVA",
            "inicio": "2026-04-05T12:00:00Z", "fim": "2026-04-05T17:00:00Z",
            "emiteParecerIndividual": false, "bancas": [], "recursos": [], "produtos": []
          }],
          "cronogramaFases": {
            "origemCandidatos": "Externa",
            "fases": [{
              "id": "0199a1b2-8888-7000-8000-000000000008", "ordem": 1,
              "faseCanonicaOrigemId": "0199a1b2-9999-7000-8000-000000000009", "codigo": "INSCRICAO",
              "donoInstitucional": "CEPS", "origemData": "Propria", "agrupaEtapas": false,
              "permiteComplementacao": true, "coletaInscricao": true, "coletaSolicitacaoIsencao": false,
              "inicio": "2026-03-01T03:00:00Z", "fim": "2026-03-20T02:59:59Z",
              "produtos": [], "faseConcluinteCodigo": null, "emiteParecerIndividual": false,
              "bancasRequeridas": [], "regraRecurso": null
            }]
          },
          "documentosExigidos": {
            "exigencias": [{
              "exigenciaId": "0199a1b2-aaaa-7000-8000-00000000000a",
              "tipoDocumentoOrigemId": "0199a1b2-bbbb-7000-8000-00000000000b",
              "tipoDocumentoCodigo": "DOC_OFICIAL_FOTO",
              "tipoDocumentoNome": "Documento oficial com foto",
              "aplicabilidade": "Geral",
              "obrigatorio": true,
              "formatosPermitidos": {"qualquer": false, "lista": [{"formato": "PDF", "tamanhoMaximoBytesMax": 5242880}]}
            }],
            "obrigatoriedades": [], "referenciaTemporalFatos": null, "dataReferenciaFatos": null, "metadadosFatos": []
          },
          "atendimento": {
            "condicoes": [{"condicaoOrigemId": "0199a1b2-cccc-7000-8000-00000000000c", "condicaoCodigo": "TEMPO_ADICIONAL", "condicaoNome": "Tempo adicional"}],
            "recursos": [{"recursoOrigemId": "0199a1b2-dddd-7000-8000-00000000000d", "recursoNome": "Ledor"}],
            "tiposDeficiencia": [{"tipoDeficienciaOrigemId": "0199a1b2-eeee-7000-8000-00000000000e", "tipoDeficienciaCodigo": "VISUAL", "tipoDeficienciaNome": "Deficiência visual"}]
          },
          "taxaInscricao": {"presente": true, "cobra": true, "valor": "80.00", "fundamentos": ["LEI_12799_2013"]},
          "classificacao": {"regrasEliminacao": [{"codigo": "ELIM-NOTA-MINIMA-ETAPA"}]},
          "fatosColetados": [{"fatoCodigo": "COR_RACA", "ordem": 0}]
        }
        """;

    [Fact(DisplayName = "Sem versão vigente devolve não encontrado sem sequer perguntar pelo ato")]
    public async Task Handle_SemVersaoVigente_NaoEncontradoESemConsultarOAto()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersaoVigenteAsync(processoId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns((VersaoConfiguracao?)null);
        IAtoRegistradoReader leitor = Substitute.For<IAtoRegistradoReader>();

        Result<CertamePublicadoDto> resultado = await HandleAsync(repository, leitor, processoId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
        await leitor.DidNotReceiveWithAnyArgs().EstaRegistradoAsync(default, default);
    }

    [Fact(DisplayName = "Versão vigente cujo ato criador não está registrado devolve o MESMO não encontrado")]
    public async Task Handle_AtoNaoRegistrado_ColapsaNoMesmoNaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, EnvelopeCompleto, out Guid atoCriadorId);

        Result<CertamePublicadoDto> resultado = await HandleAsync(repository, LeitorRespondendo(atoCriadorId, false), processoId);

        resultado.IsFailure.Should().BeTrue(
            "enquanto o registro do ato não se confirma — por dreno em curso ou por recusa terminal — o certame não é divulgado");
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Versão fora das capacidades de leitura falha nomeadamente, sem se disfarçar de não encontrado")]
    public async Task Handle_SchemaVersionAposentada_NaoColapsaNoNaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(
            processoId, EnvelopeCompleto, out Guid atoCriadorId, VersaoAposentada);

        Result<CertamePublicadoDto> resultado = await HandleAsync(repository, LeitorRespondendo(atoCriadorId, true), processoId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ErrosCodecEnvelope.VersaoDesconhecida);
    }

    [Fact(DisplayName = "Certame visível projeta os blocos públicos e nenhum bloco interno")]
    public async Task Handle_AtoRegistrado_ProjetaSomenteOsBlocosPublicos()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, EnvelopeCompleto, out Guid atoCriadorId);

        Result<CertamePublicadoDto> resultado = await HandleAsync(repository, LeitorRespondendo(atoCriadorId, true), processoId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        CertamePublicadoDto certame = resultado.Value!;

        certame.ProcessoSeletivoId.Should().Be(processoId);
        certame.AtoCriadorId.Should().Be(atoCriadorId);
        certame.TipoProcesso.Codigo.Should().Be("SISU");
        certame.Periodo.Numero.Should().Be("01/2026");
        certame.Periodo.Inicio.Should().Be(new DateTimeOffset(2026, 3, 1, 3, 0, 0, TimeSpan.Zero));
        certame.Periodo.Inicio.Offset.Should().Be(TimeSpan.Zero, "o instante congelado é UTC e não se desloca pelo fuso de quem lê");
        certame.Localidade.Uf.Should().Be("PA");
        certame.UnidadeAdministradora.Sigla.Should().Be("IGE");
        certame.UnidadeAdministradora.CidadeNome.Should().Be("Marabá");
        certame.DocumentoEdital.DocumentoEditalId.Should().Be(Guid.Parse(DocumentoEditalId));
        certame.Ofertas.Should().ContainSingle().Which.Should().Be(Guid.Parse(OfertaId));
        certame.ModalidadesOfertadas.Should().BeEquivalentTo(["AC", "LB_PPI"]);
        certame.Vagas.Should().ContainSingle().Which.TotalPublicado.Should().Be(30);
        certame.Vagas[0].Quadro.Should().HaveCount(2);
        certame.Etapas.Should().ContainSingle().Which.Nome.Should().Be("Prova objetiva");
        certame.Etapas[0].TipoEtapa.Codigo.Should().Be("PROVA");
        certame.Etapas[0].NotaMinima.Should().BeNull();
        certame.OrigemCandidatos.Should().Be("Externa");
        certame.CronogramaFases.Should().ContainSingle().Which.ColetaInscricao.Should().BeTrue();
        certame.DocumentosExigidos.Should().ContainSingle().Which.Rotulo.Should().Be("Documento oficial com foto");
        certame.DocumentosExigidos[0].Formatos.Lista.Should().BeEquivalentTo(["PDF"]);
        certame.Atendimento.Recursos.Should().BeEquivalentTo(["Ledor"]);
        certame.TaxaInscricao!.Valor.Should().Be("80.00");
        certame.Retificacao.Should().BeNull("a publicação de abertura não emenda ato algum");

        // Ausência estrutural: o contrato é a assinatura do tipo, e um bloco interno do envelope
        // não tem por onde atravessar a fronteira pública.
        typeof(CertamePublicadoDto).GetProperties().Select(static p => p.Name).Should().NotContain(
            ["Classificacao", "FatosColetados", "CriteriosDesempate", "Distribuicao", "GrafoDependencia"]);
    }

    [Fact(DisplayName = "Retificação congelada aparece no contrato público")]
    public async Task Handle_VersaoRetificada_ProjetaARetificacao()
    {
        string envelope = EnvelopeCompleto.TrimEnd().TrimEnd('}')
            + ", \"retificacao\": {\"editalRetificadoId\": \"" + AtoRetificadoId
            + "\", \"motivo\": \"Correção do quadro de vagas\"}}";

        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelope, out Guid atoCriadorId);

        Result<CertamePublicadoDto> resultado = await HandleAsync(repository, LeitorRespondendo(atoCriadorId, true), processoId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Retificacao!.AtoRetificadoId.Should().Be(Guid.Parse(AtoRetificadoId));
        resultado.Value.Retificacao.Motivo.Should().Be("Correção do quadro de vagas");
    }

    [Fact(DisplayName = "Bloco com forma inesperada recusa a leitura inteira, nunca projeta pela metade")]
    public async Task Handle_BlocoMalformado_RecusaAProjecaoInteira()
    {
        string envelope = EnvelopeCompleto.Replace(
            "\"codigoIbge\": \"1504208\", \"nome\": \"Marabá\"",
            "\"nome\": \"Marabá\"",
            StringComparison.Ordinal);

        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelope, out Guid atoCriadorId);

        Result<CertamePublicadoDto> resultado = await HandleAsync(repository, LeitorRespondendo(atoCriadorId, true), processoId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CertamePublicado.EnvelopeInesperado");
    }

    [Fact(DisplayName = "Instante fora da forma canônica é recusado, nunca lido no fuso da máquina que serve")]
    public async Task Handle_InstanteSemDesignadorDeFuso_Recusa()
    {
        // Sem o 'Z', uma leitura permissiva atribuiria ao texto o fuso LOCAL do processo: o mesmo
        // envelope responderia períodos de inscrição diferentes conforme a máquina que atende.
        string envelope = EnvelopeCompleto.Replace(
            "\"inicio\": \"2026-03-01T03:00:00Z\", \"fim\": \"2026-03-20T02:59:59Z\"",
            "\"inicio\": \"2026-03-01T03:00:00\", \"fim\": \"2026-03-20T02:59:59Z\"",
            StringComparison.Ordinal);

        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelope, out Guid atoCriadorId);

        Result<CertamePublicadoDto> resultado = await HandleAsync(repository, LeitorRespondendo(atoCriadorId, true), processoId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CertamePublicado.EnvelopeInesperado");
    }

    [Fact(DisplayName = "Exigência condicional é publicada como condicional, não como obrigatória para todos")]
    public async Task Handle_ExigenciaCondicional_PublicaAAplicabilidade()
    {
        // Sozinha, a obrigatoriedade mente: uma exigência condicional incide apenas sobre quem
        // satisfaz o gatilho, e publicá-la como obrigatória faria o candidato de ampla concorrência
        // reunir documento que não lhe é pedido.
        string envelope = EnvelopeCompleto.Replace(
            "\"aplicabilidade\": \"Geral\"",
            "\"aplicabilidade\": \"Condicional\"",
            StringComparison.Ordinal);

        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelope, out Guid atoCriadorId);

        Result<CertamePublicadoDto> resultado = await HandleAsync(repository, LeitorRespondendo(atoCriadorId, true), processoId);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.DocumentosExigidos.Should().ContainSingle()
            .Which.Aplicabilidade.Should().Be("Condicional");
    }

    [Fact(DisplayName = "Formatos que dizem aceitar qualquer um E trazem lista são recusados")]
    public async Task Handle_FormatosContraditorios_RecusaAProjecao()
    {
        // "aceita qualquer formato" com uma lista de formatos ao lado são duas afirmações
        // contraditórias no mesmo objeto — repassá-las publicaria a contradição.
        string envelope = EnvelopeCompleto.Replace(
            "\"qualquer\": false, \"lista\"",
            "\"qualquer\": true, \"lista\"",
            StringComparison.Ordinal);

        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelope, out Guid atoCriadorId);

        Result<CertamePublicadoDto> resultado = await HandleAsync(repository, LeitorRespondendo(atoCriadorId, true), processoId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CertamePublicado.EnvelopeInesperado");
    }

    private static Task<Result<CertamePublicadoDto>> HandleAsync(
        IProcessoSeletivoRepository repository, IAtoRegistradoReader atoRegistradoReader, Guid processoId) =>
        ObterCertamePublicadoQueryHandler.Handle(
            new ObterCertamePublicadoQuery(processoId),
            repository,
            atoRegistradoReader,
            RegistroReconhecendoVersaoCorrente,
            Relogio,
            CancellationToken.None);

    private static IAtoRegistradoReader LeitorRespondendo(Guid atoId, bool registrado)
    {
        IAtoRegistradoReader leitor = Substitute.For<IAtoRegistradoReader>();
        leitor.EstaRegistradoAsync(atoId, Arg.Any<CancellationToken>()).Returns(registrado);
        return leitor;
    }

    private static IRegistroCodecsEnvelope CriarRegistroReconhecendo(params string[] schemaVersionsReconhecidas)
    {
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        registro.Capacidades.Returns(schemaVersionsReconhecidas
            .Select(static v => new CapacidadeCodec(v, TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null))
            .ToList());
        return registro;
    }

    private static IProcessoSeletivoRepository MockComVersaoVigente(
        Guid processoId,
        string envelopeJson,
        out Guid atoCriadorId,
        string schemaVersion = VersaoCorrenteReconhecida)
    {
        atoCriadorId = Guid.CreateVersion7();
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processoId,
            Encoding.UTF8.GetBytes(envelopeJson),
            schemaVersion,
            "canonical-json/sha256@v1",
            atoCriadorId,
            new string('a', 64),
            "user-sub-123",
            Relogio.GetUtcNow());

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersaoVigenteAsync(processoId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(versao);
        return repository;
    }
}
