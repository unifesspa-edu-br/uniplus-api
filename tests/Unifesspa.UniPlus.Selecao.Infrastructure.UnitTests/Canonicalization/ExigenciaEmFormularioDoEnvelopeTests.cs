namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Conferencias;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class ExigenciaEmFormularioDoEnvelopeTests
{
    private const string HashDoDocumento = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string MensagemEsperada = "uma exigência cita formulário que não está no envelope ou que é de outra fase";

    [Fact]
    public void ExigenciaEmFormularioInvalido_FormularioDaFaseDaExigencia_Aceita()
    {
        Cenario cenario = Cenario.Novo();

        CoerenciaEntreBlocos.ExigenciaEmFormularioInvalido([Cenario.Exigencia("INSCRICAO", cenario.FaseDaInscricao)], cenario.Formularios)
            .Should().BeNull();
    }

    [Fact]
    public void ExigenciaEmFormularioInvalido_SemExigencias_Aceita()
    {
        CoerenciaEntreBlocos.ExigenciaEmFormularioInvalido([], Cenario.Novo().Formularios).Should().BeNull();
    }

    [Fact]
    public void ExigenciaEmFormularioInvalido_FormularioQueOEnvelopeNaoTraz_Recusa()
    {
        Cenario cenario = Cenario.Novo();

        DomainError? recusa = CoerenciaEntreBlocos.ExigenciaEmFormularioInvalido(
            [Cenario.Exigencia("AUSENTE_DO_ENVELOPE", cenario.FaseDaInscricao)], cenario.Formularios);

        ConferirRecusa(recusa, "AUSENTE_DO_ENVELOPE");
    }

    [Fact]
    public void ExigenciaEmFormularioInvalido_FormularioDeOutraFase_Recusa()
    {
        Cenario cenario = Cenario.Novo();

        DomainError? recusa = CoerenciaEntreBlocos.ExigenciaEmFormularioInvalido(
            [Cenario.Exigencia("INSCRICAO", Guid.CreateVersion7())], cenario.Formularios);

        ConferirRecusa(recusa, "INSCRICAO");
    }

    [Fact]
    public void ExigenciaEmFormularioInvalido_FormularioSemFase_RecusaPorSerDeOutraFase()
    {
        Cenario cenario = Cenario.Novo();

        DomainError? recusa = CoerenciaEntreBlocos.ExigenciaEmFormularioInvalido(
            [Cenario.Exigencia("HABILITACAO", cenario.FaseDaInscricao)], cenario.Formularios);

        ConferirRecusa(recusa, "HABILITACAO");
    }

    [Fact]
    public void ExigenciaEmFormularioInvalido_UmaExigenciaInvalidaEntreValidas_Recusa()
    {
        Cenario cenario = Cenario.Novo();

        DomainError? recusa = CoerenciaEntreBlocos.ExigenciaEmFormularioInvalido(
            [
                Cenario.Exigencia("INSCRICAO", cenario.FaseDaInscricao),
                Cenario.Exigencia("AUSENTE_DO_ENVELOPE", cenario.FaseDaInscricao),
            ],
            cenario.Formularios);

        ConferirRecusa(recusa, "AUSENTE_DO_ENVELOPE");
    }

    [Fact]
    public void Conferir_ExigenciaEmFormularioInexistente_RecusaOEnvelopeComoMalformado()
    {
        Cenario cenario = Cenario.Novo();
        BlocosDoEnvelope blocos = cenario.Blocos(Cenario.Exigencia("AUSENTE_DO_ENVELOPE", cenario.FaseDaInscricao));

        Result<EnvelopeReidratado> resultado = CoerenciaEntreBlocos.Conferir(blocos, Cenario.Versao(), new JsonObject());

        FieldError causa = resultado.Errors.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        causa.Error.Message.Should().Contain(MensagemEsperada).And.NotContain("AUSENTE_DO_ENVELOPE");
    }

    private static void ConferirRecusa(DomainError? recusa, string codigoDoFormulario)
    {
        recusa.Should().NotBeNull();
        recusa!.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
        recusa.Message.Should().Contain(MensagemEsperada).And.NotContain(codigoDoFormulario);
    }

    private sealed record Cenario(FaseCronograma Fase, IReadOnlyList<FormularioProcesso> Formularios)
    {
        internal Guid FaseDaInscricao => Fase.Id;

        internal static Cenario Novo()
        {
            FaseCronograma fase = FormulariosDoDocumentoDeTeste.Fase(coletaInscricao: true);
            CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do([fase]);
            return new Cenario(fase, [Formulario("INSCRICAO", fase.Id, cronograma), Formulario("HABILITACAO", null, cronograma)]);
        }

        internal static DocumentoExigido Exigencia(string formularioCodigo, Guid faseId)
        {
            JsonObject exigencia = ChavesDasExigenciasNoEnvelopeTests.Exigencia();
            exigencia["formularioCodigo"] = formularioCodigo;
            exigencia["exigidoNaFaseId"] = faseId.ToString();
            LeitorEnvelope leitor = new();
            IReadOnlyList<DocumentoExigido> lidas = ChavesDasExigenciasNoEnvelopeTests.Ler(leitor, exigencia);
            leitor.Falhou.Should().BeFalse();
            return lidas.Single();
        }

        internal BlocosDoEnvelope Blocos(DocumentoExigido exigencia) => new(Configuracao(), Documentos(exigencia), null!, null, [], Coleta());

        internal static VersaoConfiguracao Versao()
        {
            SnapshotCanonico snapshot = new SnapshotPublicacaoCanonicalizer().Canonicalizar(ExemploDeConfiguracao.Entrada());
            DocumentoCongelado congelado = new(snapshot.Bytes, snapshot.SchemaVersion, snapshot.AlgoritmoHash, HashDoDocumento);
            return VersaoConfiguracao.Abrir(Guid.NewGuid(), congelado, Guid.NewGuid(), "usuario-ficticio", DateTimeOffset.UnixEpoch);
        }

        private BlocosDaConfiguracao Configuracao() => new()
        {
            Dados = null,
            HashDocumento = HashDoDocumento,
            MotivosDeDecisao = [],
            Etapas = [],
            Distribuicao = [],
            Atendimento = null,
            Fada = null,
            Cascata = null,
            Desempate = [],
            Classificacao = null,
            CronogramaFases = [Fase],
        };

        private BlocosDosDocumentos Documentos(DocumentoExigido exigencia) => new()
        {
            Conformidade = null,
            DocumentosExigidos = [exigencia],
            ReferenciaTemporalFatos = null,
            MetadadosFatosCongelados = null,
            Formularios = Formularios,
            TermosExigidos = [],
        };

        private static BlocosDeColeta Coleta() => new()
        {
            FatosColetados = [],
            ValoresSelecionaveis = new Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?>(),
            GruposColetados = [],
            AgregadosDosGrupos = [],
            RegrasDerivacao = [],
            VersaoInterpretador = CalculoDosFatosDerivados.VersaoSemantica,
            ApresentacaoDosPressupostos = new Dictionary<string, ApresentacaoDoFato>(),
            TiposDeAtoCongelados = new Dictionary<string, TipoDeAtoCongelado>(),
            FatosDerivadosCongelados = new Dictionary<string, FatoDerivadoCongelado>(),
        };

        private static FormularioProcesso Formulario(string codigo, Guid? faseId, CronogramaDoProcesso cronograma) =>
            FormularioProcesso.Criar(
                new FormularioDeclarado(CodigoDoFormulario.Criar(codigo).Value!, faseId, Obrigatorio: true),
                cronograma,
                "Formulário fictício",
                FormulariosDoDocumentoDeTeste.Paginas()).Value!;
    }
}
