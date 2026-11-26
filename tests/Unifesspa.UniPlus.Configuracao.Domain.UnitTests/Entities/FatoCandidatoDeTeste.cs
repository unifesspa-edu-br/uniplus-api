namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>O fato do candidato válido que os testes de domínio do catálogo variam um campo por vez.</summary>
internal static class FatoCandidatoDeTeste
{
    internal const string BindingCorRaca = "CAMPO_FORMULARIO:COR_RACA";
    internal const string PontoResolucaoInscricao = "INSCRICAO";
    internal const string Finalidade = "Enquadramento na reserva de vagas.";
    internal const string Descricao = "Cor ou raça que o candidato declara.";

    internal static readonly DescritivoDoFato Descritivo = new("COR_RACA", "Cor ou raça", Descricao);

    internal static readonly DefinicaoDoFato Definicao = new(
        TipoDoDado.Opcao,
        QuantidadeDeValores.ValorUnico,
        FonteValoresFato.Catalogo,
        null,
        PontoResolucaoInscricao,
        EscopoFato.Candidato,
        ClassificacaoDeProtecao.Sensivel,
        Finalidade,
        HipoteseLegalTratamento.CumprimentoObrigacaoLegal);

    /// <summary>O fato válido de <see cref="Descritivo"/> e <see cref="Definicao"/>; cada teste varia só o que importa a ele.</summary>
    internal static Result<FatoCandidato> Criar(
        DescritivoDoFato? descritivo = null,
        DefinicaoDoFato? definicao = null,
        OrigemFato origem = OrigemFato.Declarado,
        string binding = BindingCorRaca,
        bool sistema = false) =>
        CriacaoDoFato.Criar(descritivo ?? Descritivo, origem, binding, definicao ?? Definicao, sistema: sistema);
}
