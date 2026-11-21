namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Os itens, os termos e os processos que os testes dos formulários por finalidade montam.</summary>
internal static class ApoioFormulariosPorFinalidade
{
    internal static FatoColetado Item(string codigo, int ordem, string? etapa = FormularioDeTeste.Secao) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem).ComObrigatoriedade(Obrigatoriedade.Sempre).NaSecao(etapa).Construir();

    internal static ProcessoSeletivo ComHabilitacao()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, null, null, FormularioDeTeste.Etapas())
            .IsSuccess.Should().BeTrue();
        return processo;
    }

    internal static TermoExigidoFormulario Termo(string codigo) =>
        TermoExigidoFormulario.Criar(
            codigo, 0,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "TERMO_DE_TESTE", 1, "Declaração", "Texto", "Base legal", "REGISTRO_DIGITAL_SEM_LOG_IP", new string('a', 64)),
            null, Obrigatoriedade.Sempre).Value!;

    internal static FatoColetado ItemQueCita(string codigo, int ordem, string citado) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem)
            .ExibidoQuando(ExibicaoDeTeste.Quando(citado, Operador.Igual, true))
            .NaSecao(FormularioDeTeste.Secao)
            .Construir();

    internal static ConfiguracaoDerivacaoFato DerivadoQueDependeDe(string fato) =>
        ConfiguracaoDerivacaoFato.Criar("REGIME_DE_VAGA",
            [RegraDerivacaoConfigurada.Criar(0, "AC", [CondicaoRegraDerivacao.Criar(0, fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!])
            .Value!;

    internal static IReadOnlyList<EtapaFormulario> EtapasComExibicao(string? citado) =>
    [
        FormularioDeTeste.Etapas()[0],
        EtapaFormulario.Criar("DADOS", 1, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null).Value!,
        EtapaFormulario.Criar("RENDA", 2, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Renda", null, null,
            citado is null
                ? null
                : PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(citado, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!).Value!,
        EtapaFormulario.Criar("REVISAO", 3, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão e aceite", null, null).Value!,
    ];

    internal static FatoColetado ItemNaSecao(string codigo, int ordem, string secao) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem).ComObrigatoriedade(Obrigatoriedade.Sempre).NaSecao(secao).Construir();

    internal static FatoColetado Opcional(string codigo, int ordem, PredicadoDnf? exibicao = null) =>
        FatoColetadoDeTeste.Coletando(codigo, ordem)
            .ExibidoQuando(exibicao)
            .NaSecao(FormularioDeTeste.Secao)
            .Construir();

    /// <summary>A data de nascimento na seção dos dados básicos, com a obrigatoriedade do caso.</summary>
    internal static FatoColetado DataDeNascimento(Obrigatoriedade obrigatoriedade) => FatoColetadoDeTeste.Coletando("DATA_NASCIMENTO", ConjuntoBasicoDaInscricao.Itens.Single(static i => i.FatoCodigo == "DATA_NASCIMENTO").Ordem)
        .ComRotulo("Data de nascimento")
        .DoTipo(TipoRenderizacao.Data)
        .ComObrigatoriedade(obrigatoriedade)
        .NaSecao(ConjuntoBasicoDaInscricao.CodigoDaSecao)
        .Construir();

    internal static FatoColetado CitaFaixaEtaria(int ordem) => Opcional(
        "MENOR_SOB_RESPONSAVEL", ordem, ExibicaoDeTeste.Quando("FAIXA_ETARIA", Operador.MenorIgual, 17));

    internal static FatoColetado ItemQueCitaODerivado(int ordem) =>
        FatoColetadoDeTeste.Coletando("CONCORRER_EP", ordem)
            .ComRotulo("Concorrer")
            .ExibidoQuando(ExibicaoDeTeste.Quando("REGIME_DE_VAGA", Operador.Igual, "AC"))
            .NaSecao(FormularioDeTeste.Secao)
            .Construir();

    internal static TermoExigidoFormulario TermoQueCita(string fato, int ordem = 0) =>
        TermoExigidoFormulario.Criar(
            $"TERMO_{ordem}", ordem,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "TERMO_DE_TESTE", 1, "Declaração", "Texto", "Base legal", "REGISTRO_DIGITAL_SEM_LOG_IP", new string('a', 64)),
            PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
            Obrigatoriedade.Sempre).Value!;
}
