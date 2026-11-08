namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O cenário da consulta administrativa: um processo publicado com os fatos e as exigências que o teste declara, e a
/// inscrição enviada de uma persona, com as respostas e os arquivos. Monta o que a versão congelou sem o codec real.
/// </summary>
internal sealed class InscricaoAdministrativaDeTeste
{
    public const string Hipotese = "CUMPRIMENTO_OBRIGACAO_LEGAL";

    private readonly List<FatoColetado> _fatos = [];
    private readonly List<GrupoColetado> _grupos = [];
    private readonly List<DocumentoExigido> _exigencias = [];
    private readonly Dictionary<string, FatoDerivadoCongelado> _derivados = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> _respostas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonElement> _derivadas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> _ocorrencias = new(StringComparer.Ordinal);
    private readonly List<(DocumentoExigido Exigencia, ArquivoDaApresentacao Arquivo, ApresentacaoDeDocumento Apresentacao)> _arquivos = [];
    private readonly List<DateTimeOffset> _envios = [];
    private (DateTimeOffset Em, string? Motivo)? _cancelamento;
    private readonly Cpf _cpf;
    private readonly Guid _faseId = Guid.CreateVersion7();

    public InscricaoAdministrativaDeTeste(Cpf cpf, SituacaoInscricao situacao = SituacaoInscricao.Submetida, Guid? processoId = null)
    {
        _cpf = cpf;
        Situacao = situacao;
        ProcessoId = processoId ?? Guid.CreateVersion7();
    }

    public Guid ProcessoId { get; }

    public Guid InscricaoId { get; } = Guid.CreateVersion7();

    public Guid VersaoId { get; private set; }

    public const string NomeDoProcesso = "PSR 2027";

    public const string NomeDaOferta = "Medicina (Bacharelado) — Campus Universitário de Marabá — Matutino e Vespertino";

    public Guid OfertaCursoId { get; } = Guid.CreateVersion7();

    public SituacaoInscricao Situacao { get; }

    public InscricaoAdministrativaDeTeste Fato(
        string codigo, string classificacao, TipoRenderizacao tipo, JsonElement? resposta, string? secao = "DADOS", int ordem = 0,
        string hipoteseLegal = Hipotese, string? formato = null, string? ajuda = null, string? componente = null, string? descricao = null,
        OrigemValoresColeta origemValores = OrigemValoresColeta.Catalogo, IReadOnlyList<RestricaoValor>? restricoes = null)
    {
        Result<FatoColetado> fato = FatoColetado.Criar(
            codigo, ordem, $"Rótulo de {codigo}", tipo, Obrigatoriedade.Nunca, null, classificacao, hipoteseLegal,
            ProtecaoDoFatoDeTeste.Finalidade, descricao ?? $"Descrição de {codigo}", origemValores, secao,
            FinalidadeFormulario.Inscricao, formato ?? (tipo == TipoRenderizacao.Texto ? "LIVRE" : null), ajuda, restricoes: restricoes, componenteExibicao: componente);
        _fatos.Add(fato.IsSuccess
            ? fato.Value!
            : throw new InvalidOperationException(string.Join("; ", fato.Errors.Select(e => e.Error.Message))));
        if (resposta is { } valor)
        {
            _respostas[codigo] = valor;
        }

        return this;
    }

    /// <summary>Um grupo repetível do formulário, com as ocorrências que o candidato respondeu.</summary>
    public InscricaoAdministrativaDeTeste Grupo(GrupoColetado grupo, params OcorrenciaDeGrupoInscricao[] ocorrencias)
    {
        _grupos.Add(grupo);
        _ocorrencias[grupo.Codigo] = ocorrencias;
        return this;
    }

    public InscricaoAdministrativaDeTeste Derivado(
        string codigo, string nome, string dominio, string classificacao, JsonElement valor, string hipoteseLegal = Hipotese)
    {
        _derivados[codigo] = new FatoDerivadoCongelado(codigo, nome, $"Descrição de {codigo}", null, dominio, "ESCALAR", classificacao, hipoteseLegal, "Finalidade");
        _derivadas[codigo] = valor;
        return this;
    }

    public InscricaoAdministrativaDeTeste Exigencia(string codigo, ClassificacaoDeProtecao classificacao, bool noEnvelope = true, bool comArquivo = true)
    {
        DocumentoExigido exigencia = DocumentoExigido.Criar(
            _faseId, Guid.CreateVersion7(), codigo, $"Documento {codigo}", "CATEGORIA", ProtecaoDoDocumentoDeTeste.De(classificacao),
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null,
            FormatosPermitidos.Criar(false, [("PDF", null)]).Value!, null).Value!;
        if (noEnvelope)
        {
            _exigencias.Add(exigencia);
        }

        if (comArquivo)
        {
            ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(InscricaoId, exigencia, null);
            ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
                apresentacao.Id, InscricaoId, exigencia, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
            arquivo.Confirmar("\"etag\"", TimeProvider.System);
            arquivo.RegistrarVerificado(1024, new string('c', 64), FormatoPermitido.Pdf);
            _arquivos.Add((exigencia, arquivo, apresentacao));
        }

        return this;
    }

    /// <summary>O instante em que o candidato enviou a inscrição; vários instantes são os reenvios.</summary>
    public InscricaoAdministrativaDeTeste EnviadaEm(params DateTimeOffset[] instantes)
    {
        _envios.AddRange(instantes);
        return this;
    }

    /// <summary>O instante e o motivo do cancelamento da inscrição cancelada; o motivo é opcional.</summary>
    public InscricaoAdministrativaDeTeste CanceladaEm(DateTimeOffset instante, string? motivo = null)
    {
        _cancelamento = (instante, motivo);
        return this;
    }

    public Guid ArquivoDe(string codigoDoDocumento) => _arquivos.Single(a => a.Exigencia.TipoDocumentoCodigo == codigoDoDocumento).Arquivo.Id;

    /// <summary>Entrega os repositórios e o codec que respondem pelo cenário montado.</summary>
    public Mundo Montar()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar(NomeDoProcesso);
        GrafoConfiguracao grafo = new(
            [.. processo.Etapas], processo.OfertaAtendimento!, [ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(ofertaCursoOrigemId: OfertaCursoId)], processo.BonusRegional,
            [.. processo.CriteriosDesempate], processo.Classificacao!, [], _exigencias, [], null,
            fatosColetados: _fatos, gruposColetados: _grupos, numeroDeOpcoesDeCurso: 1,
            formularios: [FormularioProcesso.Criar(FinalidadeFormulario.Inscricao, _faseId, null, FormularioDeTeste.Etapas()).Value!]);
        EnvelopeReidratado envelope = new(
            grafo, DadosEdital.Criar("001/2027", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(30), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null, fatosDerivadosCongelados: _derivados);

        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            ProcessoId, [1], "0.0.42", "canonical-json/sha256@v1", Guid.CreateVersion7(), new string('a', 64), "sub", DateTimeOffset.UnixEpoch);
        VersaoId = versao.Id;

        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ObterUnidadeAdministradoraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(DecisoesDeAcessoDeTeste.UnidadeDona);
        processos.ObterUnidadeENomeAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns(new UnidadeENomeDoProcesso(DecisoesDeAcessoDeTeste.UnidadeDona, NomeDoProcesso));
        IReadOnlyList<VersaoConfiguracao> versoes = [versao];
        processos.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(versoes);

        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        IReadOnlyList<CapacidadeCodec> capacidades = [new CapacidadeCodec(versao.SchemaVersion, TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null)];
        codecs.Capacidades.Returns(capacidades);
        codecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(envelope));

        Inscricao inscricao = Inscricao.Reidratar(
            InscricaoId, ProcessoId, _cpf, "sub-candidato", "https://idp.test", Situacao, versao.Id, OfertaCursoId, null,
            Situacao == SituacaoInscricao.Rascunho ? null : "000001", _cancelamento?.Em, _cancelamento is null ? null : "sub-candidato", _cancelamento?.Motivo,
            _respostas, _derivadas, _ocorrencias, [], new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal),
            revisao: 1, DateTimeOffset.UtcNow, null);

        IInscricaoRepository inscricoes = Substitute.For<IInscricaoRepository>();
        inscricoes.ObterVinculoAsync(InscricaoId, Arg.Any<CancellationToken>())
            .Returns(new VinculoDaInscricao(InscricaoId, ProcessoId, Situacao, versao.Id, OfertaCursoId, null));
        inscricoes.ObterPorIdAsync(InscricaoId, Arg.Any<CancellationToken>()).Returns(inscricao);

        IDocumentoDaInscricaoRepository documentos = Substitute.For<IDocumentoDaInscricaoRepository>();
        IReadOnlyList<ApresentacaoDeDocumento> apresentacoes = [.. _arquivos.Select(static a => a.Apresentacao)];
        IReadOnlyList<ArquivoDaApresentacao> arquivos = [.. _arquivos.Select(static a => a.Arquivo)];
        documentos.ListarApresentacoesDaInscricaoAsync(InscricaoId, Arg.Any<CancellationToken>()).Returns(apresentacoes);
        documentos.ListarArquivosDaInscricaoAsync(InscricaoId, Arg.Any<CancellationToken>()).Returns(arquivos);

        IReadOnlyDictionary<Guid, DateTimeOffset> primeirosEnvios = _envios.Count == 0
            ? new Dictionary<Guid, DateTimeOffset>()
            : new Dictionary<Guid, DateTimeOffset> { [InscricaoId] = _envios.Min() };
        inscricoes.ObterPrimeirosEnviosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(primeirosEnvios);

        return new Mundo(processos, inscricoes, documentos, codecs);
    }

    public sealed record Mundo(
        IProcessoSeletivoRepository Processos, IInscricaoRepository Inscricoes, IDocumentoDaInscricaoRepository Documentos, IRegistroCodecsEnvelope Codecs);
}
