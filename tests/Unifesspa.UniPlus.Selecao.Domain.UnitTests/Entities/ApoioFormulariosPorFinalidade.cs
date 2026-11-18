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
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, etapaCodigo: etapa, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

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
        FatoColetado.Criar(
            codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Nunca,
            [CondicaoPrecondicaoFato.Criar(0, citado, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!],
            etapaCodigo: FormularioDeTeste.Secao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

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
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, null, etapaCodigo: secao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    internal static FatoColetado Opcional(string codigo, int ordem, params CondicaoPrecondicaoFato[] precondicoes) =>
        FatoColetado.Criar(codigo, ordem, codigo, TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, precondicoes, etapaCodigo: FormularioDeTeste.Secao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    /// <summary>A data de nascimento na seção dos dados básicos, com a obrigatoriedade do caso.</summary>
    internal static FatoColetado DataDeNascimento(Obrigatoriedade obrigatoriedade) => FatoColetado.Criar(
        "DATA_NASCIMENTO", ConjuntoBasicoDaInscricao.Itens.Single(static i => i.FatoCodigo == "DATA_NASCIMENTO").Ordem, "Data de nascimento",
        TipoRenderizacao.Data, obrigatoriedade, null, etapaCodigo: ConjuntoBasicoDaInscricao.CodigoDaSecao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    internal static FatoColetado CitaFaixaEtaria(int ordem) => Opcional(
        "MENOR_SOB_RESPONSAVEL", ordem, CondicaoPrecondicaoFato.Criar(0, "FAIXA_ETARIA", Operador.MenorIgual, JsonSerializer.SerializeToElement(17)).Value!);

    internal static FatoColetado ItemQueCitaODerivado(int ordem) =>
        FatoColetado.Criar(
            "CONCORRER_EP", ordem, "Concorrer", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca,
            [CondicaoPrecondicaoFato.Criar(0, "REGIME_DE_VAGA", Operador.Igual, JsonSerializer.SerializeToElement("AC")).Value!],
            etapaCodigo: FormularioDeTeste.Secao, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

    internal static TermoExigidoFormulario TermoQueCita(string fato, int ordem = 0) =>
        TermoExigidoFormulario.Criar(
            $"TERMO_{ordem}", ordem,
            new VersaoTermoEscolhida(Guid.CreateVersion7(), Guid.CreateVersion7(), "TERMO_DE_TESTE", 1, "Declaração", "Texto", "Base legal", "REGISTRO_DIGITAL_SEM_LOG_IP", new string('a', 64)),
            PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
            Obrigatoriedade.Sempre).Value!;
}
