namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A aplicação da cópia de um modelo de formulário ao processo (UNI-REQ-0144, ADR-0061): substitui
/// o formulário inteiro, preserva a fase, registra a origem, traz para a inscrição o fato de outro
/// formulário e confere o estado final antes de mudar o processo.
/// </summary>
public sealed class ProcessoSeletivoAplicarModeloTests
{
    private static readonly ModeloDeOrigem Origem = new(Guid.CreateVersion7(), "INSCRICAO_MEDICINA", "Modelo de inscrição");

    [Fact(DisplayName = "Aplicar modelo no processo publicado recusa antes de conferir a cópia e preserva os itens")]
    public void AplicarModeloDeFormulario_Publicado_RecusaSemMudar()
    {
        ProcessoSeletivo processo = ProcessoEmEdicaoDeTeste.Publicado(out _);
        FatoColetado[] antes = [.. processo.FatosColetados];

        Result resultado = processo.AplicarModeloDeFormulario(Copia(PapelDoFormularioDeTeste.Inscricao, Item("COR_RACA", 0)));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada");
        processo.FatosColetados.Should().Equal(antes);
    }

    [Fact(DisplayName = "A cópia num processo sem o formulário é recusada, sem criar formulário nem origem")]
    public void AplicarModeloDeFormulario_SemFormulario_RecusaSemCriar()
    {
        ProcessoSeletivo processo = Processo();

        Result resultado = processo.AplicarModeloDeFormulario(Copia(PapelDoFormularioDeTeste.Inscricao, Item("COR_RACA", 0)));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            recusa => recusa.Field == "formularioCodigo" && recusa.Error.Code == FormularioProcessoErrorCodes.FormularioInexistente);
        processo.Formularios.Should().BeEmpty();
        processo.FatosColetados.Should().BeEmpty();
    }

    [Fact(DisplayName = "A reaplicação substitui os itens do formulário, preserva a fase e registra a origem")]
    public void AplicarModeloDeFormulario_FormularioExistente_SubstituiEPreservaAFase()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirItensComFaseDeInscricao([Item("QUILOMBOLA", 0)]).IsSuccess.Should().BeTrue();
        Guid? fase = processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.FaseId;

        Result resultado = processo.AplicarModeloDeFormulario(Copia(PapelDoFormularioDeTeste.Inscricao, Item("COR_RACA", 0)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.Select(static f => f.FatoCodigo).Should().Equal("COR_RACA");
        FormularioProcesso formulario = processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!;
        formulario.FaseId.Should().NotBeNull().And.Be(fase);
        formulario.ModeloDeOrigem.Should().Be(Origem);
    }

    [Fact]
    public void AplicarModeloDeFormulario_ModeloRenomeado_RecopiaNome()
    {
        ProcessoSeletivo processo = Processo();
        CopiaDeModeloDeFormulario copia = Copia(PapelDoFormularioDeTeste.Inscricao, Item("COR_RACA", 0));
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Inscricao);
        processo.AplicarModeloDeFormulario(copia).IsSuccess.Should().BeTrue();

        Result resultado = processo.AplicarModeloDeFormulario(copia with { Origem = Origem with { Nome = "Modelo atualizado" } });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.ModeloDeOrigem!.Nome.Should().Be("Modelo atualizado");
    }

    [Fact(DisplayName = "Na inscrição, o fato que outro formulário coletava passa para a inscrição")]
    public void AplicarModeloDeFormulario_InscricaoComFatoDeOutroFormulario_TrazOFato()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirItensComFaseDeInscricao([]).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Item("CERTIFICADO", 0)], papel: PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();

        Result resultado = processo.AplicarModeloDeFormulario(Copia(PapelDoFormularioDeTeste.Inscricao, Item("CERTIFICADO", 0)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.Should().ContainSingle().Which.FormularioId.Should().Be(processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.Id);
    }

    [Fact(DisplayName = "Na inscrição, o fato que outro formulário da inscrição já coleta é recusado, sem mudar o processo")]
    public void AplicarModeloDeFormulario_InscricaoComFatoDeOutroFormularioDaInscricao_RecusaSemMudar()
    {
        ProcessoSeletivo processo = ProcessoComSegundoFormularioDaInscricao(PapelDoFormularioDeTeste.Habilitacao, Item("CERTIFICADO", 0));
        FatoColetado[] antes = [.. processo.FatosColetados];

        Result resultado = processo.AplicarModeloDeFormulario(Copia(PapelDoFormularioDeTeste.Inscricao, Item("CERTIFICADO", 0)));

        resultado.Errors.Should().ContainSingle().Which.Should().Match<FieldError>(
            recusa => recusa.Field == "itens" && recusa.Error.Code == FatoColetadoErrorCodes.FatoDuplicado
                && recusa.Error.Message.Contains("'CERTIFICADO'", StringComparison.Ordinal)
                && recusa.Error.Message.Contains("HABILITACAO", StringComparison.Ordinal));
        processo.FatosColetados.Should().Equal(antes);
        processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.ModeloDeOrigem.Should().BeNull();
    }

    [Fact(DisplayName = "Na inscrição, o fato de um formulário que não é da inscrição é trazido e a citação dele continua válida")]
    public void AplicarModeloDeFormulario_InscricaoComFatoDeFormularioForaDaInscricao_TrazOFatoEMantemACitacao()
    {
        ProcessoSeletivo processo = ProcessoComSegundoFormularioDaInscricao(
            PapelDoFormularioDeTeste.IsencaoTaxa, Item("CERTIFICADO", 0), Item("DECLARACAO", 1, exibidoQuando: "CERTIFICADO"));

        Result resultado = processo.AplicarModeloDeFormulario(Copia(PapelDoFormularioDeTeste.Inscricao, Item("CERTIFICADO", 0)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        Guid inscricao = processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.Id;
        processo.FatosColetados.Single(static f => f.FatoCodigo == "CERTIFICADO").FormularioId.Should().Be(inscricao);
        processo.FatosColetados.Single(static f => f.FatoCodigo == "DECLARACAO").FormularioId
            .Should().Be(processo.FormularioDe(PapelDoFormularioDeTeste.IsencaoTaxa)!.Id);
    }

    [Fact(DisplayName = "Na inscrição, os fatos de outro formulário da inscrição são recusados juntos, em ordem de código")]
    public void AplicarModeloDeFormulario_InscricaoComFatosDeOutroFormularioDaInscricao_RecusaTodosEmOrdemDeCodigo()
    {
        ProcessoSeletivo processo = ProcessoComSegundoFormularioDaInscricao(
            PapelDoFormularioDeTeste.Habilitacao, Item("PROCURADOR", 0), Item("CERTIFICADO", 1));

        Result resultado = processo.AplicarModeloDeFormulario(
            Copia(PapelDoFormularioDeTeste.Inscricao, Item("PROCURADOR", 0), Item("CERTIFICADO", 1)));

        resultado.Errors.Should().HaveCount(2).And.OnlyContain(static e => e.Error.Code == FatoColetadoErrorCodes.FatoDuplicado);
        resultado.Errors.Select(static e => e.Error.Message).Should().SatisfyRespectively(
            primeira => primeira.Should().Contain("'CERTIFICADO'"),
            segunda => segunda.Should().Contain("'PROCURADOR'"));
    }

    [Fact(DisplayName = "Fora da inscrição, os fatos que outro formulário coleta são recusados juntos")]
    public void AplicarModeloDeFormulario_ForaDaInscricaoComFatosDeOutroFormulario_RecusaTodos()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirItens([Item("CERTIFICADO", 0), Item("PROCURADOR", 1)], papel: PapelDoFormularioDeTeste.IsencaoTaxa).IsSuccess.Should().BeTrue();
        FormularioDeTeste.GarantirFormulario(processo, PapelDoFormularioDeTeste.Habilitacao);
        Guid isencao = processo.FormularioDe(PapelDoFormularioDeTeste.IsencaoTaxa)!.Id;

        Result resultado = processo.AplicarModeloDeFormulario(
            Copia(PapelDoFormularioDeTeste.Habilitacao, Item("CERTIFICADO", 0), Item("PROCURADOR", 1)));

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal(FatoColetadoErrorCodes.FatoDuplicado, FatoColetadoErrorCodes.FatoDuplicado);
        processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)!.ModeloDeOrigem.Should().BeNull();
        processo.Campos.Should().OnlyContain(f => f.FormularioId == isencao);
    }

    [Fact(DisplayName = "A cópia que deixaria sem o fato citado outro formulário é recusada, sem mudar o processo")]
    public void AplicarModeloDeFormulario_CitacaoQueFicariaOrfa_RecusaSemMudar()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirItensComFaseDeInscricao([Item("QUILOMBOLA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Item("DECLARACAO", 0, exibidoQuando: "QUILOMBOLA")], papel: PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();

        Result resultado = processo.AplicarModeloDeFormulario(Copia(PapelDoFormularioDeTeste.Inscricao, Item("COR_RACA", 0)));

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
        processo.FatosColetados.ForaDoConjuntoBasico().Select(static f => f.FatoCodigo).Should().BeEquivalentTo(["QUILOMBOLA", "DECLARACAO"]);
        processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.ModeloDeOrigem.Should().BeNull();
    }

    [Fact(DisplayName = "As citações que ficariam órfãs em mais de um formulário são recusadas juntas")]
    public void AplicarModeloDeFormulario_OrfasEmDoisFormularios_RecusaTodas()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirItensComFaseDeInscricao([Item("QUILOMBOLA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Item("DECLARACAO", 0, exibidoQuando: "QUILOMBOLA")], papel: PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Item("COMPROVANTE", 0, exibidoQuando: "QUILOMBOLA")], papel: PapelDoFormularioDeTeste.IsencaoTaxa).IsSuccess.Should().BeTrue();

        Result resultado = processo.AplicarModeloDeFormulario(Copia(PapelDoFormularioDeTeste.Inscricao, Item("COR_RACA", 0)));

        resultado.Errors.Should().HaveCount(2).And.OnlyContain(static e => e.Error.Code == GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact(DisplayName = "A citação que outro formulário já tinha inválida não impede a aplicação; fica para a publicação")]
    public void AplicarModeloDeFormulario_CitacaoJaInvalidaEmOutroFormulario_Aplica()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirItensComFaseDeInscricao([Item("QUILOMBOLA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([Derivacao("PERFIL", "QUILOMBOLA")]).IsSuccess.Should().BeTrue();
        processo.DefinirItens([Item("DECLARACAO", 0, exibidoQuando: "PERFIL")], papel: PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([]).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(PapelDoFormularioDeTeste.IsencaoTaxa, null, null, FormularioDeTeste.Paginas(), null).IsSuccess.Should().BeTrue();

        Result resultado = processo.AplicarModeloDeFormulario(Copia(PapelDoFormularioDeTeste.IsencaoTaxa, Item("COMPROVANTE", 0)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "No formulário que já tinha uma citação inválida, a citação que a cópia invalida é recusada")]
    public void AplicarModeloDeFormulario_FormularioComCitacaoJaInvalida_RecusaANovaOrfa()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirItensComFaseDeInscricao([Item("QUILOMBOLA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([Derivacao("PERFIL", "QUILOMBOLA")]).IsSuccess.Should().BeTrue();
        processo.DefinirItens(
            [Item("DECLARACAO", 0, exibidoQuando: "PERFIL"), Item("DOC", 1, exibidoQuando: "QUILOMBOLA")],
            papel: PapelDoFormularioDeTeste.Habilitacao).IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([]).IsSuccess.Should().BeTrue();

        Result resultado = processo.AplicarModeloDeFormulario(Copia(PapelDoFormularioDeTeste.Inscricao, Item("COR_RACA", 0)));

        resultado.Errors.Should().ContainSingle().Which.Error.Message.Should().Contain("'QUILOMBOLA'");
    }

    [Fact(DisplayName = "A reaplicação mantém a consequência da falta de envio que o processo declarou, e o modelo não a traz")]
    public void AplicarModeloDeFormulario_FormularioComConsequencia_MantemAConsequenciaDoProcesso()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirFormulario(PapelDoFormularioDeTeste.Habilitacao, null, null, FormularioDeTeste.Paginas(), null, ConsequenciaDoDescumprimento.EliminaDoProcesso)
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.AplicarModeloDeFormulario(Copia(PapelDoFormularioDeTeste.Habilitacao, Item("COR_RACA", 0)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FormularioDe(PapelDoFormularioDeTeste.Habilitacao)!.ConsequenciaDaFaltaDeEnvio.Should().Be(ConsequenciaDoDescumprimento.EliminaDoProcesso);
    }

    [Fact(DisplayName = "As derivações da cópia somam-se às do processo, que continuam")]
    public void AplicarModeloDeFormulario_DerivacoesNovas_SomamSeAsExistentes()
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirItens([Item("QUILOMBOLA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([Derivacao("PERFIL_A", "QUILOMBOLA")]).IsSuccess.Should().BeTrue();

        Result resultado = processo.AplicarModeloDeFormulario(
            Copia(PapelDoFormularioDeTeste.Inscricao, Item("QUILOMBOLA", 0)) with { DerivacoesNovas = [Derivacao("PERFIL_B", "QUILOMBOLA")] });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.RegrasDerivacao.Select(static r => r.CodigoFato).Should().BeEquivalentTo(["PERFIL_A", "PERFIL_B"]);
    }

    private static ProcessoSeletivo Processo() => ProcessoSeletivo.Criar(
        "PS Modelo", TiposDeProcessoDeTeste.Sisu, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    // O formulário de habilitação é obrigatório e fica ligado à fase de inscrição: é da inscrição. O de isenção da taxa é
    // facultativo, então não é, mesmo ligado à mesma fase.
    private static ProcessoSeletivo ProcessoComSegundoFormularioDaInscricao(PapelDoFormularioDeTeste papel, params FatoColetado[] itensDoSegundo)
    {
        ProcessoSeletivo processo = Processo();
        processo.DefinirItensComFaseDeInscricao([]).IsSuccess.Should().BeTrue();
        Guid? faseDeInscricao = processo.FormularioDe(PapelDoFormularioDeTeste.Inscricao)!.FaseId;
        processo.DefinirFormulario(papel, faseDeInscricao, null, FormularioDeTeste.Paginas(papel)).IsSuccess.Should().BeTrue();
        processo.DefinirItens(itensDoSegundo, papel: papel).IsSuccess.Should().BeTrue();
        return processo;
    }

    private static CopiaDeModeloDeFormulario Copia(PapelDoFormularioDeTeste papel, params FatoColetado[] itens) =>
        new(FormularioDeTeste.CodigoDoPapel(papel), "Formulário", FormularioDeTeste.Paginas(), itens, [], [], [], Origem);

    private static FatoColetado Item(string fato, int ordem, string? exibidoQuando = null) => FatoColetadoDeTeste.Coletando(fato, ordem)
        .ComObrigatoriedade(Obrigatoriedade.Sempre)
        .ExibidoQuando(exibidoQuando is null ? null : ExibicaoDeTeste.Quando(exibidoQuando, Operador.Igual, true))
        .NaSecao(FormularioDeTeste.Secao)
        .Construir();

    private static ConfiguracaoDerivacaoFato Derivacao(string fato, string citado) => ConfiguracaoDerivacaoFato.Criar(
        fato,
        [RegraDerivacaoConfigurada.Criar(0, "SIM", [CondicaoRegraDerivacao.Criar(0, citado, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!]).Value!;
}
