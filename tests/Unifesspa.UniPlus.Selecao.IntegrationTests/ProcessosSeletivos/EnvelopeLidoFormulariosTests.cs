namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodec;

public sealed class EnvelopeLidoFormulariosTests
{
    [Fact(DisplayName = "Dois formulários, cada um com item de ordem 0 e um termo, reproduzem os bytes")]
    public void RoundTrip_DoisFormularios_OrdemRepetidaEntreEles()
    {
        ProcessoSeletivo processo = ExemploDeProcessoSeletivoProcessoRico.ProcessoRico();
        DefinirHabilitacao(processo);

        SnapshotCanonico congelado = ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(processo));

        FidelidadeDoEnvelopeDeTeste.Conferir(processo, ExemploDeProcessoSeletivoEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        Envelope(congelado)["formularios"]!.AsArray().Select(static f => f!["codigo"]!.GetValue<string>())
            .Should().Equal("HABILITACAO", "INSCRICAO");
    }

    [Fact(DisplayName = "Item com obrigatoriedade QUANDO, ajuda e confirmação reproduz os bytes")]
    public void RoundTrip_RegrasDoItem()
    {
        ProcessoSeletivo processo = ExemploDeProcessoSeletivoProcessoRico.ProcessoRico();
        DefinirItensComBolsa(processo);

        SnapshotCanonico congelado = ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(processo));

        FidelidadeDoEnvelopeDeTeste.Conferir(processo, ExemploDeProcessoSeletivoEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        JsonObject bolsa = Envelope(congelado)["dadosDosFormularios"]!.AsArray()
            .Single(static f => f!["codigoDoDado"]!.GetValue<string>() == "TEM_BOLSA")!.AsObject();
        bolsa["obrigatoriedade"]!["tipo"]!.GetValue<string>().Should().Be("QUANDO");
        bolsa["pedirConfirmacao"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact(DisplayName = "Termo que aparece só para quem optou pelas cotas da lei reproduz os bytes")]
    public void RoundTrip_FormularioCitaAOpcaoPelasCotas()
    {
        ProcessoSeletivo processo = ExemploDeProcessoSeletivoProcessoRico.ProcessoRico();
        PredicadoDnf optou = PredicadoDnf.CriarDeCondicoesAgrupadas(
            [(0, CondicaoDnf.Criar(OpcaoPelasCotasDaLei.Fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;
        processo.DefinirTermos(
            [ExemploDeProcessoSeletivoEntrada.Termo("CIENCIA_DA_RESERVA", 0, exibicao: optou, obrigatoriedade: Obrigatoriedade.Quando(optou))]).IsSuccess.Should().BeTrue();

        SnapshotCanonico congelado = ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(processo));

        VersaoConfiguracao versao = ExemploDeProcessoSeletivoEntrada.VersaoDeAbertura(processo, congelado.Bytes);
        FidelidadeDoEnvelopeDeTeste.Conferir(processo, versao, congelado);
        Envelope(congelado)["calculosDosDados"]!.AsArray()
            .Select(static d => d!["codigoDoDado"]!.GetValue<string>())
            .Should().NotContain(OpcaoPelasCotasDaLei.Fato);
        ConferirDerivacoesDoRecortePublico(versao);
    }

    [Fact(DisplayName = "Campos de texto com formato, de data e de endereço reproduzem os bytes")]
    public void RoundTrip_CampoDeTexto()
    {
        ProcessoSeletivo processo = ExemploDeProcessoSeletivoProcessoRico.ProcessoRico();
        DefinirCamposDeTexto(processo);

        SnapshotCanonico congelado = ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(processo));

        FidelidadeDoEnvelopeDeTeste.Conferir(processo, ExemploDeProcessoSeletivoEntrada.VersaoDeAbertura(processo, congelado.Bytes), congelado);
        Envelope(congelado)["dadosDosFormularios"]!.AsArray()
            .Single(static f => f!["codigoDoDado"]!.GetValue<string>() == "NOME_SOCIAL")!["formato"]!.GetValue<string>()
            .Should().Be("NOME_PESSOA");
        Envelope(congelado)["dadosDosFormularios"]!.AsArray()
            .Select(static f => f!["tipoDeCampo"]!.GetValue<string>()).Should().Contain(["DATA", "ENDERECO"]);
    }

    [Fact(DisplayName = "A versão N>1 tem o bloco retificacao: o round-trip usa a RetificacaoDoEdital ORIGINAL, recuperada do próprio envelope")]
    public void RoundTrip_VersaoRetificada()
    {
        ProcessoSeletivo processo = ExemploDeProcessoSeletivoProcessoRico.ProcessoRico();
        RetificacaoDoEdital retificacao = new(ExemploDeProcessoSeletivo.AtoAbertura, "Correção do quadro de vagas do curso de Direito");

        SnapshotCanonico abertura = ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(processo));
        SnapshotCanonico congelado = ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(processo, retificacao));

        VersaoConfiguracao v1 = ExemploDeProcessoSeletivoEntrada.VersaoDeAbertura(processo, abertura.Bytes);
        VersaoConfiguracao v2 = ExemploDeProcessoSeletivoEntrada.VersaoDeRetificacao(v1, congelado.Bytes);

        EnvelopeReidratado envelope = FidelidadeDoEnvelopeDeTeste.Conferir(processo, v2, congelado);

        envelope.Retificacao.Should().NotBeNull("a versão N>1 carrega o bloco retificacao");
        envelope.Retificacao!.EditalRetificadoId.Should().Be(ExemploDeProcessoSeletivo.AtoAbertura);
        envelope.Retificacao.Motivo.Should().Be("Correção do quadro de vagas do curso de Direito");
    }

    private static FaseCronograma FaseDeHabilitacao(ProcessoSeletivo processo) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.Da(Guid.CreateVersion7(), "HABILITACAO", OrigemDataFase.NoEdital,
            agrupaEtapas: false, permiteComplementacao: false, coletaInscricao: false, coletaSolicitacaoIsencao: false, organizadaEmChamadas: false),
        new DecisoesDeCronogramaDaFase(processo.CronogramaFases.Max(f => f.Ordem) + 1,
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 5, 10, 18, 0, 0, TimeSpan.Zero), UnidadesDeTeste.Ceps),
        new PublicacoesDaFase([ProdutoDaFase.Criar("HABILITACAO", PapelDaPublicacaoPrevista.Definitivo)], false, null), []).Value!;

    private static void DefinirHabilitacao(ProcessoSeletivo processo)
    {
        FaseCronograma habilitacao = FaseDeHabilitacao(processo);
        processo.DefinirCronogramaFases([.. processo.CronogramaFases, habilitacao], [])
            .IsSuccess.Should().BeTrue();
        CodigoDoFormulario codigo = FormulariosDoProcessoDeTeste.Codigo(FormulariosDoProcessoDeTeste.Habilitacao);
        processo.DefinirFormulario(FormulariosDoProcessoDeTeste.Declarado(FormulariosDoProcessoDeTeste.Habilitacao, habilitacao.Id, obrigatorio: true), "Habilitação", FormulariosDoProcessoDeTeste.PaginasSemConjuntoBasico())
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(codigo, [CertificadoEmitidoSeCorOuRacaPreta()]).IsSuccess.Should().BeTrue();
        processo.DefinirTermosDoFormulario(codigo, [ExemploDeProcessoSeletivoEntrada.Termo("VERACIDADE", 0)])
            .IsSuccess.Should().BeTrue();
    }

    private static FatoColetado CertificadoEmitidoSeCorOuRacaPreta() => FatoColetadoDeTeste.Coletando("CERTIFICADO_EMITIDO", 0)
        .ComRotulo("Certificado emitido")
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ExibidoQuando(ExibicaoDeTeste.Quando("COR_RACA", Operador.Igual, "PRETA"))
        .NaSecao(FormularioDeTeste.Secao)
        .Construir();

    private static void DefinirItensComBolsa(ProcessoSeletivo processo) =>
        processo.DefinirItens(
        [
            SelecaoObrigatoria("COR_RACA", 0, "Cor ou raça").Construir(),
            SelecaoObrigatoria("RENDA", 1, "Faixa de renda familiar").Construir(),
            FatoDaBolsa(),
            FormularioDeTeste.CampoDaEdicaoDoEnem(3),
            ConcorrerEscolaPublica(4),
        ], grupos: []).IsSuccess.Should().BeTrue();

    private static FatoColetadoDeTeste SelecaoObrigatoria(string codigo, int ordem, string rotulo) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem)
            .ComRotulo(rotulo)
            .DoTipo(TipoDeCampo.EscolhaUnica)
            .ComObrigatoriedade(Obrigatoriedade.Sempre);

    private static FatoColetado ConcorrerEscolaPublica(int ordem) =>
        FatoColetadoDeTeste.Coletando(OpcaoPelasCotasDaLei.FatoConcorrerEscolaPublica, ordem)
            .ComRotulo("Deseja concorrer às vagas de escola pública?")
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .Construir();

    private static FatoColetado FatoDaBolsa() =>
        FatoColetadoDeTeste.Coletando("TEM_BOLSA", 2)
            .ComRotulo("Tem bolsa")
            .ComObrigatoriedade(Obrigatoriedade.Quando(PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!)]).Value!))
            .ComAjuda("Renda por pessoa da família")
            .PedindoConfirmacao()
            .Construir();

    private static void DefinirCamposDeTexto(ProcessoSeletivo processo) =>
        processo.DefinirItens(
        [
            SelecaoObrigatoria("COR_RACA", 0, "Cor ou raça").Construir(),
            SelecaoObrigatoria("RENDA", 1, "Faixa de renda familiar")
                .ExibidoQuando(ExibicaoDeTeste.Quando("COR_RACA", Operador.Igual, "PRETA"))
                .Construir(),
            FatoColetadoDeTeste.Coletando("NOME_SOCIAL", 2).ComRotulo("Nome social").DeTexto("NOME_PESSOA").Construir(),
            ObrigatorioDoTipo("DATA_NASCIMENTO", 3, "Data de nascimento", TipoDeCampo.Data),
            ObrigatorioDoTipo("ENDERECO_RESIDENCIAL", 4, "Endereço residencial", TipoDeCampo.Endereco),
            FormularioDeTeste.CampoDaEdicaoDoEnem(5),
            ConcorrerEscolaPublica(6),
        ], grupos: []).IsSuccess.Should().BeTrue();

    private static FatoColetado ObrigatorioDoTipo(string codigo, int ordem, string rotulo, TipoDeCampo tipo) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem)
            .ComRotulo(rotulo)
            .DoTipo(tipo)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .Construir();

    private static void ConferirDerivacoesDoRecortePublico(VersaoConfiguracao versao)
    {
        Result<EnvelopeReidratado> reidratado = ExemploDeProcessoSeletivo.Registro.Reidratar(versao);
        reidratado.IsSuccess.Should().BeTrue(reidratado.Error?.Message);
        Result<DefinicaoAvaliavel> avaliavel = DefinicaoAvaliavelDoProcesso.DoCongelado(reidratado.Value!);
        avaliavel.IsSuccess.Should().BeTrue(avaliavel.Error?.Message);
        RecorteDoFormulario recorte = RecorteDoFormulario.De(
            avaliavel.Value!.Definicao, FormulariosDoProcessoDeTeste.Codigo(FormulariosDoProcessoDeTeste.Inscricao).Valor);
        recorte.Regras.Derivacoes.Select(static d => d.FatoCodigo)
            .Should().Contain([OpcaoPelasCotasDaLei.Fato, OrigemEscolar.FatoEgresso]);
        recorte.Pressupostos.Should().NotContain(OpcaoPelasCotasDaLei.Fato);
    }
}
