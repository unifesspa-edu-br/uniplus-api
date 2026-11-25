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
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
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
        string codigo, string classificacao, TipoDeCampo tipo, JsonElement? resposta,
        string? secao = "DADOS", int ordem = 0, string? formato = null) =>
        Fato(codigo, classificacao, tipo, resposta, new MetadadosDoFatoDeTeste { Secao = secao, Ordem = ordem, Formato = formato });

    public InscricaoAdministrativaDeTeste Fato(
        string codigo, string classificacao, TipoDeCampo tipo, JsonElement? resposta, MetadadosDoFatoDeTeste metadados)
    {
        _fatos.Add(CriarFato(codigo, classificacao, tipo, metadados));
        if (resposta is { } valor)
        {
            _respostas[codigo] = valor;
        }
        return this;
    }

    private static FatoColetado CriarFato(string codigo, string classificacao, TipoDeCampo tipo, MetadadosDoFatoDeTeste metadados)
    {
        CopiaDoItemNoCatalogo copia = new(
            metadados.OrigemValores, metadados.Formato ?? (tipo == TipoDeCampo.Texto ? "LIVRE" : null), classificacao, metadados.HipoteseLegal,
            ProtecaoDoFatoDeTeste.Finalidade, metadados.Descricao ?? $"Descrição de {codigo}", metadados.Componente);
        Result<FatoColetado> fato = FatoColetadoDeTeste.Coletando(codigo, metadados.Ordem)
            .ComRotulo($"Rótulo de {codigo}")
            .DoTipo(tipo)
            .ComCopia(copia)
            .NaSecao(metadados.Secao)
            .DaFinalidade(FinalidadeFormulario.Inscricao)
            .ComAjuda(metadados.Ajuda)
            .ComRestricoes(metadados.Restricoes)
            .Criar();
        return fato.IsSuccess ? fato.Value! : throw new InvalidOperationException(string.Join("; ", fato.Errors.Select(e => e.Error.Message)));
    }

    /// <summary>Um grupo repetível do formulário, com as ocorrências que o candidato respondeu.</summary>
    public InscricaoAdministrativaDeTeste Grupo(GrupoColetado grupo, params OcorrenciaDeGrupoInscricao[] ocorrencias)
    {
        _grupos.Add(grupo);
        _ocorrencias[grupo.Codigo] = ocorrencias;
        return this;
    }

    public InscricaoAdministrativaDeTeste Derivado(
        string codigo, string nome, TipoDoDado dominio, string classificacao, JsonElement valor, string hipoteseLegal = Hipotese)
    {
        _derivados[codigo] = new FatoDerivadoCongelado(codigo, nome, $"Descrição de {codigo}", null, dominio, QuantidadeDeValores.ValorUnico, classificacao, hipoteseLegal, "Finalidade");
        _derivadas[codigo] = valor;
        return this;
    }

    public InscricaoAdministrativaDeTeste Exigencia(string codigo, ClassificacaoDeProtecao classificacao, bool noEnvelope = true, bool comArquivo = true)
    {
        DocumentoExigido exigencia = CriarExigencia(codigo, classificacao);
        if (noEnvelope)
        {
            _exigencias.Add(exigencia);
        }

        if (comArquivo)
        {
            AdicionarArquivoDaExigencia(exigencia);
        }

        return this;
    }

    private DocumentoExigido CriarExigencia(string codigo, ClassificacaoDeProtecao classificacao)
    {
        return DocumentoExigidoDeTeste.Novo()
            .NaFase(_faseId)
            .ComTipo(codigo, $"Documento {codigo}", "CATEGORIA")
            .ComProtecao(ProtecaoDoDocumentoDeTeste.De(classificacao))
            .Facultativa()
            .ComFormatos(FormatosPermitidos.Criar(false, [("PDF", null)]).Value!)
            .Criar().Value!;
    }

    private void AdicionarArquivoDaExigencia(DocumentoExigido exigencia)
    {
        ApresentacaoDeDocumento apresentacao = ApresentacaoDeDocumento.Criar(InscricaoId, exigencia, null);
        ArquivoDaApresentacao arquivo = ArquivoDaApresentacao.IniciarPendente(
            apresentacao.Id, InscricaoId, exigencia, "application/pdf", 1024, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        arquivo.Confirmar("\"etag\"", TimeProvider.System);
        arquivo.RegistrarVerificado(1024, new string('c', 64), FormatoPermitido.Pdf);
        _arquivos.Add((exigencia, arquivo, apresentacao));
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
        EnvelopeReidratado envelope = EnvelopeDoCenario();
        VersaoConfiguracao versao = VersaoConfiguracao.Abrir(
            ProcessoId, new DocumentoCongelado([1], "0.0.42", "canonical-json/sha256@v1", new string('a', 64)),
            Guid.CreateVersion7(), "sub", DateTimeOffset.UnixEpoch);
        VersaoId = versao.Id;

        return new Mundo(ProcessosCom(versao), InscricoesCom(versao), DocumentosDoCenario(), CodecsQueLem(versao, envelope));
    }

    private EnvelopeReidratado EnvelopeDoCenario()
    {
        ProcessoSeletivo processo = ProcessoSeletivoConformeBuilder.Criar(NomeDoProcesso);
        ConfiguracaoPublicada configuracao = new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, []),
            new AvaliacaoDeclarada([.. processo.Etapas], [.. processo.CriteriosDesempate], processo.Classificacao!),
            new VagasDeclaradas([ProcessoSeletivoConformeBuilder.DistribuicaoAmplaConcorrencia(ofertaCursoOrigemId: OfertaCursoId)], 1, processo.BonusRegional is not null,
                processo.BonusRegional, null, processo.AtendimentoEspecializado!),
            new ExigenciasDeclaradas(_exigencias, []),
            new ColetaDeclarada(ConfiguracaoPublicadaDeTeste.CamposDaColeta(_fatos, _grupos), _grupos, [FormularioProcesso.Criar(FinalidadeFormulario.Inscricao, _faseId, null, FormularioDeTeste.Paginas()).Value!], [],
                [], [], null),
            new DivulgacaoETaxaDeclaradas(null, null), null);

        return new EnvelopeReidratado(
            configuracao, DadosEdital.Criar("001/2027", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(30), Guid.CreateVersion7()).Value!,
            new string('a', 64), "America/Sao_Paulo", retificacao: null, conformidade: null, new DadosDeCatalogoDaConfiguracaoPublicada(FatosDerivadosCongelados: _derivados));
    }

    private IProcessoSeletivoRepository ProcessosCom(VersaoConfiguracao versao)
    {
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        processos.ObterUnidadeENomeAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns(new UnidadeENomeDoProcesso(DecisoesDeAcessoDeTeste.UnidadeDona, NomeDoProcesso));
        IReadOnlyList<VersaoConfiguracao> versoes = [versao];
        processos.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(versoes);

        return processos;
    }

    private static IRegistroCodecsEnvelope CodecsQueLem(VersaoConfiguracao versao, EnvelopeReidratado envelope)
    {
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        codecs.LeAVersao(versao.SchemaVersion).Returns(true);
        codecs.Reidratar(versao).Returns(Result<EnvelopeReidratado>.Success(envelope));

        return codecs;
    }

    private IInscricaoRepository InscricoesCom(VersaoConfiguracao versao)
    {
        IInscricaoRepository inscricoes = Substitute.For<IInscricaoRepository>();
        inscricoes.ObterVinculoAsync(InscricaoId, Arg.Any<CancellationToken>())
            .Returns(new VinculoDaInscricao(InscricaoId, ProcessoId, Situacao, versao.Id, OfertaCursoId, null));
        inscricoes.ObterPorIdAsync(InscricaoId, Arg.Any<CancellationToken>()).Returns(InscricaoReidratada(versao));
        SubmeterDeTeste.ComprovantesDe(inscricoes).ObterPrimeirosEnviosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(PrimeirosEnvios());

        return inscricoes;
    }

    private Inscricao InscricaoReidratada(VersaoConfiguracao versao) => Inscricao.Reidratar(InscricaoId, ProcessoId,
            TitularDaInscricao.Reidratar(_cpf, "conta-candidato", "https://idp.test"),
            EstadoDaInscricao.Reidratar(Situacao, Situacao == SituacaoInscricao.Rascunho ? null : "000001", CancelamentoDaInscricao.Reidratar(_cancelamento?.Em, _cancelamento is null ? null : "conta-candidato", _cancelamento?.Motivo)),
            FormularioDaInscricao.Reidratar(versao.Id, PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(OfertaCursoId, null), _respostas, _ocorrencias, []),
                _derivadas, new Dictionary<string, DefinicaoDaResposta>(StringComparer.Ordinal)), 1, new Unifesspa.UniPlus.Selecao.Domain.ValueObjects.DatasDeGravacaoDaInscricao(default, null));

    private Dictionary<Guid, DateTimeOffset> PrimeirosEnvios() => _envios.Count == 0
        ? []
        : new Dictionary<Guid, DateTimeOffset> { [InscricaoId] = _envios.Min() };

    private IDocumentoDaInscricaoRepository DocumentosDoCenario()
    {
        IDocumentoDaInscricaoRepository documentos = Substitute.For<IDocumentoDaInscricaoRepository>();
        IReadOnlyList<ApresentacaoDeDocumento> apresentacoes = [.. _arquivos.Select(static a => a.Apresentacao)];
        IReadOnlyList<ArquivoDaApresentacao> arquivos = [.. _arquivos.Select(static a => a.Arquivo)];
        documentos.ListarApresentacoesDaInscricaoAsync(InscricaoId, Arg.Any<CancellationToken>()).Returns(apresentacoes);
        documentos.ListarArquivosDaInscricaoAsync(InscricaoId, Arg.Any<CancellationToken>()).Returns(arquivos);

        return documentos;
    }

    public sealed record Mundo(
        IProcessoSeletivoRepository Processos, IInscricaoRepository Inscricoes, IDocumentoDaInscricaoRepository Documentos, IRegistroCodecsEnvelope Codecs)
    {
        public IComprovanteDaInscricaoRepository Comprovantes => SubmeterDeTeste.ComprovantesDe(Inscricoes);
    }
}
