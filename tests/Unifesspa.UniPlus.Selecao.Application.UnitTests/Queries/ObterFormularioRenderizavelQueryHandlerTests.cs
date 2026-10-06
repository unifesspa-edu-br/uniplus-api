namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura do <see cref="ObterFormularioRenderizavelQueryHandler"/> (Story #559/#1059): a
/// resolução pela divulgação, a recusa de uma <c>SchemaVersion</c> aposentada (issue #1089) e da
/// versão que não se prova, e o que ainda sai do JSON congelado — a comprovação documental e a data
/// de referência dos fatos. A apresentação e as regras saem do grafo reidratado pela projeção
/// única, coberta em <see cref="Services.ProjecaoDoFormularioRenderizavelTests"/>; aqui o registro
/// de codecs é um substituto, e a reidratação real é provada na suíte de integração.
/// </summary>
public sealed class ObterFormularioRenderizavelQueryHandlerTests
{
    /// <summary>
    /// A versão que o registro de codecs FAKE (<see cref="RegistroReconhecendoVersaoCorrente"/>)
    /// declara como única capacidade de leitura — o caso corrente de todo teste que não é sobre o
    /// gate de versão em si.
    /// </summary>
    private const string VersaoCorrenteReconhecida = "0.0.10";

    /// <summary>
    /// Uma versão que já foi corrente e deixou de ser reconhecida quando o codec vivo avançou —
    /// usada SÓ no teste que prova a recusa (issue #1089); nenhum outro teste deste arquivo rotula
    /// o caso corrente com ela.
    /// </summary>
    private const string VersaoAposentada = "0.0.7";

    private static readonly TimeProvider Relogio = TimeProvider.System;

    /// <summary>
    /// Substituto da porta reconhecendo, como única capacidade de leitura, exatamente
    /// <see cref="VersaoCorrenteReconhecida"/> — o mesmo perfil do registro de produção, que hoje
    /// tem um único codec vivo (ADR-0110 Emenda 2).
    /// </summary>
    private static readonly IRegistroCodecsEnvelope RegistroReconhecendoVersaoCorrente =
        CriarRegistroReconhecendo(VersaoCorrenteReconhecida);

    /// <summary>
    /// Ato que confirmou a publicidade da versão servida. Único em todo o arquivo: os testes não
    /// são sobre QUAL versão a divulgação aponta — isso é dos testes da vitrine —, e sim sobre o
    /// que a projeção faz com a versão que ela aponta.
    /// </summary>
    private static readonly Guid AtoDaDivulgacao = Guid.CreateVersion7();

    /// <summary>O acervo público, com o endereço base da borda fixado para o teste.</summary>
    private static readonly IEnderecoNoAcervoPublico Acervo = CriarAcervo();

    private static IEnderecoNoAcervoPublico CriarAcervo()
    {
        IEnderecoNoAcervoPublico acervo = Substitute.For<IEnderecoNoAcervoPublico>();
        acervo.De(Arg.Any<string>()).Returns(static chamada => new Uri($"https://acervo.teste/{chamada.Arg<string>()}"));
        return acervo;
    }

    private static Task<Result<FormularioRenderizavelDto>> HandleAsync(
        IProcessoSeletivoRepository repository,
        Guid processoId,
        ICertameDivulgadoRepository? divulgadoRepository = null,
        FinalidadeFormulario finalidade = FinalidadeFormulario.Inscricao) =>
        ObterFormularioRenderizavelQueryHandler.Handle(
            new ObterFormularioRenderizavelQuery(processoId, finalidade),
            repository,
            divulgadoRepository ?? RepositorioComDivulgacao(processoId),
            RegistroReconhecendoVersaoCorrente,
            Acervo,
            CancellationToken.None);

    /// <summary>Repositório cuja linha de divulgação existe — o processo É público.</summary>
    private static ICertameDivulgadoRepository RepositorioComDivulgacao(Guid processoId)
    {
        ICertameDivulgadoRepository repositorio = Substitute.For<ICertameDivulgadoRepository>();
        repositorio.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>())
            .Returns(CertameDivulgado.Criar(
                processoId,
                numeroVersao: 1,
                AtoDaDivulgacao,
                new string('a', 64),
                versaoProjecao: "1",
                new FacetasDoCertameDivulgado("certame-formulario", "Certame", "001/2026", ["AC"], DataHoraFixa, DataHoraFixa.AddDays(30)),
                """{"nome":"documento"}""",
                DataHoraFixa));
        return repositorio;
    }

    /// <summary>Repositório sem linha: o processo não é público, seja qual for o motivo.</summary>
    private static ICertameDivulgadoRepository RepositorioSemDivulgacao()
    {
        ICertameDivulgadoRepository repositorio = Substitute.For<ICertameDivulgadoRepository>();
        repositorio.ObterParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CertameDivulgado?)null);
        return repositorio;
    }

    private static readonly DateTimeOffset DataHoraFixa = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private static IRegistroCodecsEnvelope CriarRegistroReconhecendo(params string[] schemaVersionsReconhecidas)
    {
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        registro.Capacidades.Returns(schemaVersionsReconhecidas
            .Select(static v => new CapacidadeCodec(v, TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null))
            .ToList());
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(Result<EnvelopeReidratado>.Success(EnvelopeCom()));
        return registro;
    }

    private static IRegistroCodecsEnvelope RegistroReidratando(EnvelopeReidratado envelope)
    {
        IRegistroCodecsEnvelope registro = CriarRegistroReconhecendo(VersaoCorrenteReconhecida);
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(Result<EnvelopeReidratado>.Success(envelope));
        return registro;
    }

    /// <summary>
    /// A reidratação de um processo com os formulários dados, sobre a estrutura de um processo
    /// conforme. Sem formulários, nenhuma finalidade tem o que servir.
    /// </summary>
    private static EnvelopeReidratado EnvelopeCom(params FormularioProcesso[] formularios)
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar("PS 2026 — SiSU");
        return new EnvelopeReidratado(
            new GrafoConfiguracao(
                [.. processo.Etapas], processo.OfertaAtendimento!, [.. processo.DistribuicaoVagas],
                processo.BonusRegional, [.. processo.CriteriosDesempate], processo.Classificacao!,
                [.. processo.CronogramaFases], [.. processo.DocumentosExigidos], [], null, formularios: formularios),
            DadosEdital.Criar(
                "001/2026", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null);
    }

    /// <summary>Um formulário só com blocos: o de comprovação documental, quando pedido, e o de revisão e aceite.</summary>
    private static FormularioProcesso Formulario(FinalidadeFormulario finalidade, Guid? faseId, bool comComprovacao) =>
        FormularioProcesso.Criar(
            finalidade, faseId, null,
            [
                .. comComprovacao
                    ? [EtapaFormulario.Criar("DOCUMENTOS", 0, TipoEtapaFormulario.Bloco, BlocoSistema.ComprovacaoDocumental, "Documentos", null, null).Value!]
                    : Array.Empty<EtapaFormulario>(),
                EtapaFormulario.Criar("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!,
            ]).Value!;

    private static Task<Result<FormularioRenderizavelDto>> HandleAsync(
        Guid processoId, string envelopeJson, EnvelopeReidratado reidratado, FinalidadeFormulario finalidade) =>
        ObterFormularioRenderizavelQueryHandler.Handle(
            new ObterFormularioRenderizavelQuery(processoId, finalidade),
            MockComVersaoVigente(processoId, envelopeJson),
            RepositorioComDivulgacao(processoId),
            RegistroReidratando(reidratado),
            Acervo,
            CancellationToken.None);

    [Fact(DisplayName = "Certame sem divulgação não serve formulário, e a recusa não diz por quê")]
    public async Task Handle_SemDivulgacao_RetornaNaoEncontrado()
    {
        // Inexistente, em rascunho, sem versão vigente e com ato não confirmado caem todos aqui,
        // pelo mesmo caminho: nenhum deles tem linha. Distinguir deixou de ser possível, em vez de
        // ser possível e proibido — e numa rota anônima essa diferença importa, porque a recusa
        // que distingue responde a um estranho se um identificador é um processo em rascunho.
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(
            repository, processoId, RepositorioSemDivulgacao());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Sem divulgação, a versão de configuração nem chega a ser consultada")]
    public async Task Handle_QuandoNaoHaDivulgacao_NaoDeveConsultarAVersao()
    {
        // A publicidade é a primeira pergunta, não um filtro aplicado depois. Consultar a versão
        // antes reabriria o oráculo por outro caminho: o tempo de resposta, e qualquer recusa que
        // dependesse do que a consulta encontrasse.
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();

        await HandleAsync(repository, processoId, RepositorioSemDivulgacao());

        await repository.DidNotReceive().ObterVersoesPorAtoCriadorAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Divulgação apontando versão inexistente NÃO colapsa no não encontrado")]
    public async Task Handle_QuandoDivulgacaoApontaVersaoInexistente_DeveAflorarODefeito()
    {
        // A linha só nasce junto da versão, então isto é corrupção, não ausência. Responder não
        // encontrado esconderia o defeito atrás de uma resposta plausível — e aqui não há oráculo
        // a proteger: a existência da linha já disse que o processo é público.
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersoesPorAtoCriadorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Snapshot.VigenteAusente");
    }

    [Fact(DisplayName = "O formulário é projetado da versão que a divulgação aponta, pelo ato criador")]
    public async Task Handle_QuandoHaDivulgacao_DeveResolverPelaVersaoQueElaAponta()
    {
        // O ponto da issue: sob retificação pendente, a versão vigente POR RELÓGIO é a nova, que
        // ainda não tem publicidade. Servir o formulário dela faria o candidato ler um edital e
        // preencher o de outro. A consulta tem de partir do ato que a divulgação carrega.
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersoesPorAtoCriadorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await HandleAsync(repository, processoId);

        await repository.Received(1).ObterVersoesPorAtoCriadorAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(AtoDaDivulgacao)),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Processo sem formulário da finalidade não serve formulário")]
    public async Task Handle_SemFormularioDaFinalidade_NaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(
            processoId, """{"documentosExigidos": {"dataReferenciaFatos": null}}""",
            EnvelopeCom(Formulario(FinalidadeFormulario.Inscricao, null, comComprovacao: false)), FinalidadeFormulario.Habilitacao);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Theory(DisplayName = "A data de referência dos fatos sai do envelope congelado; fora da forma de data, a versão não tem apresentação")]
    [InlineData("\"2026-01-31\"", true)]
    [InlineData("null", true)]
    [InlineData("\"31/01/2026\"", false)]
    public async Task Handle_DataReferenciaFatos_SaiDoEnvelope(string data, bool valida)
    {
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(
            processoId, $$"""{"documentosExigidos": {"dataReferenciaFatos": {{data}} } }""",
            EnvelopeCom(Formulario(FinalidadeFormulario.Inscricao, null, comComprovacao: false)), FinalidadeFormulario.Inscricao);

        if (valida)
        {
            resultado.Value!.DataReferenciaFatos.Should().Be(data == "null" ? null : new DateOnly(2026, 1, 31));
        }
        else
        {
            resultado.Error!.Code.Should().Be("FormularioInscricao.VersaoSemApresentacao");
        }
    }

    [Fact(DisplayName = "O bloco de comprovação documental lista as exigências da fase do formulário, na forma pública e com o modelo editável; sem o bloco, nada")]
    public async Task Handle_BlocoDeComprovacao_ListaAsExigenciasDaFaseDoFormulario()
    {
        Guid faseHabilitacao = Guid.Parse("0199a000-0000-7000-8000-0000000000f2");
        Guid faseInscricao = Guid.Parse("0199a000-0000-7000-8000-0000000000f1");
        string envelope = $$"""
            {"documentosExigidos": {"dataReferenciaFatos": null, "exigencias": [
              {{Exigencia("Documento de identidade", "Geral", faseHabilitacao, "HABILITACAO")}},
              {{Exigencia("Comprovante de inscrição", "Geral", faseInscricao, "INSCRICAO")}},
              {{Exigencia("Autodeclaração étnico-racial", "Condicional", faseHabilitacao, "HABILITACAO", Modelo)}}
            ] } }
            """;
        EnvelopeReidratado reidratado = EnvelopeCom(
            Formulario(FinalidadeFormulario.Habilitacao, faseHabilitacao, comComprovacao: true),
            Formulario(FinalidadeFormulario.Inscricao, faseInscricao, comComprovacao: false));
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> habilitacao = await HandleAsync(processoId, envelope, reidratado, FinalidadeFormulario.Habilitacao);
        Result<FormularioRenderizavelDto> inscricao = await HandleAsync(processoId, envelope, reidratado, FinalidadeFormulario.Inscricao);

        habilitacao.Value!.ComprovacaoDocumental!.Select(static e => (e.Rotulo, e.Aplicabilidade)).Should().Equal(
            [("Documento de identidade", "Geral"), ("Autodeclaração étnico-racial", "Condicional")],
            "só as exigências da fase da habilitação, na ordem congelada");
        habilitacao.Value.ComprovacaoDocumental!.Select(static e => e.Modelo).Should().BeEquivalentTo(
            [
                null,
                new ModeloDocumentalCertameDto(
                    "Autodeclaração.odt",
                    "ODT",
                    new string('a', 64),
                    new Uri(
                        $"https://acervo.teste/selecao/processos-seletivos/{processoId:D}/atos/{AtoDaDivulgacao:D}"
                        + $"/modelos-de-documento/0199a000-0000-7000-8000-0000000000aa/{new string('a', 64)}.odt")),
            ],
            "a exigência com modelo o traz com o endereço no acervo do ato da divulgação, sem o id do cadastro como campo; a sem modelo, nulo");
        inscricao.Value!.ComprovacaoDocumental.Should().BeNull("o formulário de inscrição não tem o bloco");
    }

    /// <summary>
    /// Inscrição e isenção respondidas na mesma fase, as duas com o bloco de comprovação: a fase não
    /// separa os documentos, e cada formulário mostra só os da sua finalidade. O documento fora de
    /// formulário não aparece em nenhum.
    /// </summary>
    [Fact(DisplayName = "Na fase que divide inscrição e isenção, o bloco de comprovação de cada formulário lista só as exigências da sua finalidade")]
    public async Task Handle_BlocoDeComprovacaoNaFaseCompartilhada_ListaSoAsExigenciasDaFinalidade()
    {
        Guid fase = Guid.Parse("0199a000-0000-7000-8000-0000000000f1");
        string envelope = $$"""
            {"documentosExigidos": {"dataReferenciaFatos": null, "exigencias": [
              {{Exigencia("Documento de identidade", "Geral", fase, "INSCRICAO")}},
              {{Exigencia("Comprovante de renda", "Geral", fase, "ISENCAO_TAXA")}},
              {{Exigencia("Laudo da banca", "Geral", fase, null)}}
            ] } }
            """;
        EnvelopeReidratado reidratado = EnvelopeCom(
            Formulario(FinalidadeFormulario.Inscricao, fase, comComprovacao: true),
            Formulario(FinalidadeFormulario.IsencaoTaxa, fase, comComprovacao: true));
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> inscricao = await HandleAsync(processoId, envelope, reidratado, FinalidadeFormulario.Inscricao);
        Result<FormularioRenderizavelDto> isencao = await HandleAsync(processoId, envelope, reidratado, FinalidadeFormulario.IsencaoTaxa);

        inscricao.Value!.ComprovacaoDocumental!.Select(static e => e.Rotulo).Should().Equal("Documento de identidade");
        isencao.Value!.ComprovacaoDocumental!.Select(static e => e.Rotulo).Should().Equal("Comprovante de renda");
    }

    [Theory(DisplayName = "Com o bloco de comprovação, fase ou finalidade ausente ou malformada recusa a versão em vez de omitir documento")]
    [InlineData(false, "\"0199a000-0000-7000-8000-0000000000f2\"", "\"finalidade\": \"HABILITACAO\",")]
    [InlineData(true, "null", "\"finalidade\": \"HABILITACAO\",")]
    [InlineData(true, "\"nao-e-guid\"", "\"finalidade\": \"HABILITACAO\",")]
    [InlineData(true, "\"0199a000-0000-7000-8000-0000000000f2\"", "")]
    [InlineData(true, "\"0199a000-0000-7000-8000-0000000000f2\"", "\"finalidade\": \"MATRICULA\",")]
    public async Task Handle_BlocoDeComprovacaoComFaseOuFinalidadeAusente_Recusa(bool formularioComFase, string faseDaExigencia, string finalidadeDaExigencia)
    {
        string envelope = $$"""
            {"documentosExigidos": {"dataReferenciaFatos": null, "exigencias": [
              {"tipoDocumentoNome": "Documento de identidade", "aplicabilidade": "Geral", "obrigatorio": true, "exigidoNaFaseId": {{faseDaExigencia}},
               {{finalidadeDaExigencia}} "formatosPermitidos": {"lista": null, "qualquer": true}, "modelo": null }
            ] } }
            """;
        Guid? faseDoFormulario = formularioComFase ? Guid.Parse("0199a000-0000-7000-8000-0000000000f2") : null;

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(
            Guid.CreateVersion7(), envelope,
            EnvelopeCom(Formulario(FinalidadeFormulario.Habilitacao, faseDoFormulario, comComprovacao: true)), FinalidadeFormulario.Habilitacao);

        resultado.Error!.Code.Should().Be("FormularioInscricao.VersaoSemApresentacao");
    }

    private const string Modelo = """
        {"modeloId": "0199a000-0000-7000-8000-0000000000aa", "nomeArquivo": "Autodeclaração.odt", "formato": "ODT", "hashSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}
        """;

    private static string Exigencia(string nome, string aplicabilidade, Guid fase, string? finalidade, string modelo = "null") => $$"""
        {"tipoDocumentoNome": "{{nome}}", "aplicabilidade": "{{aplicabilidade}}", "obrigatorio": true, "exigidoNaFaseId": "{{fase}}",
         "finalidade": {{(finalidade is null ? "null" : $"\"{finalidade}\"")}}, "formatosPermitidos": {"lista": null, "qualquer": true}, "modelo": {{modelo}} }
        """;

    /// <summary>
    /// O teste central da issue #1089: a versão é aposentada, mas o JSON congelado tem a forma
    /// ATUAL e é válido — exatamente o cenário em que decidir pela forma (o que o handler fazia
    /// antes desta correção) mascara a recusa que o registro de codecs já dá em outras
    /// superfícies (ex.: <c>AbrirRetificacaoCommandHandler</c>). Um envelope malformado não
    /// provaria nada aqui: o handler já o recusava antes, por <c>FormularioInscricao.VersaoSemApresentacao</c>.
    /// O que só esta correção resolve é a versão desconhecida com bytes que "passariam" no shape.
    /// </summary>
    [Fact(DisplayName = "Versão aposentada com bytes de forma corrente recusa com EnvelopeCodec.VersaoDesconhecida, sem tentar decidir pela forma")]
    public async Task Handle_VersaoAposentadaComFormaCorrente_RetornaVersaoDesconhecida()
    {
        const string envelopeFormaCorrente = """
            {
              "documentosExigidos": {"dataReferenciaFatos": null}, "formularios": [{"finalidade": "INSCRICAO", "faseId": null, "titulo": "Formulário de Inscrição", "modeloOrigem": null, "etapas": [
                  {"codigo": "DADOS", "ordem": 0, "tipo": "SECAO", "bloco": null, "titulo": "Dados", "descricao": null, "aviso": null},
                  {"codigo": "REVISAO", "ordem": 1, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE", "titulo": "Revisão e aceite", "descricao": null, "aviso": null}
                ], "termos": []}],
              "gruposColetados": [], "fatosColetados": []
            }
            """;
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelopeFormaCorrente, VersaoAposentada);

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsFailure.Should().BeTrue(
            $"a versão '{VersaoAposentada}' deixou de ser reconhecida quando o codec vivo avançou para " +
            $"'{VersaoCorrenteReconhecida}' — bytes coincidentemente válidos na forma atual não a devolvem " +
            "à lista de capacidades reconhecidas");
        resultado.Error!.Code.Should().Be("EnvelopeCodec.VersaoDesconhecida");
    }

    [Fact(DisplayName = "Versão cujos bytes não se provam não serve formulário: a recusa da reidratação aflora")]
    public async Task Handle_ReidratacaoRecusada_AfloraARecusa()
    {
        const string envelope = """
            {
              "documentosExigidos": {"dataReferenciaFatos": null}, "formularios": [{"finalidade": "INSCRICAO", "faseId": null, "titulo": null, "modeloOrigem": null, "etapas": [], "termos": []}],
              "gruposColetados": [], "fatosColetados": []
            }
            """;
        Guid processoId = Guid.CreateVersion7();
        IRegistroCodecsEnvelope registro = CriarRegistroReconhecendo(VersaoCorrenteReconhecida);
        registro.Reidratar(Arg.Any<VersaoConfiguracao>()).Returns(
            Result<EnvelopeReidratado>.Failure(new DomainError("EnvelopeCodec.HashDivergente", "Os bytes não batem com o hash.")));

        Result<FormularioRenderizavelDto> resultado = await ObterFormularioRenderizavelQueryHandler.Handle(
            new ObterFormularioRenderizavelQuery(processoId, FinalidadeFormulario.Inscricao),
            MockComVersaoVigente(processoId, envelope),
            RepositorioComDivulgacao(processoId),
            registro,
            Acervo,
            CancellationToken.None);

        resultado.Error!.Code.Should().Be("EnvelopeCodec.HashDivergente", "as regras saem da reidratação, e uma versão que não se prova não é servida");
    }

    /// <summary>
    /// Prova ESTRUTURAL (não comportamental) de que a leitura pública nunca reconsulta o
    /// catálogo: nenhum parâmetro do <c>Handle</c> é um leitor cross-módulo do catálogo de fatos
    /// (<c>IFatoCandidatoReader</c>) — os valores selecionáveis entregues vêm inteiramente do
    /// envelope congelado já persistido em <see cref="VersaoConfiguracao.ConfiguracaoCongelada"/>,
    /// nunca de uma releitura do cadastro vivo. É essa ausência estrutural, e não um mock que
    /// "não é chamado", que garante que alterar/inativar/remover descrições no catálogo vivo
    /// nunca muda a resposta deste endpoint.
    /// </summary>
    [Fact(DisplayName = "O grafo de dependências do Handle não alcança IFatoCandidatoReader — a leitura pública é imune ao catálogo vivo")]
    public void Handle_NaoDependeDeLeitorDeCatalogo()
    {
        System.Reflection.MethodInfo handle = typeof(ObterFormularioRenderizavelQueryHandler)
            .GetMethod(nameof(ObterFormularioRenderizavelQueryHandler.Handle))!;

        handle.GetParameters().Select(static p => p.ParameterType.Name).Should().NotContain(
            "IFatoCandidatoReader",
            "os valores selecionáveis do formulário público vêm do envelope congelado, nunca de uma releitura do " +
            "catálogo vivo — se o Handle injetasse o leitor cross-módulo, alterar/inativar/remover uma descrição " +
            "no cadastro mudaria a resposta de uma versão já publicada, o que a imutabilidade do envelope proíbe");
    }

    private static IProcessoSeletivoRepository MockComVersaoVigente(
        Guid processoId, string envelopeJson, string schemaVersion = VersaoCorrenteReconhecida)
    {
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            processoId,
            Encoding.UTF8.GetBytes(envelopeJson),
            schemaVersion,
            "canonical-json/sha256@v1",
            Guid.CreateVersion7(),
            new string('a', 64),
            "user-sub-123",
            Relogio.GetUtcNow());

        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersoesPorAtoCriadorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([versao]);
        return repository;
    }
}
