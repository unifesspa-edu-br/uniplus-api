namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Cobertura do <see cref="ObterFormularioRenderizavelQueryHandler"/> (Story #559/#1059): a
/// distinção 404/422/200, a guarda contra versão vigente congelada ANTES de a apresentação do
/// formulário — ou os valores selecionáveis (UNI-REQ-0072) — existirem no envelope, e a recusa de
/// uma <c>SchemaVersion</c> aposentada (issue #1089) mesmo quando os bytes coincidem com a forma
/// atual. Sem <see cref="Unifesspa.UniPlus.Selecao.Infrastructure"/> disponível aqui (Application
/// não a alcança), a projeção lê o JSON cru e tem de recusar com um erro nomeado, nunca estourar,
/// quando as chaves novas (rotulo/tipoRenderizacao/obrigatorio/formulario/valoresSelecionaveis)
/// não existem, ou quando <c>valoresSelecionaveis</c> descumpre a bicondicional com
/// <c>tipoRenderizacao</c>.
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
        return registro;
    }

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

    [Fact(DisplayName = "Versão vigente congelada ANTES desta Story (sem formulario/campos novos) recusa com FormularioInscricao.VersaoSemApresentacao, nunca estoura")]
    public async Task Handle_EnvelopeAntigoSemApresentacao_RetornaErroNomeado()
    {
        // Forma real de uma VersaoConfiguracao congelada sob schema_version anterior a esta
        // Story: "formulario" é stub (nao_construido) e os itens de "fatosColetados" não têm
        // rotulo/tipoRenderizacao/obrigatorio — exatamente o que já existe hoje no banco
        // compartilhado de desenvolvimento (versões em 0.0.2/1.2/1.3/1.4).
        const string envelopeAntigo = """
            {
              "formulario": {"status": "nao_construido"},
              "gruposColetados": [], "fatosColetados": [
                {"fatoCodigo": "COR_RACA", "ordem": 0, "precondicao": null}
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelopeAntigo);

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsFailure.Should().BeTrue("um envelope sem as chaves novas não pode ser interpretado como formulário vazio nem estourar — é um estado nomeado");
        resultado.Error!.Code.Should().Be("FormularioInscricao.VersaoSemApresentacao");
    }

    [Theory(DisplayName = "Envelope com valor de tipo/nulidade incoerente (só alcançável por linha adulterada) recusa com o mesmo erro nomeado, nunca estoura")]
    [InlineData(
        // Item sem "finalidade": não pode aparecer no formulário de uma finalidade qualquer.
        """{"formularios":[{"finalidade":"INSCRICAO","faseId":null,"titulo":null,"modeloOrigem":null,"etapas":[],"termos":[]}],"fatosColetados":[{"fatoCodigo":"COR_RACA","etapaCodigo":null,"formato":null,"ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatoriedade":{"tipo":"SEMPRE","predicado":null},"ajuda":null,"pedirConfirmacao":false,"restricoes":[],"precondicao":null,"valoresSelecionaveis":[]}]}""")]
    [InlineData(
        // "pedirConfirmacao" como texto em vez de booleano.
        """{"formularios":[{"finalidade":"INSCRICAO","faseId":null,"titulo":null,"modeloOrigem":null,"etapas":[],"termos":[]}],"fatosColetados":[{"fatoCodigo":"COR_RACA","finalidade":"INSCRICAO","etapaCodigo":null,"formato":null,"ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatoriedade":{"tipo":"SEMPRE","predicado":null},"ajuda":null,"pedirConfirmacao":"true","restricoes":[],"precondicao":null,"valoresSelecionaveis":[]}]}""")]
    [InlineData(
        // "rotulo" presente mas null — chave existe, valor não é o esperado.
        """{"formularios":[{"finalidade":"INSCRICAO","faseId":null,"titulo":null,"modeloOrigem":null,"etapas":[],"termos":[]}],"fatosColetados":[{"fatoCodigo":"COR_RACA","finalidade":"INSCRICAO","etapaCodigo":null,"formato":null,"ordem":0,"rotulo":null,"tipoRenderizacao":"SELECAO_UNICA","obrigatoriedade":{"tipo":"NUNCA","predicado":null},"ajuda":null,"pedirConfirmacao":false,"restricoes":[],"precondicao":null,"valoresSelecionaveis":[]}]}""")]
    [InlineData(
        // "precondicao" presente com tipo errado (objeto em vez de array) — não pode virar
        // silenciosamente "sem pré-condição", que mudaria a semântica do campo.
        """{"formularios":[{"finalidade":"INSCRICAO","faseId":null,"titulo":null,"modeloOrigem":null,"etapas":[],"termos":[]}],"fatosColetados":[{"fatoCodigo":"COR_RACA","finalidade":"INSCRICAO","etapaCodigo":null,"formato":null,"ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatoriedade":{"tipo":"NUNCA","predicado":null},"ajuda":null,"pedirConfirmacao":false,"restricoes":[],"precondicao":{},"valoresSelecionaveis":[]}]}""")]
    [InlineData(
        // "valoresSelecionaveis" null num fato de seleção — descumpre a bicondicional (issue
        // #1059): SELECAO_UNICA/SELECAO_MULTIPLA exige array, nunca null.
        """{"formularios":[{"finalidade":"INSCRICAO","faseId":null,"titulo":null,"modeloOrigem":null,"etapas":[],"termos":[]}],"fatosColetados":[{"fatoCodigo":"COR_RACA","finalidade":"INSCRICAO","etapaCodigo":null,"formato":null,"ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatoriedade":{"tipo":"NUNCA","predicado":null},"ajuda":null,"pedirConfirmacao":false,"restricoes":[],"precondicao":null,"valoresSelecionaveis":null}]}""")]
    [InlineData(
        // "valoresSelecionaveis" ausente — envelope congelado antes de a chave existir.
        """{"formularios":[{"finalidade":"INSCRICAO","faseId":null,"titulo":null,"modeloOrigem":null,"etapas":[],"termos":[]}],"fatosColetados":[{"fatoCodigo":"COR_RACA","finalidade":"INSCRICAO","etapaCodigo":null,"formato":null,"ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatoriedade":{"tipo":"NUNCA","predicado":null},"ajuda":null,"pedirConfirmacao":false,"restricoes":[],"precondicao":null}]}""")]
    [InlineData(
        // "tipoRenderizacao" fora do vocabulário, com valoresSelecionaveis null — o
        // decoder converteria o token em TipoRenderizacao.Nenhuma e FatoColetado.Criar recusaria;
        // sem esta guarda aqui, o token desconhecido cairia no ramo "não é seleção" por omissão.
        """{"formularios":[{"finalidade":"INSCRICAO","faseId":null,"titulo":null,"modeloOrigem":null,"etapas":[],"termos":[]}],"fatosColetados":[{"fatoCodigo":"COR_RACA","finalidade":"INSCRICAO","etapaCodigo":null,"formato":null,"ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"DATA_HORA","obrigatoriedade":{"tipo":"NUNCA","predicado":null},"ajuda":null,"pedirConfirmacao":false,"restricoes":[],"precondicao":null,"valoresSelecionaveis":null}]}""")]
    [InlineData(
        // "ordem" negativa dentro de um item de valoresSelecionaveis — o decoder recusa.
        """{"formularios":[{"finalidade":"INSCRICAO","faseId":null,"titulo":null,"modeloOrigem":null,"etapas":[],"termos":[]}],"fatosColetados":[{"fatoCodigo":"COR_RACA","finalidade":"INSCRICAO","etapaCodigo":null,"formato":null,"ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatoriedade":{"tipo":"NUNCA","predicado":null},"ajuda":null,"pedirConfirmacao":false,"restricoes":[],"precondicao":null,"valoresSelecionaveis":[{"valorCodigo":"BRANCA","descricao":null,"ordem":-1}]}]}""")]
    [InlineData(
        // "valorCodigo" repetido no array — o decoder recusa (o encoder nunca emite duas entradas
        // para o mesmo valor).
        """{"formularios":[{"finalidade":"INSCRICAO","faseId":null,"titulo":null,"modeloOrigem":null,"etapas":[],"termos":[]}],"fatosColetados":[{"fatoCodigo":"COR_RACA","finalidade":"INSCRICAO","etapaCodigo":null,"formato":null,"ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatoriedade":{"tipo":"NUNCA","predicado":null},"ajuda":null,"pedirConfirmacao":false,"restricoes":[],"precondicao":null,"valoresSelecionaveis":[{"valorCodigo":"BRANCA","descricao":null,"ordem":0},{"valorCodigo":"BRANCA","descricao":null,"ordem":1}]}]}""")]
    [InlineData(
        // "formato" ausente — o encoder sempre o emite, nulo fora do campo de texto.
        """{"formularios":[{"finalidade":"INSCRICAO","faseId":null,"titulo":null,"modeloOrigem":null,"etapas":[],"termos":[]}],"fatosColetados":[{"fatoCodigo":"COR_RACA","finalidade":"INSCRICAO","etapaCodigo":null,"ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatoriedade":{"tipo":"NUNCA","predicado":null},"ajuda":null,"pedirConfirmacao":false,"restricoes":[],"precondicao":null,"valoresSelecionaveis":[{"valorCodigo":"BRANCA","descricao":null,"ordem":0}]}]}""")]
    [InlineData(
        // Restrição de tipo desconhecido — não pode ser descartada em silêncio, que liberaria o valor.
        """{"formularios":[{"finalidade":"INSCRICAO","faseId":null,"titulo":null,"modeloOrigem":null,"etapas":[],"termos":[]}],"fatosColetados":[{"fatoCodigo":"COR_RACA","finalidade":"INSCRICAO","etapaCodigo":null,"formato":null,"ordem":0,"rotulo":"Cor ou raça","tipoRenderizacao":"SELECAO_UNICA","obrigatoriedade":{"tipo":"NUNCA","predicado":null},"ajuda":null,"pedirConfirmacao":false,"restricoes":[{"tipo":"REGEX"}],"precondicao":null,"valoresSelecionaveis":[{"valorCodigo":"BRANCA","descricao":null,"ordem":0}]}]}""")]
    [InlineData(
        // Campo de texto sem formato — o encoder sempre emite o formato do catálogo no campo de texto.
        """{"formularios":[{"finalidade":"INSCRICAO","faseId":null,"titulo":null,"modeloOrigem":null,"etapas":[],"termos":[]}],"fatosColetados":[{"fatoCodigo":"NOME_SOCIAL","finalidade":"INSCRICAO","etapaCodigo":null,"formato":null,"ordem":0,"rotulo":"Nome social","tipoRenderizacao":"TEXTO","obrigatoriedade":{"tipo":"NUNCA","predicado":null},"ajuda":null,"pedirConfirmacao":false,"restricoes":[],"precondicao":null,"valoresSelecionaveis":null}]}""")]
    [InlineData(
        // Formato num campo que não é de texto.
        """{"formularios":[{"finalidade":"INSCRICAO","faseId":null,"titulo":null,"modeloOrigem":null,"etapas":[],"termos":[]}],"fatosColetados":[{"fatoCodigo":"BAIXA_RENDA","finalidade":"INSCRICAO","etapaCodigo":null,"formato":"CPF","ordem":0,"rotulo":"Baixa renda","tipoRenderizacao":"BOOLEANO","obrigatoriedade":{"tipo":"NUNCA","predicado":null},"ajuda":null,"pedirConfirmacao":false,"restricoes":[],"precondicao":null,"valoresSelecionaveis":null}]}""")]
    public async Task Handle_EnvelopeComValorIncoerente_RecusaSemEstourar(string envelopeJson)
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelopeJson);

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormularioInscricao.VersaoSemApresentacao");
    }

    [Fact(DisplayName = "Com dois formulários, cada finalidade serve só os seus itens e termos, e a finalidade sem formulário é 404")]
    public async Task Handle_DoisFormularios_CadaFinalidadeServeOsSeus()
    {
        const string etapas = """
            [{"codigo": "DADOS", "ordem": 0, "tipo": "SECAO", "bloco": null, "titulo": "Dados", "descricao": null, "aviso": null},
             {"codigo": "REVISAO", "ordem": 1, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE", "titulo": "Revisão e aceite", "descricao": null, "aviso": null}]
            """;
        string envelope = $$"""
            {
              "formularios": [
                {"finalidade": "INSCRICAO", "faseId": null, "titulo": "Inscrição", "modeloOrigem": null, "etapas": {{etapas}}, "termos": [{{Termo("CIENCIA_EDITAL")}}]},
                {"finalidade": "HABILITACAO", "faseId": null, "titulo": "Habilitação", "modeloOrigem": null, "etapas": {{etapas}}, "termos": [{{Termo("VERACIDADE")}}]}
              ],
              "gruposColetados": [], "fatosColetados": [
                {{Item("COR_RACA", "INSCRICAO")}},
                {{Item("CERTIFICADO_EMITIDO", "HABILITACAO")}}
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelope);

        Result<FormularioRenderizavelDto> inscricao = await HandleAsync(repository, processoId);
        Result<FormularioRenderizavelDto> habilitacao = await HandleAsync(repository, processoId, finalidade: FinalidadeFormulario.Habilitacao);
        Result<FormularioRenderizavelDto> isencao = await HandleAsync(repository, processoId, finalidade: FinalidadeFormulario.IsencaoTaxa);

        inscricao.IsSuccess.Should().BeTrue(inscricao.Error?.Message);
        habilitacao.IsSuccess.Should().BeTrue(habilitacao.Error?.Message);
        inscricao.Value!.FatosColetados.Select(static f => f.FatoCodigo).Should().Equal("COR_RACA");
        inscricao.Value!.Termos.Select(static t => t.Codigo).Should().Equal("CIENCIA_EDITAL");
        habilitacao.Value!.FatosColetados.Select(static f => f.FatoCodigo).Should().Equal("CERTIFICADO_EMITIDO");
        habilitacao.Value!.Termos.Select(static t => t.Codigo).Should().Equal("VERACIDADE");
        isencao.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado", "a versão vigente não tem formulário de isenção");

        static string Termo(string codigo) => $$"""
            {"codigo": "{{codigo}}", "ordem": 0, "termoId": "0199a000-0000-7000-8000-00000000a001", "versaoId": "0199a000-0000-7000-8000-00000000b001",
             "nome": "Termo", "texto": "Texto", "baseLegal": "Base legal", "formaAceite": "REGISTRO_DIGITAL_SEM_LOG_IP", "hashVersao": "aaaa",
             "exibicao": null, "obrigatoriedade": {"tipo": "SEMPRE", "predicado": null} }
            """;

        static string Item(string fato, string finalidade) => $$"""
            {"fatoCodigo": "{{fato}}", "finalidade": "{{finalidade}}", "etapaCodigo": "DADOS", "formato": null, "ordem": 0, "rotulo": "{{fato}}",
             "tipoRenderizacao": "BOOLEANO", "obrigatoriedade": {"tipo": "SEMPRE", "predicado": null}, "ajuda": null, "pedirConfirmacao": false, "restricoes": [], "precondicao": null, "valoresSelecionaveis": null}
            """;
    }

    [Fact(DisplayName = "O bloco de comprovação documental lista as exigências da fase do formulário, na forma pública; sem o bloco, nada")]
    public async Task Handle_BlocoDeComprovacao_ListaAsExigenciasDaFaseDoFormulario()
    {
        const string faseHabilitacao = "0199a000-0000-7000-8000-0000000000f2";
        string envelope = $$"""
            {
              "formularios": [
                {"finalidade": "HABILITACAO", "faseId": "{{faseHabilitacao}}", "titulo": null, "modeloOrigem": null, "termos": [],
                 "etapas": [
                   {"codigo": "DOCUMENTOS", "ordem": 0, "tipo": "BLOCO", "bloco": "COMPROVACAO_DOCUMENTAL", "titulo": "Documentos", "descricao": null, "aviso": null},
                   {"codigo": "REVISAO", "ordem": 1, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE", "titulo": "Revisão e aceite", "descricao": null, "aviso": null}]},
                {"finalidade": "INSCRICAO", "faseId": "0199a000-0000-7000-8000-0000000000f1", "titulo": null, "modeloOrigem": null, "termos": [],
                 "etapas": [{"codigo": "REVISAO", "ordem": 0, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE", "titulo": "Revisão e aceite", "descricao": null, "aviso": null}]}
              ],
              "gruposColetados": [], "fatosColetados": [],
              "documentosExigidos": {"exigencias": [
                {{Exigencia("Documento de identidade", "Geral", faseHabilitacao)}},
                {{Exigencia("Comprovante de inscrição", "Geral", "0199a000-0000-7000-8000-0000000000f1")}},
                {{Exigencia("Autodeclaração étnico-racial", "Condicional", faseHabilitacao)}}
              ]}
            }
            """;
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelope);

        Result<FormularioRenderizavelDto> habilitacao = await HandleAsync(repository, processoId, finalidade: FinalidadeFormulario.Habilitacao);
        Result<FormularioRenderizavelDto> inscricao = await HandleAsync(repository, processoId);

        habilitacao.Value!.ComprovacaoDocumental!.Select(static e => (e.Rotulo, e.Aplicabilidade)).Should().Equal(
            [("Documento de identidade", "Geral"), ("Autodeclaração étnico-racial", "Condicional")],
            "só as exigências da fase da habilitação, na ordem congelada");
        inscricao.Value!.ComprovacaoDocumental.Should().BeNull("o formulário de inscrição não tem o bloco");

        static string Exigencia(string nome, string aplicabilidade, string fase) => $$"""
            {"tipoDocumentoNome": "{{nome}}", "aplicabilidade": "{{aplicabilidade}}", "obrigatorio": true, "exigidoNaFaseId": "{{fase}}",
             "formatosPermitidos": {"lista": null, "qualquer": true} }
            """;
    }

    [Theory(DisplayName = "Com o bloco de comprovação, fase ausente ou malformada no formulário ou na exigência recusa a versão em vez de omitir documento")]
    [InlineData("null", "\"0199a000-0000-7000-8000-0000000000f2\"")]
    [InlineData("\"0199a000-0000-7000-8000-0000000000f2\"", "null")]
    [InlineData("\"0199a000-0000-7000-8000-0000000000f2\"", "\"nao-e-guid\"")]
    public async Task Handle_BlocoDeComprovacaoComFaseAusente_Recusa(string faseDoFormulario, string faseDaExigencia)
    {
        string envelope = $$"""
            {
              "formularios": [{"finalidade": "HABILITACAO", "faseId": {{faseDoFormulario}}, "titulo": null, "modeloOrigem": null, "termos": [],
                "etapas": [
                  {"codigo": "DOCUMENTOS", "ordem": 0, "tipo": "BLOCO", "bloco": "COMPROVACAO_DOCUMENTAL", "titulo": "Documentos", "descricao": null, "aviso": null},
                  {"codigo": "REVISAO", "ordem": 1, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE", "titulo": "Revisão e aceite", "descricao": null, "aviso": null}]}],
              "gruposColetados": [], "fatosColetados": [],
              "documentosExigidos": {"exigencias": [
                {"tipoDocumentoNome": "Documento de identidade", "aplicabilidade": "Geral", "obrigatorio": true, "exigidoNaFaseId": {{faseDaExigencia}},
                 "formatosPermitidos": {"lista": null, "qualquer": true} }
              ]}
            }
            """;
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(
            MockComVersaoVigente(processoId, envelope), processoId, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.Error!.Code.Should().Be("FormularioInscricao.VersaoSemApresentacao");
    }

    [Fact(DisplayName = "Item com obrigatoriedade QUANDO é projetado com o predicado, a ajuda e o pedido de confirmação")]
    public async Task Handle_ObrigatoriedadeQuando_ProjetaRegras()
    {
        const string envelope = """
            {
              "formularios": [{"finalidade": "INSCRICAO", "faseId": null, "titulo": null, "modeloOrigem": null, "termos": [],
                "etapas": [
                  {"codigo": "DADOS", "ordem": 0, "tipo": "SECAO", "bloco": null, "titulo": "Dados", "descricao": null, "aviso": null},
                  {"codigo": "REVISAO", "ordem": 1, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE", "titulo": "Revisão e aceite", "descricao": null, "aviso": null}
                ]}],
              "gruposColetados": [], "fatosColetados": [
                {"fatoCodigo": "BAIXA_RENDA", "finalidade": "INSCRICAO", "etapaCodigo": "DADOS", "formato": null, "ordem": 1,
                 "rotulo": "Baixa renda", "tipoRenderizacao": "BOOLEANO",
                 "obrigatoriedade": {"tipo": "QUANDO", "predicado": [[{"fato": "COR_RACA", "operador": "IGUAL", "valor": "PRETA"}]]},
                 "ajuda": "Renda por pessoa da família", "pedirConfirmacao": true, "restricoes": [], "precondicao": null, "valoresSelecionaveis": null}
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(MockComVersaoVigente(processoId, envelope), processoId);

        FatoFormularioRenderizavelDto fato = resultado.Value!.FatosColetados.Should().ContainSingle().Which;
        fato.Obrigatoriedade.Tipo.Should().Be("QUANDO");
        fato.Obrigatoriedade.Predicado.Should().ContainSingle().Which.Should().ContainSingle().Which.Fato.Should().Be("COR_RACA");
        fato.Ajuda.Should().Be("Renda por pessoa da família");
        fato.PedirConfirmacao.Should().BeTrue();
    }

    [Fact(DisplayName = "O grupo repetível da finalidade é projetado com as regras e os campos de cada ocorrência, sem máximo quando não o declara e com o candidato como membro")]
    public async Task Handle_GrupoRepetivel_ProjetaComOsCampos()
    {
        const string envelope = """
            {
              "formularios": [{"finalidade": "HABILITACAO", "faseId": null, "titulo": null, "modeloOrigem": null, "termos": [],
                "etapas": [
                  {"codigo": "DADOS", "ordem": 0, "tipo": "SECAO", "bloco": null, "titulo": "Dados", "descricao": null, "aviso": null},
                  {"codigo": "REVISAO", "ordem": 1, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE", "titulo": "Revisão e aceite", "descricao": null, "aviso": null}
                ]}],
              "fatosColetados": [],
              "gruposColetados": [
                {"codigo": "COMPOSICAO_FAMILIAR", "finalidade": "HABILITACAO", "etapaCodigo": "DADOS", "ordem": 0, "rotulo": "Composição familiar",
                 "minimo": 0, "maximo": null, "incluiCandidato": true, "exibicao": [[{"fato": "COR_RACA", "operador": "IGUAL", "valor": "PRETA"}]],
                 "obrigatoriedade": {"tipo": "NUNCA", "predicado": null},
                 "subitens": [
                   {"fatoCodigo": "MENOR_SOB_GUARDA", "finalidade": "HABILITACAO", "etapaCodigo": null, "formato": null, "ordem": 0,
                    "rotulo": "Menor sob guarda", "tipoRenderizacao": "BOOLEANO", "obrigatoriedade": {"tipo": "SEMPRE", "predicado": null},
                    "ajuda": null, "pedirConfirmacao": false, "restricoes": [], "precondicao": null, "valoresSelecionaveis": null}
                 ]},
                {"codigo": "OUTRO", "finalidade": "INSCRICAO", "etapaCodigo": "DADOS", "ordem": 0, "rotulo": "Outro",
                 "minimo": 0, "maximo": 1, "incluiCandidato": false, "exibicao": null, "obrigatoriedade": {"tipo": "NUNCA", "predicado": null}, "subitens": []}
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(
            MockComVersaoVigente(processoId, envelope), processoId, finalidade: FinalidadeFormulario.Habilitacao);

        GrupoFormularioRenderizavelDto grupo = resultado.Value!.Grupos.Should().ContainSingle("só o grupo da finalidade pedida").Which;
        grupo.Codigo.Should().Be("COMPOSICAO_FAMILIAR");
        grupo.Maximo.Should().BeNull();
        grupo.IncluiCandidato.Should().BeTrue();
        grupo.Exibicao.Should().ContainSingle().Which.Should().ContainSingle().Which.Fato.Should().Be("COR_RACA");
        grupo.Subitens.Should().ContainSingle().Which.FatoCodigo.Should().Be("MENOR_SOB_GUARDA");
    }

    [Theory(DisplayName = "Campo de grupo com outra finalidade ou com seção própria não tem apresentação — o campo segue o grupo")]
    [InlineData("\"finalidade\": \"INSCRICAO\", \"etapaCodigo\": null")]
    [InlineData("\"finalidade\": \"HABILITACAO\", \"etapaCodigo\": \"DADOS\"")]
    public async Task Handle_CampoDeGrupoForaDoGrupo_VersaoSemApresentacao(string donoDoCampo)
    {
        string envelope = $$"""
            {
              "formularios": [{"finalidade": "HABILITACAO", "faseId": null, "titulo": null, "modeloOrigem": null, "termos": [],
                "etapas": [
                  {"codigo": "DADOS", "ordem": 0, "tipo": "SECAO", "bloco": null, "titulo": "Dados", "descricao": null, "aviso": null}
                ]}],
              "fatosColetados": [],
              "gruposColetados": [
                {"codigo": "COMPOSICAO_FAMILIAR", "finalidade": "HABILITACAO", "etapaCodigo": "DADOS", "ordem": 0, "rotulo": "Composição familiar",
                 "minimo": 0, "maximo": 10, "incluiCandidato": false, "exibicao": null, "obrigatoriedade": {"tipo": "NUNCA", "predicado": null},
                 "subitens": [
                   {"fatoCodigo": "MENOR_SOB_GUARDA", {{donoDoCampo}}, "formato": null, "ordem": 0,
                    "rotulo": "Menor sob guarda", "tipoRenderizacao": "BOOLEANO", "obrigatoriedade": {"tipo": "SEMPRE", "predicado": null},
                    "ajuda": null, "pedirConfirmacao": false, "restricoes": [], "precondicao": null, "valoresSelecionaveis": null}
                 ]}
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(
            MockComVersaoVigente(processoId, envelope), processoId, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.Error!.Code.Should().Be("FormularioInscricao.VersaoSemApresentacao");
    }

    [Theory(DisplayName = "Grupo com contagem impossível ou sem campos não tem apresentação")]
    [InlineData(5, 2, true)]
    [InlineData(0, 0, true)]
    [InlineData(0, 3, false)]
    public async Task Handle_GrupoImpossivel_VersaoSemApresentacao(int minimo, int maximo, bool comCampo)
    {
        string campo = """
            {"fatoCodigo": "MENOR_SOB_GUARDA", "finalidade": "HABILITACAO", "etapaCodigo": null, "formato": null, "ordem": 0,
             "rotulo": "Menor sob guarda", "tipoRenderizacao": "BOOLEANO", "obrigatoriedade": {"tipo": "SEMPRE", "predicado": null},
             "ajuda": null, "pedirConfirmacao": false, "restricoes": [], "precondicao": null, "valoresSelecionaveis": null}
            """;
        string envelope = $$"""
            {
              "formularios": [{"finalidade": "HABILITACAO", "faseId": null, "titulo": null, "modeloOrigem": null, "termos": [],
                "etapas": [
                  {"codigo": "DADOS", "ordem": 0, "tipo": "SECAO", "bloco": null, "titulo": "Dados", "descricao": null, "aviso": null}
                ]}],
              "fatosColetados": [],
              "gruposColetados": [
                {"codigo": "COMPOSICAO_FAMILIAR", "finalidade": "HABILITACAO", "etapaCodigo": "DADOS", "ordem": 0, "rotulo": "Composição familiar",
                 "minimo": {{minimo}}, "maximo": {{maximo}}, "incluiCandidato": false, "exibicao": null, "obrigatoriedade": {"tipo": "NUNCA", "predicado": null},
                 "subitens": [{{(comCampo ? campo : string.Empty)}}]}
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(
            MockComVersaoVigente(processoId, envelope), processoId, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.Error!.Code.Should().Be("FormularioInscricao.VersaoSemApresentacao");
    }

    [Fact(DisplayName = "Grupo sem a chave do máximo não tem apresentação — só o máximo nulo explícito é grupo sem limite")]
    public async Task Handle_GrupoSemChaveDoMaximo_VersaoSemApresentacao()
    {
        const string envelope = """
            {
              "formularios": [{"finalidade": "HABILITACAO", "faseId": null, "titulo": null, "modeloOrigem": null, "termos": [],
                "etapas": [
                  {"codigo": "DADOS", "ordem": 0, "tipo": "SECAO", "bloco": null, "titulo": "Dados", "descricao": null, "aviso": null}
                ]}],
              "fatosColetados": [],
              "gruposColetados": [
                {"codigo": "COMPOSICAO_FAMILIAR", "finalidade": "HABILITACAO", "etapaCodigo": "DADOS", "ordem": 0, "rotulo": "Composição familiar",
                 "minimo": 0, "incluiCandidato": false, "exibicao": null, "obrigatoriedade": {"tipo": "NUNCA", "predicado": null},
                 "subitens": [
                   {"fatoCodigo": "MENOR_SOB_GUARDA", "finalidade": "HABILITACAO", "etapaCodigo": null, "formato": null, "ordem": 0,
                    "rotulo": "Menor sob guarda", "tipoRenderizacao": "BOOLEANO", "obrigatoriedade": {"tipo": "SEMPRE", "predicado": null},
                    "ajuda": null, "pedirConfirmacao": false, "restricoes": [], "precondicao": null, "valoresSelecionaveis": null}
                 ]}
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(
            MockComVersaoVigente(processoId, envelope), processoId, finalidade: FinalidadeFormulario.Habilitacao);

        resultado.Error!.Code.Should().Be("FormularioInscricao.VersaoSemApresentacao");
    }

    [Fact(DisplayName = "Envelope sem o bloco de grupos não tem apresentação")]
    public async Task Handle_SemBlocoDeGrupos_VersaoSemApresentacao()
    {
        const string envelope = """
            {
              "formularios": [{"finalidade": "INSCRICAO", "faseId": null, "titulo": null, "modeloOrigem": null, "termos": [],
                "etapas": [
                  {"codigo": "REVISAO", "ordem": 0, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE", "titulo": "Revisão e aceite", "descricao": null, "aviso": null}
                ]}],
              "fatosColetados": []
            }
            """;
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(MockComVersaoVigente(processoId, envelope), processoId);

        resultado.Error!.Code.Should().Be("FormularioInscricao.VersaoSemApresentacao");
    }

    [Fact(DisplayName = "As restrições de valor congeladas são projetadas por tipo, com os limites e os grupos de opções")]
    public async Task Handle_Restricoes_ProjetaPorTipo()
    {
        const string envelope = """
            {
              "formularios": [{"finalidade": "INSCRICAO", "faseId": null, "titulo": null, "modeloOrigem": null, "termos": [],
                "etapas": [
                  {"codigo": "DADOS", "ordem": 0, "tipo": "SECAO", "bloco": null, "titulo": "Dados", "descricao": null, "aviso": null},
                  {"codigo": "REVISAO", "ordem": 1, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE", "titulo": "Revisão e aceite", "descricao": null, "aviso": null}
                ]}],
              "gruposColetados": [], "fatosColetados": [
                {"fatoCodigo": "IDADE", "finalidade": "INSCRICAO", "etapaCodigo": "DADOS", "formato": null, "ordem": 0,
                 "rotulo": "Idade", "tipoRenderizacao": "NUMERO", "obrigatoriedade": {"tipo": "SEMPRE", "predicado": null},
                 "ajuda": null, "pedirConfirmacao": false, "precondicao": null, "valoresSelecionaveis": null,
                 "restricoes": [{"tipo": "FAIXA_NUMERICA", "minimo": "16.0000", "maximo": null}]},
                {"fatoCodigo": "COR_RACA", "finalidade": "INSCRICAO", "etapaCodigo": "DADOS", "formato": null, "ordem": 1,
                 "rotulo": "Cor ou raça", "tipoRenderizacao": "SELECAO_UNICA", "obrigatoriedade": {"tipo": "SEMPRE", "predicado": null},
                 "ajuda": null, "pedirConfirmacao": false, "precondicao": null,
                 "valoresSelecionaveis": [{"valorCodigo": "PRETA", "descricao": null, "ordem": 0}],
                 "restricoes": [{"tipo": "OPCOES_PERMITIDAS", "entradas": [
                   {"quando": [[{"fato": "IDADE", "operador": "MAIOR_IGUAL", "valor": 18}]], "valores": ["PRETA"]}]}]}
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(MockComVersaoVigente(processoId, envelope), processoId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        RestricaoValorDto faixa = resultado.Value!.FatosColetados[0].Restricoes.Should().ContainSingle().Which;
        faixa.Should().BeEquivalentTo(new { Tipo = "FAIXA_NUMERICA", Minimo = 16m, Maximo = (decimal?)null });
        OpcoesCondicionadasDto grupo = resultado.Value.FatosColetados[1].Restricoes.Should().ContainSingle().Which.Entradas.Should().ContainSingle().Which;
        grupo.Valores.Should().Equal("PRETA");
        grupo.Quando.Should().ContainSingle().Which.Should().ContainSingle().Which.Fato.Should().Be("IDADE");
    }

    [Fact(DisplayName = "Campo de texto é projetado com o formato e sem valores selecionáveis")]
    public async Task Handle_CampoDeTexto_ProjetaFormato()
    {
        const string envelope = """
            {
              "formularios": [{"finalidade": "INSCRICAO", "faseId": null, "titulo": null, "modeloOrigem": null, "termos": [],
                "etapas": [
                  {"codigo": "DADOS", "ordem": 0, "tipo": "SECAO", "bloco": null, "titulo": "Dados", "descricao": null, "aviso": null},
                  {"codigo": "REVISAO", "ordem": 1, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE", "titulo": "Revisão e aceite", "descricao": null, "aviso": null}
                ]}],
              "gruposColetados": [], "fatosColetados": [
                {"fatoCodigo": "NOME_SOCIAL", "finalidade": "INSCRICAO", "etapaCodigo": "DADOS", "formato": "NOME_PESSOA", "ordem": 0,
                 "rotulo": "Nome social", "tipoRenderizacao": "TEXTO", "obrigatoriedade": {"tipo": "NUNCA", "predicado": null}, "ajuda": null, "pedirConfirmacao": false, "restricoes": [], "precondicao": null, "valoresSelecionaveis": null}
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(MockComVersaoVigente(processoId, envelope), processoId);

        FatoFormularioRenderizavelDto fato = resultado.Value!.FatosColetados.Should().ContainSingle().Which;
        fato.TipoRenderizacao.Should().Be("TEXTO");
        fato.Formato.Should().Be("NOME_PESSOA");
        fato.ValoresSelecionaveis.Should().BeNull();
    }

    [Theory(DisplayName = "Campo de data e campo de endereço são projetados com o tipo, sem formato nem valores selecionáveis")]
    [InlineData("DATA_NASCIMENTO", "DATA")]
    [InlineData("ENDERECO_RESIDENCIAL", "ENDERECO")]
    public async Task Handle_CampoDeDataOuEndereco_ProjetaOTipo(string fatoCodigo, string tipoRenderizacao)
    {
        string envelope = $$"""
            {
              "formularios": [{"finalidade": "INSCRICAO", "faseId": null, "titulo": null, "modeloOrigem": null, "termos": [],
                "etapas": [
                  {"codigo": "DADOS", "ordem": 0, "tipo": "SECAO", "bloco": null, "titulo": "Dados", "descricao": null, "aviso": null},
                  {"codigo": "REVISAO", "ordem": 1, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE", "titulo": "Revisão e aceite", "descricao": null, "aviso": null}
                ]}],
              "gruposColetados": [], "fatosColetados": [
                {"fatoCodigo": "{{fatoCodigo}}", "finalidade": "INSCRICAO", "etapaCodigo": "DADOS", "formato": null, "ordem": 0,
                 "rotulo": "Campo", "tipoRenderizacao": "{{tipoRenderizacao}}", "obrigatoriedade": {"tipo": "SEMPRE", "predicado": null}, "ajuda": null, "pedirConfirmacao": false, "restricoes": [], "precondicao": null, "valoresSelecionaveis": null}
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(MockComVersaoVigente(processoId, envelope), processoId);

        FatoFormularioRenderizavelDto fato = resultado.Value!.FatosColetados.Should().ContainSingle().Which;
        fato.TipoRenderizacao.Should().Be(tipoRenderizacao);
        fato.Formato.Should().BeNull();
        fato.ValoresSelecionaveis.Should().BeNull();
    }

    [Fact(DisplayName = "Versão vigente congelada com a forma corrente projeta título, termos e fatos com apresentação e valores selecionáveis")]
    public async Task Handle_EnvelopeCorrente_ProjetaApresentacao()
    {
        const string envelopeCorrente = """
            {
              "formularios": [{"finalidade": "INSCRICAO", "faseId": null, "titulo": "Formulário de Inscrição", "modeloOrigem": null,
                "etapas": [
                  {"codigo": "DADOS", "ordem": 0, "tipo": "SECAO", "bloco": null, "titulo": "Dados", "descricao": null, "aviso": null},
                  {"codigo": "REVISAO", "ordem": 1, "tipo": "BLOCO", "bloco": "REVISAO_E_ACEITE", "titulo": "Revisão e aceite", "descricao": null, "aviso": null}
                ],
                "termos": [
                {
                  "codigo": "DECLARACAO_PERTENCIMENTO", "ordem": 0,
                  "termoId": "0199a000-0000-7000-8000-00000000a001", "versaoId": "0199a000-0000-7000-8000-00000000b001",
                  "nome": "Declaração de pertencimento", "texto": "Declaro pertencer à comunidade.", "baseLegal": "Lei 12.711/2012",
                  "formaAceite": "REGISTRO_DIGITAL_SEM_LOG_IP", "hashVersao": "aaaa",
                  "exibicao": [[{"fato": "COR_RACA", "operador": "IGUAL", "valor": "PRETA"}]],
                  "obrigatoriedade": {"tipo": "SEMPRE", "predicado": null}
                }
              ]}],
              "gruposColetados": [], "fatosColetados": [
                {
                  "fatoCodigo": "COR_RACA", "finalidade": "INSCRICAO", "etapaCodigo": "DADOS", "formato": null, "ordem": 0, "rotulo": "Cor ou raça",
                  "tipoRenderizacao": "SELECAO_UNICA", "obrigatoriedade": {"tipo": "SEMPRE", "predicado": null}, "ajuda": null, "pedirConfirmacao": false, "restricoes": [], "precondicao": null,
                  "valoresSelecionaveis": [
                    {"valorCodigo": "BRANCA", "descricao": "Autodeclaração de cor/raça branca.", "ordem": 0},
                    {"valorCodigo": "PRETA", "descricao": "Autodeclaração de cor/raça preta.", "ordem": 1}
                  ]
                }
              ]
            }
            """;
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = MockComVersaoVigente(processoId, envelopeCorrente);

        Result<FormularioRenderizavelDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Titulo.Should().Be("Formulário de Inscrição");
        TermoExigidoDto termo = resultado.Value!.Termos.Should().ContainSingle().Which;
        termo.Texto.Should().Be("Declaro pertencer à comunidade.");
        termo.Exibicao.Should().ContainSingle().Which.Should().ContainSingle().Which.Fato.Should().Be("COR_RACA");
        termo.Obrigatoriedade.Should().Be(new ObrigatoriedadeDto("SEMPRE", null));
        FatoFormularioRenderizavelDto fato = resultado.Value!.FatosColetados.Should().ContainSingle().Which;
        fato.FatoCodigo.Should().Be("COR_RACA");
        fato.Rotulo.Should().Be("Cor ou raça");
        fato.TipoRenderizacao.Should().Be("SELECAO_UNICA");
        fato.Obrigatoriedade.Tipo.Should().Be("SEMPRE");
        fato.ValoresSelecionaveis.Should().SatisfyRespectively(
            primeiro =>
            {
                primeiro.Codigo.Should().Be("BRANCA");
                primeiro.Ordem.Should().Be(0);
            },
            segundo =>
            {
                segundo.Codigo.Should().Be("PRETA");
                segundo.Ordem.Should().Be(1);
            });
    }

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
              "formularios": [{"finalidade": "INSCRICAO", "faseId": null, "titulo": "Formulário de Inscrição", "modeloOrigem": null, "etapas": [
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
