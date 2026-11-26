namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes.CenarioDaInscricaoGravada;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// O comprovante gravado volta do banco como foi emitido: na ordem de emissão, com os documentos, as modalidades das duas
/// opções de curso e a integridade que o hash confere; o primeiro envio é o menor instante entre os comprovantes.
/// </summary>
[Trait("Category", "Integration")]
[Collection(InscricaoSobreProcessoPublicadoColecao.Nome)]
public sealed class ComprovanteGravadoDaInscricaoTests
{
    private const string LinhasDaModalidade = """
        SELECT posicao, origem, tipo, valor_codigo FROM selecao.respostas_da_inscricao
        WHERE inscricao_id = @id AND codigo_do_dado = 'MODALIDADE' ORDER BY posicao
        """;

    private readonly CenarioDaInscricaoGravada _cenario;

    public ComprovanteGravadoDaInscricaoTests(InscricaoDbFixture fixture) => _cenario = new CenarioDaInscricaoGravada(fixture);

    [Fact(DisplayName = "O primeiro envio é o menor instante entre os comprovantes da inscrição; a inscrição sem comprovante não consta")]
    public async Task PrimeirosEnvios_DevolveOMenorInstanteESemComprovanteNaoConsta()
    {
        Inscricao reenviada = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        Inscricao semEnvio = NovaInscricao(PersonasDeTeste.KevinCarvalho, Guid.CreateVersion7());
        DateTimeOffset primeiro = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
        ComprovanteInscricao correcao = Enviar(reenviada, "000901", new ConteudoDoEnvio { Instante = primeiro.AddMinutes(30) }).Value!;
        ComprovanteInscricao envio = Enviar(reenviada, "000901", new ConteudoDoEnvio { Instante = primeiro }).Value!;
        await _cenario.GravarAsync(reenviada, correcao, envio);
        await _cenario.GravarAsync(semEnvio);

        IReadOnlyDictionary<Guid, DateTimeOffset> envios = await PrimeirosEnviosAsync([reenviada.Id, semEnvio.Id]);

        envios.Should().ContainSingle().Which.Should().Be(new KeyValuePair<Guid, DateTimeOffset>(reenviada.Id, primeiro));
    }

    [Fact(DisplayName = "O comprovante é lido do banco, na ordem de emissão: o 1 é a primeira submissão e o 2 a correção")]
    public async Task ObterComprovante_LeOsComprovantesGravados()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        DateTimeOffset primeira = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
        ComprovanteInscricao envio = Enviar(original, "000777", ComNomeSocial("Ariel", primeira)).Value!;
        ComprovanteInscricao correcao = Enviar(original, "000777", ComNomeSocial("Ariel D.", primeira.AddMinutes(5))).Value!;
        await _cenario.GravarAsync(original, correcao, envio);

        await using UniPlusDbContext contexto = _cenario.CriarDbContext();
        await ConferirOrdemDeEmissaoAsync(_cenario.Comprovantes(contexto), original.Id, correcao.HashIntegridade, primeira);
    }

    [Fact(DisplayName = "Os documentos do comprovante voltam do banco como foram emitidos e a integridade confere")]
    public async Task ComprovanteGravado_GuardaOsDocumentosEConfereAIntegridade()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        ComprovanteInscricao envio = Enviar(original, "000888", EnvioComOLaudoSatisfeito()).Value!;
        await _cenario.GravarAsync(original, envio);

        ComprovanteInscricao relido = (await _cenario.ComprovantesGravadosAsync(original.Id)).Should().ContainSingle().Subject;

        DocumentoDoComprovante documento = relido.Documentos.Should().ContainSingle().Subject;
        documento.Situacao.Should().Be(SituacaoDoDocumento.Satisfeita.Codigo());
        documento.Arquivos.Should().Equal(new ArquivoDoComprovante(new string('d', 64), 4096, "PDF"));
        relido.IntegridadeConfere().Should().BeTrue("o hash cobre a lista de documentos gravada");
    }

    [Fact(DisplayName = "O hash recalculado do comprovante relido do banco confere, mesmo com número em notação exponencial")]
    public async Task ComprovanteGravado_TemIntegridadeReproduzivel()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        ComprovanteInscricao envio = Enviar(original, "000778", new ConteudoDoEnvio
        {
            Respostas = Respostas(("NOME_SOCIAL", "Ariel")),
            Derivados = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["FAIXA_ETARIA"] = JsonDocument.Parse("1e3").RootElement.Clone() },
            Instante = new DateTimeOffset(2026, 10, 10, 12, 0, 0, 123, TimeSpan.Zero),
        }).Value!;
        await _cenario.GravarAsync(original, envio);

        IReadOnlyList<ComprovanteInscricao> gravados = await _cenario.ComprovantesGravadosAsync(original.Id);

        gravados.Should().ContainSingle().Which.IntegridadeConfere().Should().BeTrue();
    }

    [Fact(DisplayName = "A modalidade do envio é gravada calculada, uma linha por código na ordem das ofertadas, e o comprovante relido do banco a traz e confere")]
    public async Task Submeter_GravaAModalidadeEOComprovanteRelidoConfere()
    {
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        ModalidadeDeConcorrencia[] modalidades = [new("AC", "Ampla concorrência", 0), new("LI_EP", "Egressos de escola pública", 4)];
        ComprovanteInscricao envio = Enviar(original, "000901", new ConteudoDoEnvio
        {
            Respostas = Respostas(("NOME_SOCIAL", "Ariel")),
            Avaliacao = FormularioDeTeste.AvaliacaoSemOcultos with { Modalidades = ModalidadesConcorrendo(modalidades) },
        }).Value!;
        await _cenario.GravarAsync(original, envio);

        await ConferirALinhasDaModalidadeAsync(original.Id);

        ComprovanteInscricao relido = (await _cenario.ComprovantesGravadosAsync(original.Id)).Single();
        relido.Modalidades.Should().Equal(modalidades);
        relido.IntegridadeConfere().Should().BeTrue();
    }

    [Fact(DisplayName = "A 2ª opção e as modalidades dela são gravadas na inscrição e no comprovante, e o comprovante relido do banco confere")]
    public async Task Submeter_GravaASegundaOpcaoEAsModalidadesDela()
    {
        Guid segundaOferta = Guid.CreateVersion7();
        ModalidadeDeConcorrencia[] daSegunda = [new("AC", "Ampla concorrência", 0)];
        Inscricao original = NovaInscricao(PersonasDeTeste.FernandaDrumond, Guid.CreateVersion7());
        ComprovanteInscricao envio = Enviar(original, "000902", EnvioComDuasOpcoes(segundaOferta, daSegunda)).Value!;
        await _cenario.GravarAsync(original, envio);

        Inscricao lida = await _cenario.LerAtivaAsync(original, PersonasDeTeste.FernandaDrumond);
        lida.Formulario.Preenchimento.Opcoes.Segunda.Should().Be(segundaOferta);
        lida.Formulario.Derivados["MODALIDADE_SEGUNDA_OPCAO"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("AC");

        await ConferirComprovanteDaSegundaOpcaoAsync(original.Id, segundaOferta, daSegunda);
    }

    private static ConteudoDoEnvio ComNomeSocial(string nomeSocial, DateTimeOffset instante) =>
        new() { Respostas = Respostas(("NOME_SOCIAL", nomeSocial)), Instante = instante };

    private static ConteudoDoEnvio EnvioComDuasOpcoes(Guid segundaOferta, ModalidadeDeConcorrencia[] daSegunda) =>
        new()
        {
            Respostas = Respostas(("NOME_SOCIAL", "Ariel")),
            Admitidas = Admitidas with { OfertasDeCurso = new HashSet<Guid> { OfertaDoProcesso, segundaOferta }, NumeroDeOpcoesDeCurso = 2 },
            SegundaOfertaDeCurso = segundaOferta,
            Avaliacao = FormularioDeTeste.AvaliacaoSemOcultos with
            {
                Modalidades = ModalidadesConcorrendo([new("AC", "Ampla concorrência", 0), new("LI_EP", "Egressos de escola pública", 4)]),
                ModalidadesSegundaOpcao = ModalidadesConcorrendo(daSegunda),
            },
        };

    private static ModalidadesDeConcorrencia ModalidadesConcorrendo(ModalidadeDeConcorrencia[] modalidades) =>
        new(SituacaoDasModalidades.Concorre, modalidades, [], new DependenciasDaAvaliacao([], []));

    private static ConteudoDoEnvio EnvioComOLaudoSatisfeito()
    {
        DocumentoExigido laudo = LaudoMedicoExigido();

        return new()
        {
            Respostas = Respostas(("NOME_SOCIAL", "Ariel")),
            Avaliacao = FormularioDeTeste.AvaliacaoSemOcultos with { Documentos = DocumentoSatisfeito(laudo) },
            Arquivos = [ArquivoConfirmadoDoLaudo(laudo)],
        };
    }

    private static DocumentoExigido LaudoMedicoExigido() =>
        DocumentoExigido.Criar(
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("exigencia_laudo_medico").Value!,
            new DocumentoExigidoDeclarado(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "LAUDO_MEDICO", "Laudo médico", "CAT", ProtecaoDoDocumentoDeTeste.Pessoal),
                new MomentoDaExigencia(Guid.CreateVersion7(), null, FormulariosDoProcessoDeTeste.Codigo(FormulariosDoProcessoDeTeste.Inscricao)),
                new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
                new IndeferimentoDoDocumento(true, "RETIRA_O_ACRESCIMO", []),
                RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(true, null).Value!),
                null)).Value!;

    private static DocumentosDaInscricao DocumentoSatisfeito(DocumentoExigido laudo) =>
        new(
            [new DocumentoAvaliado(laudo, SituacaoDoDocumento.Satisfeita, null, null, [], null)],
            new Dictionary<Guid, IReadOnlyList<PendenciaDaAvaliacao>>(), []);

    private static ArquivoConfirmado ArquivoConfirmadoDoLaudo(DocumentoExigido laudo) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), laudo.Id, null, FormatoPermitido.Pdf, 4096, new string('d', 64), DateTimeOffset.UtcNow);

    private static async Task ConferirOrdemDeEmissaoAsync(
        ComprovanteDaInscricaoRepository leitura, Guid inscricaoId, string hashDaCorrecao, DateTimeOffset primeira)
    {
        ComprovanteNaPosicao? segundo = await leitura.ObterComprovanteAsync(inscricaoId, 2);
        ComprovanteNaPosicao? primeiro = await leitura.ObterComprovanteAsync(inscricaoId, 1);

        segundo!.Comprovante.HashIntegridade.Should().Be(hashDaCorrecao, "o 2 é a correção, emitida depois, mesmo gravada antes");
        (segundo.Numero, segundo.Total, segundo.Vale).Should().Be((2, 2, true));
        (primeiro!.NumeroSeguinte, primeiro.Vale).Should().Be((2, false));
        (await leitura.ObterComprovanteAsync(inscricaoId, 3)).Should().BeNull();
        (await leitura.ObterComprovanteAsync(inscricaoId, 0)).Should().BeNull("a numeração começa em 1");
        (await leitura.ObterComprovanteAsync(inscricaoId, -1)).Should().BeNull();
        (await leitura.ObterEmissoesAsync([inscricaoId])).Select(static e => e.Instante).Should().Equal(primeira, primeira.AddMinutes(5));
    }

    private async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> PrimeirosEnviosAsync(IReadOnlyCollection<Guid> inscricoes)
    {
        await using UniPlusDbContext contexto = _cenario.CriarDbContext();

        return await _cenario.Comprovantes(contexto).ObterPrimeirosEnviosAsync(inscricoes);
    }

    private async Task ConferirALinhasDaModalidadeAsync(Guid inscricaoId)
    {
        List<(int Posicao, string Origem, string Tipo, string Codigo)> linhas = await _cenario.ConsultarAsync(
            LinhasDaModalidade, inscricaoId, static l => (l.GetInt32(0), l.GetString(1), l.GetString(2), l.GetString(3)));

        linhas.Should().Equal(
            (0, "CALCULADA", "OPCAO", "AC"),
            (1, "CALCULADA", "OPCAO", "LI_EP"));
    }

    private async Task ConferirComprovanteDaSegundaOpcaoAsync(Guid inscricaoId, Guid segundaOferta, ModalidadeDeConcorrencia[] daSegunda)
    {
        ComprovanteInscricao relido = (await _cenario.ComprovantesGravadosAsync(inscricaoId)).Single();

        relido.OfertaCursoSegundaOpcaoId.Should().Be(segundaOferta);
        relido.ModalidadesSegundaOpcao.Should().Equal(daSegunda);
        relido.IntegridadeConfere().Should().BeTrue();
    }
}
