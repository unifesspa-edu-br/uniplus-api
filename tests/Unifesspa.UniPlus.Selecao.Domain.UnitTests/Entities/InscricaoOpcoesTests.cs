namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static InscricaoDeTeste;

public sealed class InscricaoOpcoesTests
{
    [Fact(DisplayName = "Uma oferta de curso que não é do processo é recusada já no rascunho")]
    public void Salvar_OfertaForaDoProcesso_Recusa()
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(Guid.CreateVersion7(), null), Respostas(), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ofertaCursoId" && e.Error.Code == InscricaoErrorCodes.OfertaForaDoProcesso);
    }

    [Fact(DisplayName = "Submeter sem oferta, num processo com ofertas, é recusado; o rascunho aceita")]
    public void Submeter_SemOfertaNumProcessoComOfertas_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), SemGrupos(), SemAceites()), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag]))
            .IsSuccess.Should().BeTrue();

        Result<ComprovanteInscricao> resultado = inscricao.Submeter("000123",
            PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), SemGrupos(), SemAceites()), Admitidas,
            Apuracao(FormularioDeTeste.AvaliacaoSemOcultos), PrecondicaoIfMatch.DeTags([inscricao.ETag]), Instante());

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.OfertaObrigatoria);
    }

    [Fact(DisplayName = "A 2ª opção é recusada, já no rascunho, quando a versão fixada admite uma opção só")]
    public void Salvar_SegundaOpcaoEmVersaoDeUmaOpcao_Recusa()
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = SalvarOpcoes(inscricao, OfertaDoProcesso, SegundaOfertaDoProcesso, Admitidas with { OfertasDeCurso = new HashSet<Guid> { OfertaDoProcesso, SegundaOfertaDoProcesso } });

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ofertaCursoSegundaOpcaoId" && e.Error.Code == InscricaoErrorCodes.SegundaOpcaoNaoAdmitida);
    }

    [Fact(DisplayName = "As recusas das duas opções saem juntas: a 1ª fora do processo e a 2ª igual a ela, também fora")]
    public void Salvar_RecusasDasOpcoesSaemAcumuladas()
    {
        Inscricao inscricao = NovaInscricao();
        Guid foraDoProcesso = Guid.CreateVersion7();

        Result resultado = SalvarOpcoes(inscricao, foraDoProcesso, foraDoProcesso, AdmitidasComDuasOpcoes);

        resultado.Errors.Select(static recusa => (recusa.Field, recusa.Error.Code, recusa.Error.Message)).Should().Equal(
            ("ofertaCursoId", InscricaoErrorCodes.OfertaForaDoProcesso, "A oferta de curso escolhida não é deste processo seletivo."),
            ("ofertaCursoSegundaOpcaoId", InscricaoErrorCodes.SegundaOpcaoIgualAPrimeira, "A 2ª opção de curso tem de ser diferente da 1ª."),
            ("ofertaCursoSegundaOpcaoId", InscricaoErrorCodes.OfertaForaDoProcesso, "A oferta de curso escolhida não é deste processo seletivo."));
    }

    [Fact(DisplayName = "A 2ª opção igual à 1ª é recusada no campo da 2ª")]
    public void Salvar_SegundaOpcaoIgualAPrimeira_Recusa()
    {
        Inscricao inscricao = NovaInscricao();

        Result resultado = SalvarOpcoes(inscricao, OfertaDoProcesso, OfertaDoProcesso, AdmitidasComDuasOpcoes);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ofertaCursoSegundaOpcaoId" && e.Error.Code == InscricaoErrorCodes.SegundaOpcaoIgualAPrimeira);
    }

    [Fact(DisplayName = "Sem a 1ª opção, a 2ª passa no rascunho e é recusada no envio")]
    public void Opcoes_SegundaSemPrimeira_SoERecusadaNoEnvio()
    {
        Inscricao inscricao = NovaInscricao();

        SalvarOpcoes(inscricao, null, SegundaOfertaDoProcesso, AdmitidasComDuasOpcoes).IsSuccess.Should().BeTrue();
        Result<ComprovanteInscricao> envio = SubmeterOpcoes(inscricao, null, SegundaOfertaDoProcesso, FormularioDeTeste.AvaliacaoSemOcultos);

        envio.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Contain(("ofertaCursoSegundaOpcaoId", InscricaoErrorCodes.SegundaOpcaoSemPrimeira));
    }

    [Fact(DisplayName = "O envio grava a 2ª opção e as modalidades dela à parte das da 1ª, e o comprovante as congela")]
    public void Submeter_ComSegundaOpcao_GravaAsModalidadesPorOpcao()
    {
        Inscricao inscricao = NovaInscricao();
        ModalidadeDeConcorrencia ampla = new("AC", "Ampla concorrência", 0);
        ModalidadeDeConcorrencia escolaPublica = new("LI_EP", "Escola pública", 3);

        ComprovanteInscricao comprovante = SubmeterOpcoes(
            inscricao, OfertaDoProcesso, SegundaOfertaDoProcesso,
            ComModalidadesPorOpcao([ampla, escolaPublica], SituacaoDasModalidades.Concorre, ampla)).Value!;

        inscricao.Formulario.Preenchimento.Opcoes.Segunda.Should().Be(SegundaOfertaDoProcesso);
        inscricao.Formulario.Derivados["MODALIDADE"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("AC", "LI_EP");
        inscricao.Formulario.Derivados["MODALIDADE_SEGUNDA_OPCAO"].EnumerateArray().Select(static v => v.GetString()).Should().Equal("AC");
        ConferirComprovantePorOpcao(comprovante, ampla, escolaPublica);
    }

    [Fact(DisplayName = "Quem não concorre a nenhuma modalidade da 2ª opção é recusado no campo dela, mesmo concorrendo na 1ª")]
    public void Submeter_NenhumaModalidadeNaSegundaOpcao_RecusaNoCampoDela()
    {
        Inscricao inscricao = NovaInscricao();

        Result<ComprovanteInscricao> resultado = SubmeterOpcoes(
            inscricao, OfertaDoProcesso, SegundaOfertaDoProcesso,
            ComModalidadesPorOpcao([new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0)], SituacaoDasModalidades.NaoConcorre));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ofertaCursoSegundaOpcaoId" && e.Error.Code == InscricaoErrorCodes.NenhumaModalidade);
        resultado.Value.Should().BeNull("a recusa não emite comprovante");
    }

    [Fact(DisplayName = "Quem não concorre a nenhuma modalidade da 1ª opção, tendo declarado a 2ª, é recusado com a mensagem da 1ª opção")]
    public void Submeter_NenhumaModalidadeNaPrimeiraOpcaoComSegunda_DizQueEDaPrimeira()
    {
        Inscricao inscricao = NovaInscricao();
        ModalidadeDeConcorrencia ampla = new("AC", "Ampla concorrência", 0);
        ResultadoDaAvaliacaoDaInscricao avaliacao = ComModalidadesPorOpcao([ampla], SituacaoDasModalidades.Concorre, ampla) with
        {
            Modalidades = new ModalidadesDeConcorrencia(
                true, SituacaoDasModalidades.NaoConcorre, [], [], new DependenciasDaAvaliacao(["CONCORRER_PCD"], [])),
        };

        Result<ComprovanteInscricao> resultado = SubmeterOpcoes(inscricao, OfertaDoProcesso, SegundaOfertaDoProcesso, avaliacao);

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "CONCORRER_PCD" && e.Error.Message.Contains("1ª opção de curso", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A modalidade da 2ª opção ainda indecidida e sem campo a indicar é recusada no campo da 2ª, não no da 1ª")]
    public void Submeter_ModalidadeDaSegundaOpcaoNaoDecidida_RecusaNoCampoDela()
    {
        Inscricao inscricao = NovaInscricao();

        Result<ComprovanteInscricao> resultado = SubmeterOpcoes(
            inscricao, OfertaDoProcesso, SegundaOfertaDoProcesso,
            ComModalidadesPorOpcao([new ModalidadeDeConcorrencia("AC", "Ampla concorrência", 0)], SituacaoDasModalidades.FaltaResponder));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            e => e.Field == "ofertaCursoSegundaOpcaoId" && e.Error.Code == InscricaoErrorCodes.ModalidadeNaoDecidida);
    }

    [Fact(DisplayName = "O reenvio com a 2ª opção nula a retira da inscrição, e o novo comprovante sai sem ela")]
    public void Submeter_ReenvioSemSegundaOpcao_RetiraAOpcao()
    {
        Inscricao inscricao = NovaInscricao();
        ModalidadeDeConcorrencia ampla = new("AC", "Ampla concorrência", 0);
        ResultadoDaAvaliacaoDaInscricao avaliacao = ComModalidadesPorOpcao([ampla], SituacaoDasModalidades.Concorre, ampla);
        Result<ComprovanteInscricao> primeiroEnvio = SubmeterOpcoes(inscricao, OfertaDoProcesso, SegundaOfertaDoProcesso, avaliacao);
        primeiroEnvio.IsSuccess.Should().BeTrue();

        ComprovanteInscricao reenvio = SubmeterOpcoes(inscricao, OfertaDoProcesso, null, avaliacao).Value!;

        inscricao.Formulario.Preenchimento.Opcoes.Segunda.Should().BeNull();
        inscricao.Formulario.Derivados.Should().NotContainKey("MODALIDADE_SEGUNDA_OPCAO");
        reenvio.OfertaCursoSegundaOpcaoId.Should().BeNull();
        reenvio.ModalidadesSegundaOpcao.Should().BeNull();
        reenvio.Should().NotBeSameAs(primeiroEnvio.Value);
    }

    [Fact(DisplayName = "O rascunho que adota a versão de uma opção de curso só perde a 2ª opção, e o que sai da oferta leva a 2ª junto")]
    public void AdotarVersao_RetiraAOfertaEA2aOpcaoQueAVersaoNaoAdmite()
    {
        Inscricao deUmaOpcao = NovaInscricao();
        SalvarOpcoes(deUmaOpcao, OfertaDoProcesso, SegundaOfertaDoProcesso, AdmitidasComDuasOpcoes).IsSuccess.Should().BeTrue();
        Inscricao semAPrimeira = NovaInscricao();
        SalvarOpcoes(semAPrimeira, OfertaDoProcesso, SegundaOfertaDoProcesso, AdmitidasComDuasOpcoes).IsSuccess.Should().BeTrue();

        deUmaOpcao.AdotarVersao(VersaoRetificada, Admitidas).IsSuccess.Should().BeTrue();
        semAPrimeira.AdotarVersao(VersaoRetificada, AdmitidasComDuasOpcoes with { OfertasDeCurso = new HashSet<Guid> { SegundaOfertaDoProcesso } }).IsSuccess.Should().BeTrue();

        deUmaOpcao.Formulario.Preenchimento.Opcoes.Primeira.Should().Be(OfertaDoProcesso);
        deUmaOpcao.Formulario.Preenchimento.Opcoes.Segunda.Should().BeNull();
        semAPrimeira.Formulario.Preenchimento.Opcoes.Primeira.Should().BeNull();
        semAPrimeira.Formulario.Preenchimento.Opcoes.Segunda.Should().BeNull();
    }

    private static void ConferirComprovantePorOpcao(ComprovanteInscricao comprovante, ModalidadeDeConcorrencia ampla, ModalidadeDeConcorrencia escolaPublica)
    {
        comprovante.OfertaCursoSegundaOpcaoId.Should().Be(SegundaOfertaDoProcesso);
        comprovante.Modalidades.Should().Equal(ampla, escolaPublica);
        comprovante.ModalidadesSegundaOpcao.Should().Equal(ampla);
        comprovante.Derivados.Should().NotContainKey("MODALIDADE_SEGUNDA_OPCAO", "a seção das modalidades da 2ª opção já as registra, com o nome");
        comprovante.IntegridadeConfere().Should().BeTrue();
    }
}
