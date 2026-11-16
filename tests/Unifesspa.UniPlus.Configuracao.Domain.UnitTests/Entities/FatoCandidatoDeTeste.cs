namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O fato do candidato válido que os testes de domínio do catálogo variam um campo por vez.</summary>
internal static class FatoCandidatoDeTeste
{
    internal const string BindingCorRaca = "CAMPO_FORMULARIO:COR_RACA";
    internal const string PontoResolucaoInscricao = "INSCRICAO";
    internal const string Finalidade = "Enquadramento na reserva de vagas.";
    internal const string Descricao = "Cor ou raça que o candidato declara.";

    internal static Result<FatoCandidato> Criar(
        string codigo = "COR_RACA",
        string nome = "Cor ou raça",
        string? descricao = Descricao,
        DominioFato dominio = DominioFato.Categorico,
        OrigemFato origem = OrigemFato.Declarado,
        CardinalidadeFato cardinalidade = CardinalidadeFato.Escalar,
        FonteValoresFato? fonteValores = FonteValoresFato.Global,
        FormatoTexto? formato = null,
        string pontoResolucao = PontoResolucaoInscricao,
        string binding = BindingCorRaca,
        ClassificacaoDeProtecao classificacao = ClassificacaoDeProtecao.Sensivel,
        string finalidade = Finalidade,
        HipoteseLegalTratamento hipotese = HipoteseLegalTratamento.CumprimentoObrigacaoLegal,
        bool sistema = false,
        EscopoFato escopo = EscopoFato.Candidato) =>
        FatoCandidato.Criar(
            codigo, nome, descricao, dominio, origem, cardinalidade,
            dominio == DominioFato.Categorico ? fonteValores : null,
            formato,
            pontoResolucao, binding, escopo, classificacao, finalidade, hipotese, sistema);
}
