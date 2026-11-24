namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>Fatos, definições e catálogos comuns aos testes da alteração da definição do fato.</summary>
internal static class FatoDeTesteDaAlteracao
{
    internal const string Finalidade = "Enquadramento na reserva de vagas.";
    internal const HipoteseLegalTratamento Hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal;
    internal static readonly PrecedenciaFase[] InscricaoAntesDeHabilitacao =
        [PrecedenciaFase.Criar("INSCRICAO", "HABILITACAO", permiteSobreposicao: false, []).Value!];

    // O derivado resolve em HABILITACAO, antes de A (RESULTADO_FINAL, recusa já existente); a segunda
    // regra cita B, que só é conhecido em HABILITACAO.
    internal static (FatoCandidato Derivado, CatalogoDeFatos Catalogo) DerivadoComDuasRegras(bool segundaRegra = true)
    {
        FatoCandidato a = Declarado("FATO_A");
        FatoCandidato b = Declarado("FATO_B", ponto: "HABILITACAO");
        FatoCandidato derivado = Derivado("PERFIL", ponto: "HABILITACAO");
        RegraDerivacao[] regras = segundaRegra ? [Booleana(("FATO_A", true)), Booleana(("FATO_B", true))] : [Booleana(("FATO_A", true))];
        derivado.DefinirRegrasPadrao(regras, Catalogo(a, b, derivado)).IsSuccess.Should().BeTrue();
        a.AlterarDefinicao(DeDeclarado(TipoDoDado.SimOuNao, ponto: "RESULTADO_FINAL"), Catalogo(a, b, derivado)).IsSuccess.Should().BeTrue();
        PrecedenciaFase[] precedencias =
        [
            PrecedenciaFase.Criar("INSCRICAO", "HABILITACAO", permiteSobreposicao: false, []).Value!,
            PrecedenciaFase.Criar("HABILITACAO", "RESULTADO_FINAL", permiteSobreposicao: false, []).Value!,
        ];
        return (derivado, new CatalogoDeFatos([a, b, derivado], precedencias));
    }

    internal static DefinicaoDoFato Definicao(
        TipoDoDado? dominio = TipoDoDado.SimOuNao,
        QuantidadeDeValores? cardinalidade = null,
        FonteValoresFato? fonte = null,
        FormatoTexto? formato = null,
        string ponto = "INSCRICAO",
        EscopoFato? escopo = EscopoFato.Candidato,
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal,
        string finalidade = Finalidade,
        HipoteseLegalTratamento hipotese = Hipotese) =>
        new(dominio, cardinalidade, fonte, formato, ponto, escopo, classificacao, finalidade, hipotese);

    // O declarado informa a cardinalidade; o derivado e o agregado a inferem.
    internal static DefinicaoDoFato DeDeclarado(
        TipoDoDado dominio,
        FormatoTexto? formato = null,
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal,
        HipoteseLegalTratamento hipotese = Hipotese,
        string ponto = "INSCRICAO") =>
        Definicao(dominio, QuantidadeDeValores.ValorUnico, formato: formato, ponto: ponto, classificacao: classificacao, hipotese: hipotese);

    internal static FatoCandidato Declarado(
        string codigo,
        TipoDoDado dominio = TipoDoDado.SimOuNao,
        FonteValoresFato? fonte = null,
        string ponto = "INSCRICAO",
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            new DefinicaoDoFato(dominio, QuantidadeDeValores.ValorUnico, fonte, null, ponto, EscopoFato.Candidato, classificacao, Finalidade, Hipotese)).Value!;

    internal static FatoCandidato DeMembro(
        string codigo,
        TipoDoDado dominio = TipoDoDado.Opcao,
        FonteValoresFato fonte = FonteValoresFato.Catalogo,
        string ponto = "INSCRICAO",
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal) =>
        CriacaoDoFato.CriarDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            new DefinicaoDoFato(dominio, QuantidadeDeValores.ValorUnico, dominio == TipoDoDado.Opcao ? fonte : null, null, ponto, EscopoFato.MembroGrupo, classificacao, Finalidade, Hipotese)).Value!;

    internal static FatoCandidato Agregado(
        string codigo,
        FatoCandidato membro,
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal,
        string ponto = "INSCRICAO",
        PrecedenciaFase[]? precedencias = null) =>
        CriacaoDoFato.CriarAgregadoDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            membro.Codigo,
            new CatalogoDeFatos([membro], precedencias ?? []),
            new DefinicaoDoFato(null, null, null, null, ponto, null, classificacao, Finalidade, Hipotese)).Value!;

    internal static FatoCandidato Derivado(
        string codigo,
        TipoDoDado dominio = TipoDoDado.SimOuNao,
        string ponto = "INSCRICAO",
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Pessoal) =>
        CriacaoDoFato.CriarDerivadoDoAdministrador(
            new DescritivoDoFato(codigo, codigo, "Descrição do fato."),
            new DefinicaoDoFato(dominio, null, null, null, ponto, EscopoFato.Candidato, classificacao, Finalidade, Hipotese)).Value!;

    internal static CatalogoDeFatos Catalogo(params FatoCandidato[] fatos) => new(fatos, []);

    internal static RegraDerivacao Booleana(params (string Fato, object Valor)[] condicoes) =>
        RegraDerivacao.CriarBooleana(Quando(condicoes));

    internal static PredicadoDnf Quando(params (string Fato, object Valor)[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas(
            [.. condicoes.Select(static c => (0, CondicaoDnf.Criar(c.Fato, Operador.Igual, JsonSerializer.SerializeToElement(c.Valor)).Value!))]).Value!;
}
