namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Um formulário por finalidade (UNI-REQ-0144): cada fato tem um só produtor no processo, a ordem é
/// única dentro de cada formulário, a fase é a que a finalidade pede, e a publicação exige o
/// formulário de inscrição quando a inscrição é própria.
/// </summary>
public sealed class FormulariosPorFinalidadeTests
{
    private static FatoColetado Item(string codigo, int ordem, string? etapa = FormularioDeTeste.Secao) =>
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, etapaCodigo: etapa).Value!;

    private static ProcessoSeletivo ComHabilitacao()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    [Fact(DisplayName = "O fato coletado por um formulário não é coletado por outro do mesmo processo")]
    public void ProdutorUnico_EntreFormularios()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.FatoDuplicado);
    }

    [Fact(DisplayName = "A ordem é única dentro de cada formulário: a mesma ordem em formulários diferentes é aceita")]
    public void Ordem_UnicaPorFormulario()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("CERTIFICADO_EMITIDO", 0)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("A", 1), Item("B", 1)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.OrdemDuplicada);
        processo.FatosColetados.Select(static f => (f.Finalidade, f.FatoCodigo)).Should().BeEquivalentTo(
            [(FinalidadeFormulario.Inscricao, "TEM_RENDA"), (FinalidadeFormulario.Habilitacao, "CERTIFICADO_EMITIDO")],
            "definir os itens de um formulário não mexe nos do outro");
    }

    private static TermoExigidoFormulario Termo(string codigo) =>
        TermoExigidoFormulario.Criar(
            codigo, 0,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "Declaração", "Texto", "Base legal", "REGISTRO_DIGITAL_SEM_LOG_IP", new string('a', 64)),
            null, Obrigatoriedade.Sempre).Value!;

    [Fact(DisplayName = "Itens e termos só entram em finalidade que tem formulário")]
    public void ItensETermos_SemFormulario_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();

        processo.DefinirFatosColetados(FinalidadeFormulario.IsencaoTaxa, [Item("A", 0)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FormularioInexistente);
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.IsencaoTaxa, [Termo("DECLARACAO")], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FormularioInexistente);
    }

    [Fact(DisplayName = "No rascunho, a recusa de seção aponta o item pelo índice dele na lista enviada")]
    public void Item_ForaDeSecao_ApontaOIndiceOriginal() =>
        ProcessoConformeFactory.Criar().DefinirFatosColetados(
                FinalidadeFormulario.Inscricao, [Item("A", 0, etapa: null), Item("B", 1, "INEXISTENTE")], PrecondicaoIfMatch.Ausente)
            .Errors.Should().ContainSingle().Which.Field.Should().Be("itens[1].etapaCodigo");

    [Fact(DisplayName = "Aviso da etapa acima do limite é recusado no próprio campo")]
    public void Etapa_AvisoLongo_RecusaNoCampoAviso() =>
        EtapaFormulario.Criar("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, new string('a', 2001))
            .Errors.Should().ContainSingle().Which.Field.Should().Be("aviso");

    private static FatoColetado ItemQueCita(string codigo, int ordem, string citado) =>
        FatoColetado.Criar(
            codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Nunca,
            [CondicaoPrecondicaoFato.Criar(0, citado, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
            etapaCodigo: FormularioDeTeste.Secao).Value!;

    [Fact(DisplayName = "Item de outra finalidade cita fato da inscrição; fato que nenhum dos dois coleta é recusado")]
    public void OutraFinalidade_CitaFatoDaInscricao()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE_RENDA", 0, "TEM_RENDA")], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE_RENDA", 0, "TEM_BOLSA")], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
    }

    [Fact(DisplayName = "A inscrição não perde fato que um item de outra finalidade cita")]
    public void Inscricao_NaoPerdeFatoCitadoPorOutraFinalidade()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [ItemQueCita("COMPROVANTE_RENDA", 0, "TEM_RENDA")], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("COR_RACA", 0)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
        processo.RemoverFormulario(FinalidadeFormulario.Inscricao, PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
    }

    private static ConfiguracaoDerivacaoFato ModalidadeQueDependeDe(string fato) =>
        ConfiguracaoDerivacaoFato.Criar("MODALIDADE",
            [RegraDerivacaoConfigurada.Criar(0, "AC", [CondicaoRegraDerivacao.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!])
            .Value!;

    [Fact(DisplayName = "Item cita derivado cujas dependências são anteriores; derivado de campo posterior é recusado")]
    public void Item_CitaDerivado_PelasDependencias()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirRegrasDerivacao([ModalidadeQueDependeDe("TEM_RENDA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0), ItemQueCitaModalidade(1)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [ItemQueCitaModalidade(0), Item("TEM_RENDA", 1)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoPosterior);
    }

    [Fact(DisplayName = "A publicação recusa item que cita derivado redefinido sobre campo posterior")]
    public void Publicacao_DerivacaoMudaDepoisDaCitacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirRegrasDerivacao([ModalidadeQueDependeDe("TEM_RENDA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(
                FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0), ItemQueCitaModalidade(1), Item("TEM_BOLSA", 2)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo).Should().BeNull();

        processo.DefinirRegrasDerivacao([ModalidadeQueDependeDe("TEM_BOLSA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoPosterior);
    }

    [Fact(DisplayName = "Termo cita os campos do próprio formulário e os da inscrição, nunca os de outra finalidade")]
    public void Termo_CitaSoOQueOFormularioConhece()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("COMPROVANTE_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [TermoQueCita("TEM_RENDA"), TermoQueCita("COMPROVANTE_RENDA", 1)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.Inscricao, [TermoQueCita("COMPROVANTE_RENDA")], PrecondicaoIfMatch.Ausente)
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "termos[0]",
                Error = new { Code = FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado },
            });
    }

    [Fact(DisplayName = "A inscrição não perde fato que um termo de outra finalidade cita")]
    public void Inscricao_NaoPerdeFatoCitadoPorTermoDeOutraFinalidade()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [TermoQueCita("TEM_RENDA")], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("COR_RACA", 0)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
    }

    [Fact(DisplayName = "Citação de outro formulário que já era inválida não trava a edição da inscrição")]
    public void Inscricao_CitacaoJaInvalidaEmOutroFormulario_NaoTravaAEdicao()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirRegrasDerivacao([ModalidadeQueDependeDe("TEM_RENDA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(
                FinalidadeFormulario.Habilitacao, [ItemQueCitaModalidade(0), Item("COMPROVANTE_RENDA", 1)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([ModalidadeQueDependeDe("COMPROVANTE_RENDA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0), Item("COR_RACA", 1)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue("a troca da inscrição não tira nada que a habilitação conhecia");
    }

    [Fact(DisplayName = "Redefinir a inscrição mantendo o fato citado por outra finalidade é aceito")]
    public void Inscricao_MantemFatoCitadoPorOutraFinalidade_Aceita()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [TermoQueCita("TEM_RENDA")], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0), Item("COR_RACA", 1)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Citação que a troca da inscrição invalida é recusada mesmo com outra citação já inválida no formulário")]
    public void Inscricao_InvalidaCitacaoComOutraJaInvalida_Recusa()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirRegrasDerivacao([ModalidadeQueDependeDe("TEM_RENDA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(
                FinalidadeFormulario.Habilitacao, [ItemQueCitaModalidade(0), Item("COMPROVANTE_RENDA", 1)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [TermoQueCita("TEM_RENDA")], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([ModalidadeQueDependeDe("COMPROVANTE_RENDA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("COR_RACA", 0)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
    }

    [Fact(DisplayName = "Remover da inscrição o fato citado por um item é recusado mesmo que outra citação do item já seja inválida")]
    public void Inscricao_ComparaCadaCitacaoDoItem()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirRegrasDerivacao([ModalidadeQueDependeDe("TEM_RENDA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        FatoColetado citaOsDois = FatoColetado.Criar(
            "CONCORRER_EP", 0, "Concorrer", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca,
            [
                CondicaoPrecondicaoFato.Criar(0, "MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!,
                CondicaoPrecondicaoFato.Criar(0, "TEM_RENDA", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
            ],
            etapaCodigo: FormularioDeTeste.Secao).Value!;
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [citaOsDois, Item("COMPROVANTE_RENDA", 1)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([ModalidadeQueDependeDe("COMPROVANTE_RENDA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("COR_RACA", 0)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
    }

    private static IReadOnlyList<EtapaFormulario> EtapasComExibicao(string? citado) =>
    [
        EtapaFormulario.Criar("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null).Value!,
        EtapaFormulario.Criar("RENDA", 1, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Renda", null, null,
            citado is null
                ? null
                : PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(citado, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!).Value!,
        EtapaFormulario.Criar("REVISAO", 2, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!,
    ];

    private static FatoColetado ItemNaSecao(string codigo, int ordem, string secao) =>
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, etapaCodigo: secao).Value!;

    [Fact(DisplayName = "Exibição de seção cita campo de seção anterior; campo da própria seção é recusado")]
    public void Secao_ExibicaoCitaSoOQueVemAntes()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid? fase = processo.FormularioDe(FinalidadeFormulario.Inscricao)!.FaseId;
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, EtapasComExibicao(null), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao,
            [ItemNaSecao("TEM_RENDA", 0, "DADOS"), ItemNaSecao("RENDA_FORMAL", 1, "RENDA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, EtapasComExibicao("TEM_RENDA"), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, EtapasComExibicao("RENDA_FORMAL"), PrecondicaoIfMatch.Ausente)
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "etapas[1].exibicao",
                Error = new { Code = FatoColetadoErrorCodes.PrecondicaoCitaFatoPosterior },
            });
    }

    [Fact(DisplayName = "Trocar os itens de modo que a seção passe a citar campo dela mesma é recusado")]
    public void Secao_ItensQueQuebramAExibicao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid? fase = processo.FormularioDe(FinalidadeFormulario.Inscricao)!.FaseId;
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, EtapasComExibicao(null), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao,
            [ItemNaSecao("TEM_RENDA", 0, "DADOS"), ItemNaSecao("RENDA_FORMAL", 1, "RENDA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, EtapasComExibicao("TEM_RENDA"), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao,
                [ItemNaSecao("RENDA_FORMAL", 0, "DADOS"), ItemNaSecao("TEM_RENDA", 1, "RENDA")], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoPosterior);
    }

    [Fact(DisplayName = "A inscrição não perde fato que a exibição de uma seção de outra finalidade cita")]
    public void Inscricao_NaoPerdeFatoCitadoPorSecaoDeOutraFinalidade()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, null, EtapasComExibicao("TEM_RENDA"), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("COR_RACA", 0)], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
    }

    [Fact(DisplayName = "A publicação recusa seção que cita derivado redefinido sobre campo da própria seção")]
    public void Publicacao_SecaoCitaDerivadoRedefinido_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid? fase = processo.FormularioDe(FinalidadeFormulario.Inscricao)!.FaseId;
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, EtapasComExibicao(null), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao,
            [ItemNaSecao("TEM_RENDA", 0, "DADOS"), ItemNaSecao("RENDA_FORMAL", 1, "RENDA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([ModalidadeQueDependeDe("TEM_RENDA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        IReadOnlyList<EtapaFormulario> etapas =
        [
            EtapaFormulario.Criar("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null).Value!,
            EtapaFormulario.Criar("RENDA", 1, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Renda", null, null,
                PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!)]).Value!).Value!,
            EtapaFormulario.Criar("REVISAO", 2, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!,
        ];
        processo.DefinirFormulario(FinalidadeFormulario.Inscricao, fase, null, etapas, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo).Should().BeNull();

        processo.DefinirRegrasDerivacao([ModalidadeQueDependeDe("RENDA_FORMAL")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoPosterior);
    }

    private static FatoColetado Opcional(string codigo, int ordem, params CondicaoPrecondicaoFato[] precondicoes) =>
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, precondicoes, etapaCodigo: FormularioDeTeste.Secao).Value!;

    [Fact(DisplayName = "Campo opcional que alimenta derivação recusa a publicação; obrigatório sempre que exibido, aceita")]
    public void Publicacao_CampoOpcionalQueAlimentaDerivacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirRegrasDerivacao([ModalidadeQueDependeDe("TEM_RENDA")], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Opcional("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be(FatoColetadoErrorCodes.OpcionalQueAlimentaRegra);

        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo).Should().BeNull();
    }

    [Fact(DisplayName = "Campo opcional citado por DIFERENTE em regra de outro campo recusa a publicação")]
    public void Publicacao_CampoOpcionalCitadoPorNegacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao,
        [
            Opcional("NACIONALIDADE_BR", 0),
            Opcional("COMPROVANTE", 1, CondicaoPrecondicaoFato.Criar(0, "NACIONALIDADE_BR", Operador.Diferente, JsonSerializer.SerializeToElement(true)).Value!),
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Message.Should().Contain("'NACIONALIDADE_BR'");
    }

    [Fact(DisplayName = "Bloco de sistema não tem exibição condicional")]
    public void Bloco_ComExibicao_Recusa() =>
        EtapaFormulario.Criar("REVISAO", 2, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null,
                PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar("TEM_RENDA", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!)
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "exibicao",
                Error = new { Code = EstruturaFormularioErrorCodes.ExibicaoForaDeSecao },
            });

    private static FatoColetado ItemQueCitaModalidade(int ordem) =>
        FatoColetado.Criar(
            "CONCORRER_EP", ordem, "Concorrer", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca,
            [CondicaoPrecondicaoFato.Criar(0, "MODALIDADE", Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!],
            etapaCodigo: FormularioDeTeste.Secao).Value!;

    private static TermoExigidoFormulario TermoQueCita(string fato, int ordem = 0) =>
        TermoExigidoFormulario.Criar(
            $"TERMO_{ordem}", ordem,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "Declaração", "Texto", "Base legal", "REGISTRO_DIGITAL_SEM_LOG_IP", new string('a', 64)),
            PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
            Obrigatoriedade.Sempre).Value!;

    [Theory(DisplayName = "Item em bloco de sistema ou em etapa inexistente é recusado na definição")]
    [InlineData("REVISAO")]
    [InlineData("INEXISTENTE")]
    public void Item_ForaDeSecao_RecusaNaDefinicao(string etapa) =>
        ProcessoConformeFactory.Criar().DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("A", 0, etapa)], PrecondicaoIfMatch.Ausente)
            .Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.ItemForaDeSecao);

    [Fact(DisplayName = "A fase do formulário está no cronograma e é a que a finalidade pede")]
    public void Fase_CoerenteComFinalidade()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid faseDeInscricao = processo.CronogramaFases.Single(static f => f.ColetaInscricao).Id;

        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, Guid.CreateVersion7(), null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FaseForaDoCronograma);
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, faseDeInscricao, null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FaseIncoerenteComFinalidade);
    }

    [Fact(DisplayName = "Título, fase e estrutura do formulário são recusados juntos")]
    public void DefinirFormulario_AcumulaAsRecusas()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        Guid faseDeInscricao = processo.CronogramaFases.Single(static f => f.ColetaInscricao).Id;
        EtapaFormulario soSecao = EtapaFormulario.Criar("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null).Value!;

        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, faseDeInscricao, new string('a', 301), [soSecao], PrecondicaoIfMatch.Ausente)
            .Errors.Select(static e => e.Error.Code).Should().BeEquivalentTo(
            [
                FormularioProcessoErrorCodes.TituloTamanho,
                FormularioProcessoErrorCodes.FaseIncoerenteComFinalidade,
                EstruturaFormularioErrorCodes.BlocoExigidoAusente,
            ]);
    }

    [Fact(DisplayName = "Redefinir o formulário sem a seção de um item existente é recusado nas etapas")]
    public void DefinirFormulario_SemASecaoDeUmItem_RecusaNasEtapas()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0)], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        EtapaFormulario outraSecao = EtapaFormulario.Criar("OUTRA", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Outra", null, null).Value!;
        EtapaFormulario revisao = EtapaFormulario.Criar("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null).Value!;

        processo.DefinirFormulario(
                FinalidadeFormulario.Inscricao, processo.FormularioDe(FinalidadeFormulario.Inscricao)!.FaseId, null, [outraSecao, revisao], PrecondicaoIfMatch.Ausente)
            .Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Field = "etapas",
                Error = new { Code = EstruturaFormularioErrorCodes.ItemForaDeSecao },
            });
    }

    [Fact(DisplayName = "O cronograma recusa remover a fase em que um formulário é respondido")]
    public void Cronograma_RecusaRemoverFaseDoFormulario() =>
        ProcessoConformeFactory.Criar().DefinirCronogramaFases([ProcessoConformeFactory.FaseConforme()], [], PrecondicaoIfMatch.Ausente)
            .Error!.Code.Should().Be(FormularioProcessoErrorCodes.FaseReferenciadaPorFormulario);

    [Fact(DisplayName = "Processo com inscrição própria sem formulário de inscrição: item vermelho e publicação recusada")]
    public void Publicacao_ExigeFormularioDeInscricao()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.RemoverFormulario(FinalidadeFormulario.Inscricao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be(FormularioProcessoErrorCodes.InscricaoSemFormulario);
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo).Should().Contain(static i => i.Codigo == "formulario_inscricao_ausente" && !i.Ok);
    }

    [Fact(DisplayName = "Formulário sem fase é aceito no rascunho e recusado na publicação")]
    public void Publicacao_ExigeFaseDoFormulario()
    {
        ProcessoSeletivo processo = ComHabilitacao();

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be(FormularioProcessoErrorCodes.SemFase);
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo).Should().Contain(static i => i.Codigo == "formulario_fase_incoerente" && !i.Ok);
    }

    [Fact(DisplayName = "Formulário de isenção só em processo que cobra taxa")]
    public void Publicacao_IsencaoExigeTaxa()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFormulario(FinalidadeFormulario.IsencaoTaxa, null, null, FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo).Should().Contain(static i => i.Codigo == "formulario_isencao_sem_taxa" && !i.Ok);
    }

    [Fact(DisplayName = "Item sem seção é aceito no rascunho e recusado na publicação")]
    public void Publicacao_ExigeItemEmSecao()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFatosColetados(FinalidadeFormulario.Inscricao, [Item("TEM_RENDA", 0, etapa: null)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao(FatosDeModalidadeDeTeste.DoCatalogo)!.Code.Should().Be(FormularioProcessoErrorCodes.ItemForaDeSecao);
        processo.AvaliarConformidade(ContextoDeContagemDePrazos.SemCalendario, FatosDeModalidadeDeTeste.DoCatalogo).Should().Contain(static i => i.Codigo == "formulario_item_fora_de_secao" && !i.Ok);
    }

    [Fact(DisplayName = "Remover o formulário em rascunho remove os itens e os termos dele")]
    public void Remover_LevaItensETermos()
    {
        ProcessoSeletivo processo = ComHabilitacao();
        processo.DefinirFatosColetados(FinalidadeFormulario.Habilitacao, [Item("CERTIFICADO_EMITIDO", 0)], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirTermosDoFormulario(FinalidadeFormulario.Habilitacao, [Termo("DECLARACAO")], PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();

        processo.RemoverFormulario(FinalidadeFormulario.Habilitacao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.FormularioDe(FinalidadeFormulario.Habilitacao).Should().BeNull();
        processo.FatosColetados.Should().NotContain(static f => f.Finalidade == FinalidadeFormulario.Habilitacao);
        processo.TermosExigidos.Should().NotContain(static t => t.Finalidade == FinalidadeFormulario.Habilitacao);
    }
}
