namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.EtapasLidasDeTeste;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.ExigenciasLidasDeTeste;

/// <summary>
/// A exigência entra pela fábrica do domínio, com a fase, a etapa e o tipo de documento que o documento cita; a que o domínio recusa sai da
/// lista e conserva a posição de origem, que as causas das seguintes apontam, e as recusas de todas as exigências se acumulam.
/// </summary>
public sealed class LeituraDosDocumentosExigidosTests
{
    private const string Lista = "documentosExigidos.exigencias";

    [Fact(DisplayName = "A exigência adotada traz a chave, o tipo copiado do cadastro, a fase, o formulário e a obrigatoriedade do documento")]
    public void Exigencia_Adotada_TrazOQueODocumentoDeclarou()
    {
        ExigenciasLidas lidas = Ler(Exigencia(0, "identidade"));

        DocumentoExigido documento = lidas.Exigencias.DocumentosExigidos.Should().ContainSingle().Subject;
        documento.Chave.Valor.Should().Be("identidade");
        documento.TipoDocumentoCodigo.Should().Be(Identidade);
        documento.FormularioCodigo.Valor.Should().Be(Formulario);
        documento.Obrigatorio.Should().BeTrue();
        documento.ExigidoNaEtapaId.Should().BeNull();
        Causas(lidas).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase e a etapa que a exigência cita pela chave são as que a leitura adotou, com o identificador em memória")]
    public void FaseEEtapa_CitadasPelaChave_SaoAsAdotadas()
    {
        ExigenciasLidas lidas = Ler(new ExigenciasNoDocumento([Exigencia(0, "identidade", etapa: "prova")], []), Fases(), [Etapa(0, "prova", fase: FaseDeInscricao)]);

        DocumentoExigido documento = lidas.Exigencias.DocumentosExigidos.Single();
        documento.ExigidoNaFaseId.Should().NotBeEmpty();
        documento.ExigidoNaEtapaId.Should().NotBeNull().And.NotBe(documento.ExigidoNaFaseId);
        Causas(lidas).Should().BeEmpty();
    }

    [Fact(DisplayName = "O bloco ausente é o processo sem comprovação documental declarada, sem recusa")]
    public void BlocoAusente_EDeclaracaoVazia()
    {
        ExigenciasLidas lidas = Ler((ExigenciasNoDocumento?)null);

        lidas.Exigencias.DocumentosExigidos.Should().BeEmpty();
        lidas.Exigencias.RegrasDeComprovacao.Should().BeEmpty();
        lidas.Origem.PartesRecusadas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O tipo de documento inexistente recusa a exigência no campo do código, sem o texto digitado, e ela sai da lista")]
    public void TipoDeDocumento_Inexistente_RecusaAExigencia()
    {
        ExigenciasLidas lidas = Ler(Exigencia(0, "a"), Exigencia(1, "b", tipo: "TIPO-DIGITADO"), Exigencia(2, "c"));

        lidas.Exigencias.DocumentosExigidos.Select(static documento => documento.Chave.Valor).Should().Equal("a", "c");
        Causas(lidas).Should().Equal(("/documentosExigidos/exigencias/1/tipoDocumentoCodigo", ProcessoSeletivoErrorCodes.TipoDocumentoDoVinculoNaoEncontrado));
        Mensagens(lidas).Should().NotContain(mensagem => mensagem.Contains("DIGITADO", StringComparison.Ordinal));
        lidas.Origem.PartesRecusadas.Select(static parte => parte.Caminho).Should().Equal(Lista);
    }

    [Fact(DisplayName = "A exigência recusada conserva a posição de origem: a causa da seguinte aponta a posição enviada, e a origem da lista a acha")]
    public void ExigenciaRecusada_ConservaAPosicaoDeOrigemDasSeguintes()
    {
        ExigenciasLidas lidas = Ler(Exigencia(0, "a", tipo: "TIPO-DIGITADO"), Exigencia(1, "b"), Exigencia(2, "c", fase: "outra"));

        lidas.Exigencias.DocumentosExigidos.Select(static documento => documento.Chave.Valor).Should().Equal("b");
        lidas.Origem.PosicaoDeOrigem(Lista, 0).Should().Be(1);
        Causas(lidas).Should().Equal(
            ("/documentosExigidos/exigencias/0/tipoDocumentoCodigo", ProcessoSeletivoErrorCodes.TipoDocumentoDoVinculoNaoEncontrado),
            ("/documentosExigidos/exigencias/2/faseChave", "DocumentoExigido.FaseNaoPertenceAoProcesso"));
    }

    [Fact(DisplayName = "A fase inexistente, a fase ausente e a etapa inexistente recusam o campo da chave, e a fábrica não é chamada")]
    public void Fase_EEtapa_Inexistentes_RecusamOCampoQueCita()
    {
        ExigenciasLidas lidas = Ler(
            Exigencia(0, "a", fase: "digitada"),
            Exigencia(1, "b", fase: null),
            Exigencia(2, "c", etapa: "digitada"));

        lidas.Exigencias.DocumentosExigidos.Should().BeEmpty();
        Causas(lidas).Should().Equal(
            ("/documentosExigidos/exigencias/0/faseChave", "DocumentoExigido.FaseNaoPertenceAoProcesso"),
            ("/documentosExigidos/exigencias/1/faseChave", "DocumentoExigido.FaseNaoPertenceAoProcesso"),
            ("/documentosExigidos/exigencias/2/etapaChave", "DocumentoExigido.EtapaNaoPertenceAoProcesso"));
        Mensagens(lidas).Should().NotContain(mensagem => mensagem.Contains("digitada", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A citação de fase ou etapa que a leitura recusou deixa a exigência fora da lista, sem causa nova")]
    public void FaseEEtapa_RecusadasNaLeitura_NaoAcumulamCausaNova()
    {
        FaseNoDocumento[] fases = [Fase(0, FaseDeInscricao, "INSCRICAO"), Fase(1, "digitada", "FASE-DIGITADA")];
        EtapaNoDocumento[] etapas = [Etapa(0, "prova", tipo: "TIPO-DIGITADO")];

        ExigenciasLidas lidas = Ler(
            new ExigenciasNoDocumento([Exigencia(0, "a", fase: "digitada"), Exigencia(1, "b", etapa: "prova")], []), fases, etapas);

        lidas.Exigencias.DocumentosExigidos.Should().BeEmpty();
        Causas(lidas).Should().BeEmpty();
    }

    [Fact(DisplayName = "A chave sem forma de chave recusa o campo da chave, sem exceção")]
    public void Chave_Ausente_RecusaOCampoDaChave()
    {
        ExigenciasLidas lidas = Ler(Exigencia(0, null!));

        lidas.Exigencias.DocumentosExigidos.Should().BeEmpty();
        Causas(lidas).Should().Equal(("/documentosExigidos/exigencias/0/chave", ChaveLocal.CodigoDeErro));
    }

    [Fact(DisplayName = "A chave repetida que chegue à leitura vale a primeira, e as duas exigências são adotadas")]
    public void ChaveRepetida_ValeAPrimeira()
    {
        ExigenciasLidas lidas = Ler(Exigencia(0, "a"), Exigencia(1, "a", tipo: Residencia));

        lidas.Exigencias.DocumentosExigidos.Select(static documento => documento.TipoDocumentoCodigo).Should().Equal(Identidade, Residencia);
    }

    [Fact(DisplayName = "Os dados que o documento comprova são os que o autor declarou, lidos como estão")]
    public void DadosComprovados_SaoOsDeclarados()
    {
        ExigenciaNoDocumento exigencia = Exigencia(0, "identidade") with { DadosComprovados = ["nome_civil", "data_de_nascimento"] };

        ExigenciasLidas lidas = Ler(exigencia);

        lidas.Exigencias.DocumentosExigidos.Single().FatosComprovados.Should().Equal("nome_civil", "data_de_nascimento");
    }

    [Fact(DisplayName = "Os dados comprovados repetidos recusam a exigência no campo dos dados")]
    public void DadosComprovados_Repetidos_RecusamOCampo()
    {
        ExigenciasLidas lidas = Ler(Exigencia(0, "identidade") with { DadosComprovados = ["nome_civil", "nome_civil"] });

        Causas(lidas).Should().Equal(("/documentosExigidos/exigencias/0/dadosComprovados", DocumentoExigido.FatosComprovadosInvalidos));
    }

    [Fact(DisplayName = "A condição com o valor nulo escrito é lida, e a condição sem o valor é recusada como valor obrigatório")]
    public void Condicao_ValorNuloEscritoOuAusente_UmaELidaEOutraRecusada()
    {
        JsonElement nuloEscrito = JsonSerializer.SerializeToElement<string?>(null);

        ExigenciasLidas escrito = Ler(Exigencia(0, "identidade").Condicional([Condicao("SEXO", "IGUAL", nuloEscrito)]));
        ExigenciasLidas ausente = Ler(Exigencia(0, "identidade").Condicional([Condicao("SEXO", "IGUAL", null)]));

        Causas(escrito).Should().BeEmpty();
        escrito.Exigencias.DocumentosExigidos.Single().Condicoes.Should().ContainSingle();
        Causas(ausente).Should().ContainSingle().Which.Ponteiro.Should().EndWith("/valor");
    }

    [Fact(DisplayName = "O modelo de documento não é lido: a exigência é convertida sem ele")]
    public void Modelo_NaoELido()
    {
        ExigenciasLidas lidas = Ler(Exigencia(0, "identidade") with { ModeloId = Guid.CreateVersion7() });

        lidas.Exigencias.DocumentosExigidos.Single().Modelo.Should().BeNull();
        Causas(lidas).Should().BeEmpty();
    }

    [Fact(DisplayName = "As recusas independentes da mesma exigência se acumulam na ordem citações, formulário, forma e tamanho")]
    public void Recusas_Independentes_SeAcumulam()
    {
        ExigenciaNoDocumento exigencia = new(0, "a", "TIPO-DIGITADO", FaseDeInscricao, null, null) { Obrigatorio = true, AbrePrazoParaReenvio = false, TamanhoMaximoBytes = 0, FormatosPermitidos = new(true, []) };

        ExigenciasLidas lidas = Ler(exigencia);

        Causas(lidas).Select(static causa => causa.Ponteiro).Should().Equal(
            "/documentosExigidos/exigencias/0/tipoDocumentoCodigo",
            "/documentosExigidos/exigencias/0/formularioCodigo",
            "/documentosExigidos/exigencias/0/aplicabilidade",
            "/documentosExigidos/exigencias/0/tamanhoMaximoBytes");
    }

    [Fact(DisplayName = "A exigência com todos os campos ausentes é recusada sem exceção, com a causa de cada campo")]
    public void ExigenciaVazia_NaoLanca()
    {
        ExigenciasLidas lidas = Ler(new ExigenciaNoDocumento(0, "a", null, null, null, null));

        lidas.Exigencias.DocumentosExigidos.Should().BeEmpty();
        Causas(lidas).Select(static causa => causa.Ponteiro).Should().Equal(
            "/documentosExigidos/exigencias/0/faseChave",
            "/documentosExigidos/exigencias/0/tipoDocumentoCodigo",
            "/documentosExigidos/exigencias/0/formularioCodigo",
            "/documentosExigidos/exigencias/0/aplicabilidade",
            "/documentosExigidos/exigencias/0/obrigatorio",
            "/documentosExigidos/exigencias/0/abrePrazoParaReenvio",
            "/documentosExigidos/exigencias/0/formatosPermitidos");
    }

    [Fact(DisplayName = "A exigência sem a obrigatoriedade é recusada como informação obrigatória, e não vale falso")]
    public void ExigenciaSemObrigatorio_RecusaOCampo()
    {
        ExigenciasLidas lidas = Ler(Exigencia(0, "a") with { Obrigatorio = null });

        lidas.Exigencias.DocumentosExigidos.Should().BeEmpty();
        Causas(lidas).Should().Equal(("/documentosExigidos/exigencias/0/obrigatorio", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "A exigência sem a escolha de prazo para reenvio é recusada como informação obrigatória, e não vale falso")]
    public void ExigenciaSemPrazoParaReenvio_RecusaOCampo()
    {
        ExigenciasLidas lidas = Ler(Exigencia(0, "a") with { AbrePrazoParaReenvio = null });

        lidas.Exigencias.DocumentosExigidos.Should().BeEmpty();
        Causas(lidas).Should().Equal(("/documentosExigidos/exigencias/0/abrePrazoParaReenvio", CodigosDeValidacao.Obrigatorio));
    }

    [Theory(DisplayName = "A exigência com falso informado nas duas escolhas é lida e grava falso")]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ExigenciaComEscolhasInformadas_GravaOQueFoiInformado(bool obrigatorio, bool abrePrazo)
    {
        ExigenciasLidas lidas = Ler(Exigencia(0, "a") with { Obrigatorio = obrigatorio, AbrePrazoParaReenvio = abrePrazo });

        DocumentoExigido documento = lidas.Exigencias.DocumentosExigidos.Should().ContainSingle().Subject;
        (documento.Obrigatorio, documento.AbrePrazoParaReenvio).Should().Be((obrigatorio, abrePrazo));
    }
}
